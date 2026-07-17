using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DuizhunDataInverse
{
    /// <summary>
    /// 反推算法核心：从「(总转角, 交点X, 交点Y)」序列反推晶圆相对底盘圆心的偏心向量与晶圆半径。
    ///
    /// 数学模型（正问题）：
    ///   底盘圆心(旋转轴) O = (Ocx,Ocy)，已知或待反推；
    ///   晶圆中心 C(φ) = O + R(φ)·e ，R(φ)=[[cosφ,-sinφ],[sinφ,cosφ]]，e 为 φ=0 时的偏心向量；
    ///   晶圆边缘圆半径 R（晶圆尺寸，待反推）；
    ///   线阵测得点 P 满足 |P - C(φ)| = R。
    ///
    /// 反推（已知 O）：把每个点去旋转 w = R(-φ)(P - O)，则所有 w 落在一个圆上，
    ///   该圆圆心 = e（偏心向量），半径 = R。圆心拟合即得结果。
    /// 反推（未知 O）：对 (Ocx,Ocy,ex,ey,R) 做非线性最小二乘（Gauss-Newton）。
    /// </summary>
    public static class InverseSolver
    {
        public class SamplePoint
        {
            public double AngleDeg; // φ_i（总转角，度）
            public double X;        // 交点世界坐标 X
            public double Y;        // 交点世界坐标 Y
        }

        public class SolveResult
        {
            public bool Success;
            public string Message = "";

            public double Eccentricity;     // |e|：任意时刻恒定偏离量
            public double EccentricAngleDeg;// e 的方向角（φ=0 时，相对底盘圆心，度）
            public double WaferRadius;      // R：晶圆半径
            public double CenterX;          // e 向量 X（去旋转坐标系）
            public double CenterY;          // e 向量 Y
            public double Ocx, Ocy;         // 底盘圆心

            public double RmsResidual;      // 拟合残差 RMS（像素），越小越好
            public int UsedPoints;
            public int RejectedPoints;      // 被缺口等剔除的点数
            public double AngleMin, AngleMax, Revolutions;

            public List<PointF> DeRotatedPoints;     // 去旋转后的点（用于绘图）
            public double FitCircleCx, FitCircleCy, FitCircleR; // 拟合圆
        }

        #region 解析 CSV（兼容原工程 lstLog / 自动保存 CSV 的格式）
        /// <summary>
        /// 解析原工程导出的 CSV。逐行匹配关键标记，提取 (总转角, X, Y)。
        /// 原工程日志行格式： 总转角:,123.4500,° | 轮廓交点 (X，Y),100.1234,200.5678
        /// </summary>
        public static List<SamplePoint> ParseCsv(string path)
        {
            var list = new List<SamplePoint>();
            if (!File.Exists(path)) return list;

            var reAngle = new Regex(@"总转角:\D*([-\d.]+)");
            var rePoint = new Regex(@"轮廓交点\s*\(X，Y\)\D+([-\d.]+)\D+([-\d.]+)");

            foreach (var raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("\""))
                {
                    if (line.EndsWith("\"")) line = line.Substring(1, line.Length - 2);
                    line = line.Replace("\"\"", "\"");
                }
                if (!line.Contains("总转角") || !line.Contains("轮廓交点")) continue;

                var mA = reAngle.Match(line);
                var mP = rePoint.Match(line);
                if (!mA.Success || !mP.Success) continue;

                if (double.TryParse(mA.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ang) &&
                    double.TryParse(mP.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                    double.TryParse(mP.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    list.Add(new SamplePoint { AngleDeg = ang, X = x, Y = y });
                }
            }
            return list;
        }
        #endregion

        #region 主入口
        /// <summary>
        /// 反推。chuckCx/Cy 为 null 时自动反推底盘圆心（需至少一整圈数据）。
        /// rejectNotch：是否用残差剔除缺口离群点（推荐开启）。
        /// </summary>
        public static SolveResult Solve(List<SamplePoint> data, double? chuckCx, double? chuckCy, bool rejectNotch)
        {
            var res = new SolveResult();
            if (data == null || data.Count < 10)
            {
                res.Success = false;
                res.Message = $"有效数据点不足（{data?.Count ?? 0} < 10），无法拟合。";
                return res;
            }
            bool knownO = chuckCx.HasValue && chuckCy.HasValue;
            if (!knownO) return SolveUnknownCenter(data, rejectNotch);

            double Ocx = chuckCx.Value, Ocy = chuckCy.Value;

            // 1. 去旋转：w = R(-φ)(P - O)
            var all = new List<PointF>(data.Count);
            foreach (var s in data)
            {
                double rad = s.AngleDeg * Math.PI / 180.0;
                double dx = s.X - Ocx, dy = s.Y - Ocy;
                double c = Math.Cos(-rad), sn = Math.Sin(-rad);
                all.Add(new PointF((float)(dx * c - dy * sn), (float)(dx * sn + dy * c)));
            }

            // 2. 缺口剔除（基于圆拟合残差）
            var pts = all;
            if (rejectNotch)
            {
                for (int iter = 0; iter < 4; iter++)
                {
                    var fit = FitCircle(pts);
                    double thr = 3.0 * Math.Max(fit.rms, 1e-6);
                    var keep = new List<PointF>();
                    foreach (var p in pts)
                    {
                        double d = Math.Sqrt((p.X - fit.cx) * (p.X - fit.cx) + (p.Y - fit.cy) * (p.Y - fit.cy));
                        if (Math.Abs(d - fit.r) <= thr) keep.Add(p);
                    }
                    if (keep.Count == pts.Count || keep.Count < 3) break;
                    pts = keep;
                }
            }

            // 3. 最终圆拟合：圆心 = e，半径 = R
            var f = FitCircle(pts);
            res.Success = true;
            res.Ocx = Ocx; res.Ocy = Ocy;
            res.CenterX = f.cx; res.CenterY = f.cy; res.WaferRadius = f.r;
            res.Eccentricity = Math.Sqrt(f.cx * f.cx + f.cy * f.cy);
            res.EccentricAngleDeg = Math.Atan2(f.cy, f.cx) * 180.0 / Math.PI;
            res.RmsResidual = f.rms;
            res.UsedPoints = pts.Count;
            res.RejectedPoints = all.Count - pts.Count;
            res.AngleMin = data.Min(d => d.AngleDeg);
            res.AngleMax = data.Max(d => d.AngleDeg);
            res.Revolutions = (res.AngleMax - res.AngleMin) / 360.0;
            res.DeRotatedPoints = pts;
            res.FitCircleCx = f.cx; res.FitCircleCy = f.cy; res.FitCircleR = f.r;
            res.Message = "反推成功（底盘圆心已知，去旋转圆拟合法）。";
            return res;
        }
        #endregion

        #region 底盘圆心未知：5 参数 Gauss-Newton 反推
        private static SolveResult SolveUnknownCenter(List<SamplePoint> data, bool rejectNotch)
        {
            int n = data.Count;
            // 初值：圆心取质心；半径/偏心用径向距离极值估计
            double Ocx = data.Average(p => p.X), Ocy = data.Average(p => p.Y);
            double dmax = 0, dmin = double.MaxValue;
            foreach (var p in data)
            {
                double d = Math.Sqrt((p.X - Ocx) * (p.X - Ocx) + (p.Y - Ocy) * (p.Y - Ocy));
                dmax = Math.Max(dmax, d); dmin = Math.Min(dmin, d);
            }
            double R = (dmax + dmin) / 2.0;
            double eMag = Math.Max((dmax - dmin) / 2.0, 1e-3);
            double ex = eMag, ey = 0.0;

            for (int it = 0; it < 120; it++)
            {
                double[][] J = new double[5][]; for (int i = 0; i < 5; i++) J[i] = new double[5];
                double[] Jr = new double[5];
                for (int k = 0; k < n; k++)
                {
                    var s = data[k];
                    double rad = s.AngleDeg * Math.PI / 180.0;
                    double cr = Math.Cos(rad), sr = Math.Sin(rad);
                    double Cx = Ocx + ex * cr - ey * sr;
                    double Cy = Ocy + ex * sr + ey * cr;
                    double dx = s.X - Cx, dy = s.Y - Cy;
                    double dist = Math.Sqrt(dx * dx + dy * dy); if (dist < 1e-9) dist = 1e-9;
                    double resid = dist - R;

                    double dOcx = -dx / dist, dOcy = -dy / dist, dR = -1.0;
                    double dEx = -((cr * dx + sr * dy) / dist);
                    double dEy = -(((-sr) * dx + cr * dy) / dist);
                    double[] col = { dOcx, dOcy, dEx, dEy, dR };
                    for (int a = 0; a < 5; a++) { Jr[a] += col[a] * resid; for (int b = 0; b < 5; b++) J[a][b] += col[a] * col[b]; }
                }
                // Levenberg 阻尼，提升稳定性
                for (int i = 0; i < 5; i++) J[i][i] += 1e-3 * Math.Abs(J[i][i]) + 1e-6;
                double[] dlt = SolveLinear(J, Jr, 5);
                Ocx -= dlt[0]; Ocy -= dlt[1]; ex -= dlt[2]; ey -= dlt[3]; R -= dlt[4];
                if (Math.Abs(dlt[0]) < 1e-9 && Math.Abs(dlt[1]) < 1e-9 && Math.Abs(dlt[2]) < 1e-9 &&
                    Math.Abs(dlt[3]) < 1e-9 && Math.Abs(dlt[4]) < 1e-9) break;
            }

            var res = new SolveResult { Success = true, Ocx = Ocx, Ocy = Ocy };
            res.CenterX = ex; res.CenterY = ey; res.WaferRadius = R;
            res.Eccentricity = Math.Sqrt(ex * ex + ey * ey);
            res.EccentricAngleDeg = Math.Atan2(ey, ex) * 180.0 / Math.PI;

            // 计算去旋转点 + RMS
            var w = new List<PointF>(n);
            double ss = 0;
            foreach (var s in data)
            {
                double rad = s.AngleDeg * Math.PI / 180.0;
                double dx = s.X - Ocx, dy = s.Y - Ocy;
                double c = Math.Cos(-rad), sn = Math.Sin(-rad);
                double wx = dx * c - dy * sn, wy = dx * sn + dy * c;
                w.Add(new PointF((float)wx, (float)wy));
                double dd = Math.Sqrt((wx - ex) * (wx - ex) + (wy - ey) * (wy - ey)) - R;
                ss += dd * dd;
            }
            res.RmsResidual = Math.Sqrt(ss / n);
            res.UsedPoints = n; res.RejectedPoints = 0;
            res.AngleMin = data.Min(d => d.AngleDeg);
            res.AngleMax = data.Max(d => d.AngleDeg);
            res.Revolutions = (res.AngleMax - res.AngleMin) / 360.0;
            res.DeRotatedPoints = w;
            res.FitCircleCx = ex; res.FitCircleCy = ey; res.FitCircleR = R;
            res.Message = "反推成功（底盘圆心未知，5 参数非线性最小二乘）。";
            return res;
        }
        #endregion

        #region 圆拟合：Kåsa 初值 + Gauss-Newton 精化
        /// <summary>
        /// 对点集做最小二乘圆拟合，返回 (圆心X, 圆心Y, 半径, 残差RMS)。
        /// </summary>
        public static (double cx, double cy, double r, double rms) FitCircle(List<PointF> pts)
        {
            int n = pts.Count;
            if (n < 3) return (0, 0, 0, double.MaxValue);

            // Kåsa 线性初值：x²+y² + D x + E y + F = 0
            double Sx = 0, Sy = 0, Sx2 = 0, Sy2 = 0, Sxy = 0, Sx_z = 0, Sy_z = 0, Sz = 0;
            for (int i = 0; i < n; i++)
            {
                double x = pts[i].X, y = pts[i].Y, z = x * x + y * y;
                Sx += x; Sy += y; Sx2 += x * x; Sy2 += y * y; Sxy += x * y;
                Sx_z += x * z; Sy_z += y * z; Sz += z;
            }
            double[][] A = New(3);
            double[] b = new double[3];
            A[0][0] = Sx2; A[0][1] = Sxy; A[0][2] = Sx;
            A[1][0] = Sxy; A[1][1] = Sy2; A[1][2] = Sy;
            A[2][0] = Sx; A[2][1] = Sy; A[2][2] = n;
            b[0] = -Sx_z; b[1] = -Sy_z; b[2] = -Sz;
            double[] c = SolveLinear(A, b, 3);
            double cx = -c[0] / 2, cy = -c[1] / 2;
            double r = Math.Sqrt(Math.Max(0, (c[0] * c[0] + c[1] * c[1]) / 4 - c[2]));

            // Gauss-Newton 精化（几何残差最小）
            for (int it = 0; it < 40; it++)
            {
                double[][] J = New(3);
                double[] Jr = new double[3];
                for (int i = 0; i < n; i++)
                {
                    double dx = pts[i].X - cx, dy = pts[i].Y - cy;
                    double dist = Math.Sqrt(dx * dx + dy * dy); if (dist < 1e-9) dist = 1e-9;
                    double res = dist - r;
                    double gx = -dx / dist, gy = -dy / dist; // ∂res/∂cx, ∂res/∂cy
                    J[0][0] += gx * gx; J[0][1] += gx * gy; J[0][2] += gx * (-1);
                    J[1][1] += gy * gy; J[1][2] += gy * (-1); J[2][2] += (-1) * (-1);
                    Jr[0] += gx * res; Jr[1] += gy * res; Jr[2] += (-1) * res;
                }
                J[1][0] = J[0][1]; J[2][0] = J[0][2]; J[2][1] = J[1][2];
                double[] d = SolveLinear(J, Jr, 3);
                cx -= d[0]; cy -= d[1]; r -= d[2];
                if (Math.Abs(d[0]) < 1e-9 && Math.Abs(d[1]) < 1e-9 && Math.Abs(d[2]) < 1e-9) break;
            }

            double s = 0;
            foreach (var p in pts) { double d = Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)) - r; s += d * d; }
            return (cx, cy, r, Math.Sqrt(s / n));
        }
        #endregion

        #region 合成数据生成（自测用）
        /// <summary>
        /// 生成合成对准数据 CSV：按正问题模型在边缘圆上采样，叠加高斯噪声。
        /// 用于验证反推函数（已知真值后可比对误差）。
        /// </summary>
        public static void GenerateSynthetic(string path, double Ocx, double Ocy, double ex, double ey,
                                             double R, int revs, int perRev, double noise)
        {
            var rnd = new Random(20260614);
            using var sw = new StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine("==== 合成对准数据（反推自测用） ====");
            int total = revs * perRev;
            for (int k = 0; k < total; k++)
            {
                double phi = (double)k / perRev * 360.0;
                double rad = phi * Math.PI / 180.0;
                double cx = Ocx + ex * Math.Cos(rad) - ey * Math.Sin(rad);
                double cy = Ocy + ex * Math.Sin(rad) + ey * Math.Cos(rad);
                double theta = rnd.NextDouble() * 2 * Math.PI;
                double px = cx + R * Math.Cos(theta) + (rnd.NextDouble() * 2 - 1) * noise;
                double py = cy + R * Math.Sin(theta) + (rnd.NextDouble() * 2 - 1) * noise;
                sw.WriteLine($"总转角:,{phi:F4},° | 轮廓交点 (X，Y),{px:F4},{py:F4}");
            }
        }
        #endregion

        #region 通用线性求解（Gauss-Jordan，带主元）
        private static double[][] New(int n) { var a = new double[n][]; for (int i = 0; i < n; i++) a[i] = new double[n]; return a; }

        private static double[] SolveLinear(double[][] A, double[] b, int n)
        {
            double[][] M = new double[n][];
            for (int i = 0; i < n; i++) { M[i] = new double[n + 1]; for (int j = 0; j < n; j++) M[i][j] = A[i][j]; M[i][n] = b[i]; }
            for (int col = 0; col < n; col++)
            {
                int piv = col;
                for (int r = col + 1; r < n; r++) if (Math.Abs(M[r][col]) > Math.Abs(M[piv][col])) piv = r;
                if (Math.Abs(M[piv][col]) < 1e-12) continue;
                if (piv != col) { var tmp = M[col]; M[col] = M[piv]; M[piv] = tmp; }
                double pv = M[col][col];
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double f = M[r][col] / pv; if (f == 0) continue;
                    for (int c = col; c <= n; c++) M[r][c] -= f * M[col][c];
                }
            }
            double[] x = new double[n];
            for (int i = 0; i < n; i++) { double d = M[i][i]; x[i] = Math.Abs(d) < 1e-12 ? 0 : M[i][n] / d; }
            return x;
        }
        #endregion
    }
}
