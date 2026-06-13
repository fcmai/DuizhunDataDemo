using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DuizhunDataDemo
{
    public partial class Form1 : Form
    {
        //参数
        private double _rotCenterX = 250;    //大圆旋转中心X
        private double _rotCenterY = 250;    //大圆旋转中心Y
        private double _bigR = 120;          //大圆半径
        private double _waferR = 35;         //晶圆外圆半径
        private double _initWaferCx;         //【晶圆初始中心X（固定不变）】
        private double _initWaferCy;         //【晶圆初始中心Y（固定不变）】
        private double _omega = 120d;        //角速度 °/s
        private double _totalAngle = 0;      //累计旋转角度 
        double r_pianxin = 0; //偏心距（可选参数，当前未使用）
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer() { Interval = 50 };
        private Bitmap _bmp;
        private readonly object _lock = new();
        int maxJiaodu旋转 = 3600;
        double rad = 0;
        //线阵端点
        private PointF _lineLeft = new PointF(80, 320);
        private PointF _lineRight = new PointF(420, 320);

        // ========= 新增：全局统一缺口尺寸 =========
        private readonly float _notchDepth = 8.0f;
        private readonly float _notchWidth = 20f;

        //标尺参数
        private bool _showRuler = true;      //是否显示标尺
        private int _rulerStep = 50;         //标尺步长（像素）
        private Color _rulerColor = Color.FromArgb(150, 100, 100, 100); //标尺颜色（半透明灰色）
        private Color _rulerTextColor = Color.Black; //标尺文字颜色

        public Form1()
        {
            InitializeComponent();
            _timer.Tick += TimerTick;
            // 计算晶圆初始安装中心：在大圆右侧0°位置（初始静止点位）
            _initWaferCx = _rotCenterX + _bigR;
            _initWaferCy = _rotCenterY;
            _bmp = new Bitmap(pictureBox1.Width, pictureBox1.Height);
            pictureBox1.Image = _bmp;
        }

        private void UpdateParas()
        {
            _rotCenterX = (double)nudCx.Value;    //大圆旋转中心X 250
            _rotCenterY = (double)nudCy.Value;    //大圆旋转中心Y 250
            _bigR = (double)nudDipanR.Value;        //底盘圆半径  150
            _waferR = (double)nudWaferR.Value;    //晶圆外圆半径
            _initWaferCx = (double)nudWaferCx.Value;         //【晶圆初始中心X（固定不变）】
            _initWaferCy = (double)nudWaferCy.Value;         //【晶圆初始中心Y（固定不变）】修复：这里应该是Cy而不是Cx
            _omega = (double)nudOmega.Value;      //角速度 °/s
            _totalAngle = 0;                      //累计旋转角度 

            //线阵端点
            _lineLeft = new PointF((float)nudLx.Value, (float)nudLy.Value);
            _lineRight = new PointF((float)nudRx.Value, (float)nudRy.Value);
            maxJiaodu旋转 = (int)nud旋转总角度.Value;
            r_pianxin = Math.Sqrt((_initWaferCx - _rotCenterX) * (_initWaferCx - _rotCenterX) + (_initWaferCy - _rotCenterY) * (_initWaferCy - _rotCenterY));
            rad = MathHelper.GetAngleRadianFull(_rotCenterX, _rotCenterY, _initWaferCx, _initWaferCy);
        }

        private void AddParametersToLog()
        {
            // 所有参数日志输出（统一格式：变量名=值，保留4位小数）
            lstLog.Items.Add($"==================================== 运行参数 ====================================");
            lstLog.Items.Add($"大圆旋转中心X:_rotCenterX,{_rotCenterX:F4}");
            lstLog.Items.Add($"大圆旋转中心Y:_rotCenterY,{_rotCenterY:F4}");
            lstLog.Items.Add($"底盘圆半径:_bigR,{_bigR:F4}");
            lstLog.Items.Add($"晶圆外圆半径:_waferR,{_waferR:F4}");
            lstLog.Items.Add($"晶圆初始中心X:_initWaferCx,{_initWaferCx:F4}");
            lstLog.Items.Add($"晶圆初始中心Y:_initWaferCy,{_initWaferCy:F4}");
            lstLog.Items.Add($"角速度°/s:_omega,{_omega:F4}");
            lstLog.Items.Add($"累计旋转角度:_totalAngle,{_totalAngle:F4}");
            lstLog.Items.Add($"线阵左端点X:_lineLeft.X,{_lineLeft.X:F4}");
            lstLog.Items.Add($"线阵左端点Y:_lineLeft.Y,{_lineLeft.Y:F4}");
            lstLog.Items.Add($"线阵右端点X:_lineRight.X,{_lineRight.X:F4}");
            lstLog.Items.Add($"线阵右端点Y:_lineRight.Y,{_lineRight.Y:F4}");
            lstLog.Items.Add($"最大旋转角度:maxJiaodu旋转,{maxJiaodu旋转}");
            lstLog.Items.Add($"偏心距离:r_pianxin,{r_pianxin:F4}");
            lstLog.Items.Add($"初始夹角弧度:rad,{rad:F4}");
            lstLog.Items.Add($"====================================================================================");
            lstLog.Items.Add($"参数更新:旋转中心=({_rotCenterX},{_rotCenterY})," +
                $"底盘半径={_bigR},晶圆半径={_waferR},初始中心=({_initWaferCx},{_initWaferCy})," +
                $"角速度={_omega}°/s,线阵分辨率{nud线阵分辨率.Value},端点位置,({_lineLeft.X},{_lineLeft.Y})-({_lineRight.X},{_lineRight.Y})," +
                $"最大旋转角度={maxJiaodu旋转}°");
        }


        private void btnStart_Click(object sender, EventArgs e)
        {
            lock (_lock)
            {
                _totalAngle = 0;
                _timer.Enabled = false;

                UpdateParas();

                lstLog.Items.Clear();
                AddParametersToLog();
                //子线程启动定时器（多线程需求）
                new Thread(() =>
                {
                    Invoke(() => _timer.Enabled = true);
                }).Start();
            }
        }


        // 【核心】保存按钮点击事件
        private void btnSaveToCsv_Click(object sender, EventArgs e)
        {
            // 1. 配置保存对话框，限制CSV格式
            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Title = "保存ListBox内容为CSV文件";
                saveDialog.Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
                saveDialog.DefaultExt = "csv";
                saveDialog.FileName = "内容导出.csv"; // 默认文件名

                // 2. 用户取消选择则直接退出
                if (saveDialog.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    // 3. 生成CSV格式内容
                    string csvContent = GenerateCsvFromListBox(lstLog);

                    // 4. 写入文件，UTF-8编码兼容中文和特殊字符
                    File.WriteAllText(saveDialog.FileName, csvContent, Encoding.UTF8);

                    MessageBox.Show($"保存成功！\n文件路径：{saveDialog.FileName}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // 【核心工具函数】从ListBox生成符合规范的CSV内容
        private string GenerateCsvFromListBox(ListBox listBoxLog)
        {
            StringBuilder csvBuilder = new StringBuilder();

            // 遍历ListBox所有项
            foreach (var item in listBoxLog.Items)
            {
                // 提取项的显示文本（兼容手动添加的字符串/数据绑定对象）
                string itemText = listBoxLog.GetItemText(item);

                // 处理CSV特殊字符转义，避免格式错乱
                string escapedText = EscapeCsvField(itemText);

                // 每一项占一行，写入CSV
                csvBuilder.AppendLine(escapedText);
            }

            return csvBuilder.ToString();
        }

        // 【CSV规范工具】转义CSV字段中的特殊字符
        private string EscapeCsvField(string field)
        {
            // 空值直接返回空
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // 包含特殊字符（逗号、双引号、换行符）时，必须用双引号包裹
            //bool needsQuotes = field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r");
            bool needsQuotes = field.Contains("\"") || field.Contains("\n") || field.Contains("\r");

            if (needsQuotes)
            {
                // 字段内的双引号必须转义为两个双引号（CSV标准规范）
                field = field.Replace("\"", "\"\"");
                // 用双引号包裹整个字段
                field = $"\"{field}\"";
            }

            return field;
        }

        #region 【核心定时器事件】每50ms更新一次晶圆位置并重绘
        private void TimerTick(object sender, EventArgs e)
        {
            lock (_lock)
            {
                float currX = 0;
                float currY =  0;
                float notchDepth = _notchDepth;
                float notchWidth = _notchWidth;
                float notchBaseAngleDeg = 0;
                // 最终缺口朝向总角度（角度制）
                float totalNotchAngleDeg = 0;
                // 转为弧度，用于三角函数计算
                float totalNotchAngleRad = 0f;

                // 2. 修复三角函数：屏幕Y向下，使用标准极坐标公式
                // 极坐标：x = r*cosθ , y = r*sinθ
                PointF notchTopLeft;
                PointF notchTopRight;

                // 缺口底部顶点
                PointF notchBottom;
                notchDepth = _notchDepth;
                //单步转角
                double stepAngle = _omega * 50 / 1000d;
                _totalAngle += stepAngle;
                if (_totalAngle > maxJiaodu旋转)
                {
                    _timer.Enabled = false;
                    return;
                }


                // ========== 修复后代码 ==========
                // 初始夹角 + 累计旋转角度，从界面设置的起点开始旋转
                double currentRad = rad + _totalAngle * Math.PI / 180.0;
                // 晶圆中心绕旋转中心公转（标准极坐标，适配屏幕坐标系）
                double currWaferX = _rotCenterX + r_pianxin * Math.Cos(currentRad);
                double currWaferY = _rotCenterY + r_pianxin * Math.Sin(currentRad);
                //离屏绘图
                using (Graphics g = Graphics.FromImage(_bmp))
                {
                    g.Clear(Color.White);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    #region 0. 绘制标尺（新增）
                    if (_showRuler)
                    {
                        DrawRuler(g);
                    }
                    #endregion


                    #region 1.绘制基准大圆(灰色填充 + 黑色边框)
                    using (SolidBrush brushBig = new SolidBrush(Color.LightGray))
                    using (Pen penBig = new Pen(Color.Black, 1))
                    {
                        // 先填充
                        g.FillEllipse(brushBig,
                            (float)(_rotCenterX - _bigR),
                            (float)(_rotCenterY - _bigR),
                            (float)(_bigR * 2),
                            (float)(_bigR * 2));

                        // 再画边框（可选）
                        // 自定义：短划线 5px，空 3px，点 2px，空 3px
                        penBig.DashPattern = new float[] { 2, 3 };
                        g.DrawEllipse(penBig,
                            (float)(_rotCenterX - _bigR),
                            (float)(_rotCenterY - _bigR),
                            (float)(_bigR * 2),
                            (float)(_bigR * 2));
                    }
                    #endregion


                    #region 2.标记【大圆旋转中心】红色十字
                    float cx = (float)_rotCenterX;
                    float cy = (float)_rotCenterY;
                    using (Pen penRed = new Pen(Color.Red, 2))
                    {
                        g.DrawLine(penRed, cx - 8, cy, cx + 8, cy);
                        g.DrawLine(penRed, cx, cy - 8, cx, cy + 8);
                    }
                    #endregion
                    #region 1.绘制基准大圆(淡灰色)
                    using (Pen penGz = new Pen(Color.LightYellow, 2))
                    {
                        // 👇 在这里修改线型！
                        //penGz.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; // 虚线
                        //public enum DashStyle
                        //{   //Solid,                        //Dash,                        //Dot,                        //DashDot,                        //DashDotDot,                        //Custom                        //}
                        // 自定义：短划线 5px，空 3px，点 2px，空 3px
                        penGz.DashPattern = new float[] { 5, 3, 2, 3 };

                        g.DrawEllipse(penGz,
                            (float)(_rotCenterX - r_pianxin),
                            (float)(_rotCenterY - r_pianxin),
                            (float)(r_pianxin * 2),
                            (float)(r_pianxin * 2));
                    }
                    #endregion

                    #region 3.标记【晶圆初始中心（固定不动，黄色实心圆点）】关键补充
                    float initX = (float)_initWaferCx;
                    float initY = (float)_initWaferCy;
                    using (SolidBrush brYellow = new SolidBrush(Color.Gold))
                    {
                        g.FillEllipse(brYellow, initX - 6, initY - 6, 12, 12);
                    }
                    #endregion

                    #region 4.当前实时晶圆中心(蓝色圆点，随大圆旋转) + 紫色晶圆外圈
                      currX = (float)currWaferX;
                      currY = (float)currWaferY;

                    #region 44.绘制基准大圆(淡紫色填充 + 紫色边框)
                    using (SolidBrush brushBig = new SolidBrush(Color.LightBlue))
                    using (Pen jingyuanWy = new Pen(Color.Blue, 1))
                    {
                        // 先填充
                        //g.FillEllipse(brushBig,
                        //       currX - (float)_waferR,
                        //    currY - (float)_waferR,
                        //    (float)_waferR * 2,
                        //    (float)_waferR * 2);

                        // 再画边框（可选）紫色                        // 自定义：短划线 3px， 空 3px
                        jingyuanWy.DashPattern = new float[] { 3, 3 };
                        g.DrawEllipse(jingyuanWy,
                               currX - (float)_waferR,
                            currY - (float)_waferR,
                            (float)_waferR * 2,
                            (float)_waferR * 2);
                    }
                    #endregion 44


                    #region 45.绘制8寸晶圆标准缺口 Notch (修复角度+坐标系)
                    // ===================== 8寸晶圆 Notch 标准尺寸 =====================


                    // 1. 缺口基础偏角(控件输入 角度°) + 晶圆整体旋转角度
                    notchBaseAngleDeg = (float)nudQuekouAngle.Value;
                    // 最终缺口朝向总角度（角度制）
                    totalNotchAngleDeg = notchBaseAngleDeg + (float)_totalAngle;
                    // 转为弧度，用于三角函数计算
                    totalNotchAngleRad = totalNotchAngleDeg * (float)Math.PI / 180.0f;

                    // 2. 修复三角函数：屏幕Y向下，使用标准极坐标公式
                    // 极坐标：x = r*cosθ , y = r*sinθ
                    notchTopLeft = GetNotchPoint(currX, currY, (float)_waferR, totalNotchAngleRad, (float)(-notchWidth / 2.0));
                    notchTopRight = GetNotchPoint(currX, currY, (float)_waferR, totalNotchAngleRad, (float)(notchWidth / 2.0));

                    // 缺口底部顶点
                    notchBottom = new PointF(
                      (float)(currX + (_waferR - notchDepth) * Math.Cos(totalNotchAngleRad)),
                      (float)(currY + (_waferR - notchDepth) * Math.Sin(totalNotchAngleRad))
                  );

                    // 用蓝色实线画缺口
                    using (Pen penNotch = new Pen(Color.Blue, 1))
                    {
                        g.DrawLine(penNotch, notchTopLeft, notchBottom);
                        g.DrawLine(penNotch, notchBottom, notchTopRight);
                    }
                    #endregion


                    //实时晶圆中心蓝圆点
                    using (SolidBrush brBlue = new SolidBrush(Color.Blue))
                    {
                        g.FillEllipse(brBlue, currX - 5, currY - 5, 10, 10);
                    }
                    #endregion

                    #region 5.绿色线阵直线
                    using (Pen penGreen = new Pen(Color.Green, 2))
                    {
                        g.DrawLine(penGreen, _lineLeft, _lineRight);
                    }
                    //标记线阵端点
                    using (SolidBrush brGreen = new SolidBrush(Color.Green))
                    {
                        g.FillEllipse(brGreen, _lineLeft.X - 3, _lineLeft.Y - 3, 6, 6);
                        g.FillEllipse(brGreen, _lineRight.X - 3, _lineRight.Y - 3, 6, 6);
                    }
                    #endregion
                }

                List<PointF> finalIntersectPoints = new List<PointF>();
                double lineX1 = _lineLeft.X, lineY1 = _lineLeft.Y;
                double lineX2 = _lineRight.X, lineY2 = _lineRight.Y;

                // 1. 线段与整圆求交
                PointF[] allCirclePts = GetLineCircleIntersect(
                    lineX1, lineY1, lineX2, lineY2, currWaferX, currWaferY, _waferR);

                // 缺口参数
      
                   notchBaseAngleDeg = (float)nudQuekouAngle.Value;
                  totalNotchAngleDeg = notchBaseAngleDeg + (float)_totalAngle;
                  totalNotchAngleRad = totalNotchAngleDeg * (float)Math.PI / 180.0f;
                double notchHalfAngleRad = Math.Atan((notchWidth / 2.0) / _waferR) + 0.02; // 余量扩大范围
                PointF waferCenter = new PointF(currX, currY);

                // 2. 过滤缺口圆弧上的圆交点
                foreach (var pt in allCirclePts)
                {
                    bool isInNotchArc = IsPointInNotchArcArea(pt, waferCenter, totalNotchAngleRad, notchHalfAngleRad);
                    if (!isInNotchArc)
                    {
                        finalIntersectPoints.Add(pt);
                    }
                }

                // 3. 计算缺口两条斜边交点
                  notchTopLeft = GetNotchPoint(currX, currY, (float)_waferR, totalNotchAngleRad, -notchWidth / 2);
                  notchTopRight = GetNotchPoint(currX, currY, (float)_waferR, totalNotchAngleRad, notchWidth / 2);
                  notchBottom = new PointF(
                    (float)(currX + (_waferR - notchDepth) * Math.Cos(totalNotchAngleRad)),
                    (float)(currY + (_waferR - notchDepth) * Math.Sin(totalNotchAngleRad))
                );

                var pLeft = LineLineIntersect(lineX1, lineY1, lineX2, lineY2,
                    notchTopLeft.X, notchTopLeft.Y, notchBottom.X, notchBottom.Y);
                var pRight = LineLineIntersect(lineX1, lineY1, lineX2, lineY2,
                    notchTopRight.X, notchTopRight.Y, notchBottom.X, notchBottom.Y);

                if (pLeft.HasValue) finalIntersectPoints.Add(pLeft.Value);
                if (pRight.HasValue) finalIntersectPoints.Add(pRight.Value);

                // 4. 浮点去重
                var uniquePoints = finalIntersectPoints
                    .Distinct(new PointFEqualityComparer())
                    .ToList();

                // ===================== 修正后筛选逻辑 =====================
                if (uniquePoints.Count > 1)
                {
                    var pointWithDist = uniquePoints.Select(p => new
                    {
                        Point = p,
                        Dist = Math.Sqrt(Math.Pow(p.X - currX, 2) + Math.Pow(p.Y - currY, 2))
                    }).ToList();

                    // 距离从小到大排序，取最近点
                    pointWithDist.Sort((a, b) => a.Dist.CompareTo(b.Dist));
                    uniquePoints = new List<PointF> { pointWithDist[0].Point };
                }
                // =========================================================

                // 5. 日志输出
                foreach (var p in uniquePoints)
                {
                    string logStr = $"总转角:{_totalAngle:F4}° | 轮廓交点 X={p.X:F4}, Y={p.Y:F4}";
                    Trace.WriteLine(logStr);
                    lstLog.Items.Add(logStr);
                }

                // 6. 绘制交点
                using (Graphics g = Graphics.FromImage(_bmp))
                using (SolidBrush brushPt = new SolidBrush(Color.Red))
                {
                    foreach (var p in uniquePoints)
                    {
                        g.FillEllipse(brushPt, p.X - 4, p.Y - 4, 8, 8);
                    }
                }
 
            
                pictureBox1.Refresh();
            }
        }

        #endregion 【核心定时器事件】每50ms更新一次晶圆位置并重绘
        #region 12
        /// <summary>
        /// 用于 PointF 的浮点精度比较器
        /// </summary>
        public class PointFEqualityComparer : IEqualityComparer<PointF>
        {
            private readonly double _eps = 1e-4;
            public bool Equals(PointF p1, PointF p2)
            {
                return Math.Abs(p1.X - p2.X) < _eps && Math.Abs(p1.Y - p2.Y) < _eps;
            }
            public int GetHashCode(PointF obj)
            {
                return (obj.X.ToString("F4") + "|" + obj.Y.ToString("F4")).GetHashCode();
            }
        }
        #endregion 12 


        #region   判断圆上一点是否在缺口切除的圆弧区域内

        /// <summary>
        /// 判断圆上一点是否在缺口切除的圆弧区域内
        /// </summary>
        private bool IsPointInNotchArcArea(PointF pt, PointF circleCenter, double notchAngleRad, double notchHalfAngleRad)
        {
            double dx = pt.X - circleCenter.X;
            double dy = pt.Y - circleCenter.Y;
            double pointRad = Math.Atan2(dy, dx);

            double angleMin = notchAngleRad - notchHalfAngleRad;
            double angleMax = notchAngleRad + notchHalfAngleRad;
            const double eps = 1e-3;
            bool inRange;

            if (angleMin <= angleMax)
            {
                inRange = (pointRad >= angleMin - eps) && (pointRad <= angleMax + eps);
            }
            else
            {
                inRange = (pointRad >= angleMin - eps) || (pointRad <= angleMax + eps);
            }
            return inRange;
        }
        #endregion   判断圆上一点是否在缺口切除的圆弧区域内

   


        // 计算缺口开口点坐标（修复屏幕坐标系）
        private PointF GetNotchPoint(float centerX, float centerY, float radius, float angleRad, float offsetDeg)
        {
            // 偏移角度 转 弧度
            double offsetRad = offsetDeg * Math.PI / 180.0;
            double finalRad = angleRad + offsetRad;

            // 标准极坐标（适配WinForm屏幕 Y轴向下）
            float x = centerX + (float)(radius * Math.Cos(finalRad));
            float y = centerY + (float)(radius * Math.Sin(finalRad));
            return new PointF(x, y);
        }
        /// <summary>
        /// 绘制标尺（包含X轴和Y轴刻度）
        /// </summary>
        private void DrawRuler(Graphics g)
        {
            int width = pictureBox1.Width;
            int height = pictureBox1.Height;

            // 保存原始变换
            var oldTransform = g.Transform;

            // 设置抗锯齿模式
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (Pen rulerPen = new Pen(_rulerColor, 1))
            using (Font font = new Font("Arial", 8))
            using (SolidBrush textBrush = new SolidBrush(_rulerTextColor))
            {
                // === 绘制X轴标尺（底部） ===
                int yXAxis = height - 20; // X轴位置（底部）
                g.DrawLine(rulerPen, 0, yXAxis, width, yXAxis);

                // X轴刻度
                for (int x = 0; x <= width; x += _rulerStep)
                {
                    // 绘制刻度线
                    g.DrawLine(rulerPen, x, yXAxis - 5, x, yXAxis + 2);

                    // 绘制刻度数字
                    string text = x.ToString();
                    SizeF textSize = g.MeasureString(text, font);
                    g.DrawString(text, font, textBrush, x - textSize.Width / 2, yXAxis + 2);
                }

                // X轴标签
                g.DrawString("X (像素)", font, textBrush, width - 30, yXAxis + 5);

                // === 绘制Y轴标尺（左侧） ===
                int xYAxis = 20; // Y轴位置（左侧）
                g.DrawLine(rulerPen, xYAxis, 0, xYAxis, height);

                // Y轴刻度
                for (int y = 0; y <= height; y += _rulerStep)
                {
                    // 绘制刻度线
                    g.DrawLine(rulerPen, xYAxis - 5, y, xYAxis + 2, y);

                    // 绘制刻度数字
                    string text = y.ToString();
                    SizeF textSize = g.MeasureString(text, font);
                    g.DrawString(text, font, textBrush, xYAxis - textSize.Width - 2, y - textSize.Height / 2);
                }

                // Y轴标签
                using (Matrix rotateMatrix = new Matrix())
                {
                    rotateMatrix.RotateAt(-90, new PointF(xYAxis - 15, 15));
                    g.Transform = rotateMatrix;
                    g.DrawString("Y (像素)", font, textBrush, -height + 15, xYAxis - 25);
                    g.Transform = oldTransform;
                }

                // === 可选：绘制参考网格线（半透明，辅助定位） ===
                using (Pen gridPen = new Pen(Color.FromArgb(50, _rulerColor), 1))
                {
                    gridPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

                    // 垂直网格线
                    for (int x = 0; x <= width; x += _rulerStep)
                    {
                        if (x != 0 && x != width) // 跳过边缘，避免与轴线重叠
                        {
                            g.DrawLine(gridPen, x, 0, x, height);
                        }
                    }

                    // 水平网格线
                    for (int y = 0; y <= height; y += _rulerStep)
                    {
                        if (y != 0 && y != height)
                        {
                            g.DrawLine(gridPen, 0, y, width, y);
                        }
                    }
                }
            }
        }


 

        #region 【核心工具函数】线段与圆求交点（标准数学公式，适配屏幕坐标系）
        // 线段 ↔ 圆 求交点（你原来的函数）
        private PointF[] GetLineCircleIntersect(double x1, double y1, double x2, double y2, double ox, double oy, double r)
        {
            List<PointF> res = new List<PointF>();
            double dx = x2 - x1;
            double dy = y2 - y1;
            double A = dx * dx + dy * dy;
            double B = 2 * (dx * (x1 - ox) + dy * (y1 - oy));
            double C = (x1 - ox) * (x1 - ox) + (y1 - oy) * (y1 - oy) - r * r;
            double delta = B * B - 4 * A * C;

            if (delta < 0) return Array.Empty<PointF>();
            double sq = Math.Sqrt(delta);
            double t1 = (-B - sq) / (2 * A);
            double t2 = (-B + sq) / (2 * A);

            if (t1 >= 0 && t1 <= 1)
                res.Add(new PointF((float)(x1 + t1 * dx), (float)(y1 + t1 * dy)));
            if (t2 >= 0 && t2 <= 1 && Math.Abs(t1 - t2) > 1e-6)
                res.Add(new PointF((float)(x1 + t2 * dx), (float)(y1 + t2 * dy)));

            return res.ToArray();
        }
        #endregion 【核心工具函数】线段与圆求交点（标准数学公式，适配屏幕坐标系）

        #region    线段 ↔ 线段 求交点（优化精度，仅返回两条线段范围内的交点）
        // 线段 ↔ 线段 求交点（优化精度，仅返回两条线段范围内的交点）
        private PointF? LineLineIntersect(double x1, double y1, double x2, double y2,
                                          double x3, double y3, double x4, double y4)
        {
            double denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
            // 两直线平行，无交点
            if (Math.Abs(denom) < 1e-6)
                return null;

            double ua = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / denom;
            double ub = ((x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)) / denom;

            // 严格限制：交点必须同时在【两条原始线段】上，排除延长线
            if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
            {
                return new PointF((float)(x1 + ua * (x2 - x1)), (float)(y1 + ua * (y2 - y1)));
            }
            return null;
        }
        #endregion    线段 ↔ 线段 求交点（优化精度，仅返回两条线段范围内的交点）


        //可选：添加一个按钮控制标尺显示/隐藏
        private void btnToggleRuler_Click(object sender, EventArgs e)
        {
            _showRuler = !_showRuler;
            (sender as Button).Text = _showRuler ? "隐藏标尺" : "显示标尺";
            //强制刷新
            lock (_lock)
            {
                _timer.Enabled = false;
                _totalAngle = 0;
                UpdateParas();
                new Thread(() =>
                {
                    Invoke(() => _timer.Enabled = true);
                }).Start();
            }
        }

        //窗体销毁释放位图
        protected override void Dispose(bool disposing)
        {
            _bmp?.Dispose();
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 可以在Form1上添加一个按钮来控制标尺（可选）
            // 例如：Button btnRuler = new Button() { Text = "隐藏标尺", Location = new Point(10, 10), Size = new Size(80, 30) };
            // btnRuler.Click += btnToggleRuler_Click;
            // this.Controls.Add(btnRuler);
            // 离屏绘图
            using (Graphics g = Graphics.FromImage(_bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                {
                    DrawRuler(g);
                }
            }
        }

        private void btn切换定时器使能_Click(object sender, EventArgs e)
        {
            _timer.Enabled = !_timer.Enabled;
            (sender as Button).Text = _timer.Enabled ? "timer停止" : "timer开始";
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            _timer.Enabled = false;
            this.Close();
        }
    }
}
