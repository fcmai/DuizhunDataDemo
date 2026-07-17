using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DuizhunDataInverse
{
    public partial class Form1 : Form
    {
        private TextBox txtFile;
        private NumericUpDown nudCx, nudCy;
        private CheckBox chkUnknown, chkNotch;
        private Button btnOpen, btnRun, btnSelfTest;
        private TextBox txtResult;
        private PictureBox picPlot;
        private Label lblStatus;
        private Bitmap _bmp;

        public Form1()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "对准数据反推工具（偏心距 / 晶圆尺寸）";
            this.ClientSize = new Size(980, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei", 9F);

            // ---- 输入区 ----
            btnOpen = new Button { Text = "选择CSV文件", Location = new Point(12, 12), Size = new Size(120, 30) };
            btnOpen.Click += BtnOpen_Click;

            txtFile = new TextBox { Location = new Point(140, 14), Size = new Size(330, 26), ReadOnly = true };

            var lblCx = new Label { Text = "底盘圆心 X：", Location = new Point(12, 52), AutoSize = true };
            nudCx = new NumericUpDown { Location = new Point(92, 50), Size = new Size(90, 23), DecimalPlaces = 2, Increment = 1, Minimum = -100000, Maximum = 100000, Value = 250 };
            var lblCy = new Label { Text = "Y：", Location = new Point(196, 52), AutoSize = true };
            nudCy = new NumericUpDown { Location = new Point(224, 50), Size = new Size(90, 23), DecimalPlaces = 2, Increment = 1, Minimum = -100000, Maximum = 100000, Value = 250 };

            chkUnknown = new CheckBox { Text = "底盘圆心未知（自动反推，需至少一整圈数据）", Location = new Point(12, 84), Size = new Size(420, 22), AutoSize = true };
            chkUnknown.CheckedChanged += (s, e) => { nudCx.Enabled = nudCy.Enabled = !chkUnknown.Checked; };

            chkNotch = new CheckBox { Text = "剔除缺口离群点（推荐）", Location = new Point(12, 110), Size = new Size(300, 22), Checked = true, AutoSize = true };

            btnRun = new Button { Text = "运行反推", Location = new Point(12, 140), Size = new Size(120, 34), BackColor = Color.LightSteelBlue };
            btnRun.Click += BtnRun_Click;

            btnSelfTest = new Button { Text = "生成合成数据并自测", Location = new Point(150, 140), Size = new Size(190, 34) };
            btnSelfTest.Click += BtnSelfTest_Click;

            // ---- 结果文本 ----
            txtResult = new TextBox
            {
                Location = new Point(12, 188),
                Size = new Size(348, 460),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9.5F)
            };

            // ---- 绘图区 ----
            picPlot = new PictureBox
            {
                Location = new Point(376, 12),
                Size = new Size(584, 460),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            lblStatus = new Label
            {
                Location = new Point(376, 482),
                Size = new Size(584, 160),
                AutoSize = false,
                Text = "说明：\r\n" +
                       "1) 用原工程「保存CSV」或自动保存的 csv 作为输入（含 总转角 / 轮廓交点）。\r\n" +
                       "2) 已知底盘圆心时，程序把每个点去旋转后做圆拟合——圆心即偏心向量 e，半径即晶圆尺寸 R。\r\n" +
                       "3) 偏离量任意时刻恒定 = |e|；t 时刻偏离向量 = 旋转 φ(t) 后的 e。\r\n" +
                       "4) 「生成合成数据并自测」会用已知真值造数据并比对反推误差，验证算法正确性。",
                Font = new Font("Microsoft YaHei", 9F)
            };

            this.Controls.AddRange(new Control[] { btnOpen, txtFile, lblCx, nudCx, lblCy, nudCy,
                chkUnknown, chkNotch, btnRun, btnSelfTest, txtResult, picPlot, lblStatus });
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                Title = "选择原工程导出的 CSV"
            };
            if (dlg.ShowDialog() == DialogResult.OK) txtFile.Text = dlg.FileName;
        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            string f = txtFile.Text.Trim();
            if (!File.Exists(f)) { MessageBox.Show("请先选择 CSV 文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            var data = InverseSolver.ParseCsv(f);
            if (data.Count < 10) { MessageBox.Show($"解析到的有效数据点不足：{data.Count}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            double? ocx = chkUnknown.Checked ? (double?)null : (double)nudCx.Value;
            double? ocy = chkUnknown.Checked ? (double?)null : (double)nudCy.Value;

            var res = InverseSolver.Solve(data, ocx, ocy, chkNotch.Checked);
            var sb = new StringBuilder();
            AppendResult(sb, res);
            txtResult.Text = sb.ToString();
            DrawPlot(res);
        }

        private void BtnSelfTest_Click(object sender, EventArgs e)
        {
            double Ocx = 250, Ocy = 250, ex = 18, ey = -7, R = 35;
            double eTrue = Math.Sqrt(ex * ex + ey * ey);
            double angTrue = Math.Atan2(ey, ex) * 180.0 / Math.PI;

            string f = Path.Combine(Path.GetTempPath(), "synthetic_inverse_test.csv");
            InverseSolver.GenerateSynthetic(f, Ocx, Ocy, ex, ey, R, 3, 120, 0.3);
            txtFile.Text = f;

            var data = InverseSolver.ParseCsv(f);
            var sb = new StringBuilder();
            sb.AppendLine($"【合成自测】点数={data.Count}  真值: e={eTrue:F4}, R={R:F4}, 偏角={angTrue:F2}°");
            sb.AppendLine();

            var r1 = InverseSolver.Solve(data, Ocx, Ocy, true);
            sb.AppendLine("---- 已知底盘圆心反推 ----");
            AppendResult(sb, r1);
            sb.AppendLine($"▶ 误差: |e|偏差={Math.Abs(r1.Eccentricity - eTrue):F4}, R偏差={Math.Abs(r1.WaferRadius - R):F4}");
            sb.AppendLine();

            var r2 = InverseSolver.Solve(data, null, null, true);
            sb.AppendLine("---- 底盘圆心未知(自动反推) ----");
            AppendResult(sb, r2);
            sb.AppendLine($"▶ 圆心误差: ΔO=({Math.Abs(r2.Ocx - Ocx):F3}, {Math.Abs(r2.Ocy - Ocy):F3}), |e|偏差={Math.Abs(r2.Eccentricity - eTrue):F4}, R偏差={Math.Abs(r2.WaferRadius - R):F4}");
            sb.AppendLine();

            txtResult.Text = sb.ToString();
            DrawPlot(r1);
        }

        private static void AppendResult(StringBuilder sb, InverseSolver.SolveResult res)
        {
            if (!res.Success) { sb.AppendLine("失败: " + res.Message); return; }
            sb.AppendLine($"偏心距 |e|       = {res.Eccentricity:F4}");
            sb.AppendLine($"偏心方向角 β    = {res.EccentricAngleDeg:F2}°  (φ=0 时相对底盘圆心)");
            sb.AppendLine($"晶圆半径 R       = {res.WaferRadius:F4}");
            sb.AppendLine($"偏心向量 e       = ({res.CenterX:F4}, {res.CenterY:F4})");
            sb.AppendLine($"拟合残差 RMS     = {res.RmsResidual:F4}");
            sb.AppendLine($"使用点数/剔除    = {res.UsedPoints} / {res.RejectedPoints}");
            sb.AppendLine($"角度覆盖         = {res.AngleMin:F1}° ~ {res.AngleMax:F1}°  (约 {res.Revolutions:F2} 圈)");
            sb.AppendLine($"底盘圆心 O       = ({res.Ocx:F2}, {res.Ocy:F2})");
        }

        private void DrawPlot(InverseSolver.SolveResult res)
        {
            if (_bmp != null) { picPlot.Image = null; _bmp.Dispose(); }
            int w = picPlot.Width, h = picPlot.Height;
            _bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(_bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                if (res == null || !res.Success || res.DeRotatedPoints == null || res.DeRotatedPoints.Count == 0)
                {
                    g.DrawString("无数据可绘制", new Font("Microsoft YaHei", 12), Brushes.Gray, 20, 20);
                    picPlot.Image = _bmp; return;
                }

                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (var p in res.DeRotatedPoints)
                {
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                }
                minX = Math.Min(minX, (float)(res.FitCircleCx - res.FitCircleR));
                maxX = Math.Max(maxX, (float)(res.FitCircleCx + res.FitCircleR));
                minY = Math.Min(minY, (float)(res.FitCircleCy - res.FitCircleR));
                maxY = Math.Max(maxY, (float)(res.FitCircleCy + res.FitCircleR));

                float margin = 24;
                float sx = (w - 2 * margin) / Math.Max(1e-6f, (maxX - minX));
                float sy = (h - 2 * margin) / Math.Max(1e-6f, (maxY - minY));
                float s = Math.Min(sx, sy);
                float ToX(float x) => margin + (x - minX) * s;
                float ToY(float y) => margin + (y - minY) * s;

                // 去旋转坐标系原点 O（底盘圆心在去旋转系中的位置，应为 0,0）
                var oScreen = new PointF(ToX(0), ToY(0));
                g.DrawEllipse(Pens.LightGray, oScreen.X - 4, oScreen.Y - 4, 8, 8);
                g.DrawString("O(底盘圆心)", new Font("Microsoft YaHei", 8), Brushes.Gray, oScreen.X + 6, oScreen.Y);

                // 数据点
                using (var br = new SolidBrush(Color.Red))
                    foreach (var p in res.DeRotatedPoints)
                        g.FillEllipse(br, ToX(p.X) - 1.5f, ToY(p.Y) - 1.5f, 3, 3);

                // 拟合圆（蓝色）
                float ccx = ToX((float)res.FitCircleCx), ccy = ToY((float)res.FitCircleCy);
                float cr = (float)(res.FitCircleR * s);
                g.DrawEllipse(new Pen(Color.Blue, 1.5f), ccx - cr, ccy - cr, cr * 2, cr * 2);

                // 圆心 e（绿色）
                g.FillEllipse(Brushes.Green, ccx - 4, ccy - 4, 8, 8);
                g.DrawString($"e (|e|={res.Eccentricity:F2})", new Font("Microsoft YaHei", 8), Brushes.Green, ccx + 6, ccy);

                g.DrawString("去旋转坐标系：点群应贴合蓝色圆，圆心即偏心向量 e", new Font("Microsoft YaHei", 8), Brushes.Black, 10, 10);
            }
            picPlot.Image = _bmp;
        }

        protected override void Dispose(bool disposing)
        {
            _bmp?.Dispose();
            base.Dispose(disposing);
        }
    }
}
