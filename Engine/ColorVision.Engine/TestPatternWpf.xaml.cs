using ColorVision.Engine;
using ColorVision.ImageEditor;
using ColorVision.UI.Menus;
using OpenCvSharp.WpfExtensions;
using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Engine
{
    public class ExportTestPatternWpf : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "图卡生成工具";
        public override int Order => 3;

        public override void Execute()
        {
            new TestPatternWpf() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
    /// <summary>
    /// TestPatternWpf.xaml 的交互逻辑
    /// </summary>
    public partial class TestPatternWpf : Window
    {
        private OpenCvSharp.Mat currentMat;
        private readonly string[] patternTypes = { "纯色", "棋盘格", "点阵", "十字图卡", "隔行点亮"，"MTF图卡","畸变图卡","双目融合图卡" };
        private readonly string[] imageFormats = { "png", "jpg", "bmp" };
        private readonly (string, int, int)[] commonResolutions =
        {
            ("1920x1080",1920,1080), ("1280x720",1280,720), ("1024x768",1024,768),
            ("800x600",800,600), ("640x480",640,480), ("自定义",0,0)
        };
        private System.Windows.Media.Color mainColor = System.Windows.Media.Colors.Black;
        private System.Windows.Media.Color altColor = System.Windows.Media.Colors.White;

        public TestPatternWpf()
        {
            InitializeComponent();

            cmbPattern.ItemsSource = patternTypes;
            cmbPattern.SelectedIndex = 0;
            cmbFormat.ItemsSource = imageFormats;
            cmbFormat.SelectedIndex = 0;
            cmbResolution.ItemsSource = Array.ConvertAll(commonResolutions, t => t.Item1);
            cmbResolution.SelectedIndex = 4; // 默认640x480
            rectMainColor.Fill = new SolidColorBrush(mainColor);
            rectAltColor.Fill = new SolidColorBrush(altColor);
            cmbResolution.SelectionChanged += CmbResolution_SelectionChanged;
        }

        private void CmbResolution_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int idx = cmbResolution.SelectedIndex;
            if (idx >= 0 && idx < commonResolutions.Length - 1)
            {
                txtWidth.Text = commonResolutions[idx].Item2.ToString();
                txtHeight.Text = commonResolutions[idx].Item3.ToString();
                txtWidth.IsEnabled = false;
                txtHeight.IsEnabled = false;
            }
            else // 自定义
            {
                txtWidth.IsEnabled = true;
                txtHeight.IsEnabled = true;
            }
        }

        private void BtnPickMainColor_Click(object sender, RoutedEventArgs e)
        {
            var ColorPicker1 = new HandyControl.Controls.ColorPicker();
            ColorPicker1.SelectedBrush = (SolidColorBrush)rectMainColor.Fill;
            ColorPicker1.SelectedColorChanged += (s, e) =>
            {
                mainColor = ColorPicker1.SelectedBrush.Color;
                rectMainColor.Fill = ColorPicker1.SelectedBrush;
            };
            Window window = new Window() { Owner =Application.Current.GetActiveWindow(),WindowStartupLocation = WindowStartupLocation.CenterOwner,Content = ColorPicker1 ,Width =250, Height =400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
                mainColor = ColorPicker1.SelectedBrush.Color;
                rectMainColor.Fill = new SolidColorBrush(mainColor);
                window.Close();
            };
            window.Closed += (s, e) =>
            {
                ColorPicker1.Dispose();
            };
            window.Show();     
        }

        private void BtnPickAltColor_Click(object sender, RoutedEventArgs e)
        {
            var ColorPicker1 = new HandyControl.Controls.ColorPicker();
            ColorPicker1.SelectedBrush = (SolidColorBrush)rectAltColor.Fill;
            ColorPicker1.SelectedColorChanged += (s, e) =>
            {
                mainColor = ColorPicker1.SelectedBrush.Color;
                rectAltColor.Fill = ColorPicker1.SelectedBrush;
            };
            Window window = new Window() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = ColorPicker1, Width = 250, Height = 400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
                mainColor = ColorPicker1.SelectedBrush.Color;
                rectAltColor.Fill = new SolidColorBrush(mainColor);
                window.Close();
            };
            window.Closed += (s, e) =>
            {
                ColorPicker1.Dispose();
            };
            window.Show();
        }

        private OpenCvSharp.Scalar ToScalar(System.Windows.Media.Color color)
        {
            return new OpenCvSharp.Scalar(color.B, color.G, color.R, color.A);
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            int w = int.TryParse(txtWidth.Text, out int tw) ? tw : 640;
            int h = int.TryParse(txtHeight.Text, out int th) ? th : 480;
            string pattern = patternTypes[cmbPattern.SelectedIndex];

            currentMat?.Dispose();
            OpenCvSharp.Scalar main = ToScalar(mainColor);
            OpenCvSharp.Scalar alt = ToScalar(altColor);

            switch (pattern)
            {
                case "棋盘格":
                    currentMat = GenerateCheckerboard(w, h, 8, 8, main, alt);
                    break;
                case "点阵":
                    currentMat = GenerateDotPattern(w, h, 30, 8, main, alt);
                    break;
                case "十字":
                    currentMat = GenerateCrossPattern(w, h, main, alt);
                    break;
                case "隔行点亮":
                    currentMat = GenerateInterlacedPattern(w, h, main, alt);
                    break;
                case "纯色":
                    currentMat = GenerateSolidPattern(w, h, main);
                    break;
                default:
                    currentMat = new OpenCvSharp.Mat(h, w, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.Gray);
                    break;
            }
            imgDisplay.ImageShow.Source = currentMat.ToBitmapSource();
            imgDisplay.Zoombox1.ZoomUniform();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (currentMat == null)
            {
                System.Windows.MessageBox.Show("请先生成图案");
                return;
            }
            string ext = imageFormats[cmbFormat.SelectedIndex];
            string pattern = patternTypes[cmbPattern.SelectedIndex];
            string filename = $"{pattern}_{txtWidth.Text}x{txtHeight.Text}_{DateTime.Now:yyyyMMddHHmmss}.{ext}";
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp",
                DefaultExt = ext,
                FileName = filename
            };
            if (dlg.ShowDialog() == true)
            {
                currentMat.SaveImage(dlg.FileName);
                System.Windows.MessageBox.Show("保存成功: " + dlg.FileName);
            }
        }

        // 3. 添加方法
        private OpenCvSharp.Mat GenerateSolidPattern(int w, int h, OpenCvSharp.Scalar color)
        {
            return new OpenCvSharp.Mat(h, w, OpenCvSharp.MatType.CV_8UC3, color);
        }

        // 棋盘格
        private OpenCvSharp.Mat GenerateCheckerboard(int w, int h, int gridX, int gridY, OpenCvSharp.Scalar color1, OpenCvSharp.Scalar color2)
        {
            var mat = new OpenCvSharp.Mat(h, w, OpenCvSharp.MatType.CV_8UC3, color1);
            int cellW = w / gridX;
            int cellH = h / gridY;
            for (int y = 0; y < gridY; y++)
                for (int x = 0; x < gridX; x++)
                    if ((x + y) % 2 == 1)
                        OpenCvSharp.Cv2.Rectangle(mat, new OpenCvSharp.Rect(x * cellW, y * cellH, cellW, cellH), color2, -1);
            return mat;
        }

        // 点阵
        private OpenCvSharp.Mat GenerateDotPattern(int w, int h, int spacing, int radius, OpenCvSharp.Scalar dotColor, OpenCvSharp.Scalar bgColor)
        {
            var mat = new OpenCvSharp.Mat(h, w, OpenCvSharp.MatType.CV_8UC3, bgColor);
            for (int y = spacing / 2; y < h; y += spacing)
                for (int x = spacing / 2; x < w; x += spacing)
                    OpenCvSharp.Cv2.Circle(mat, new OpenCvSharp.Point(x, y), radius, dotColor, -1);
            return mat;
        }

        // 十字
        private OpenCvSharp.Mat GenerateCrossPattern(int w, int h, OpenCvSharp.Scalar lineColor, OpenCvSharp.Scalar bgColor)
        {
            var mat = new OpenCvSharp.Mat(h, w, OpenCvSharp.MatType.CV_8UC3, bgColor);
            OpenCvSharp.Cv2.Line(mat, new OpenCvSharp.Point(0, h / 2), new OpenCvSharp.Point(w, h / 2), lineColor, 3);
            OpenCvSharp.Cv2.Line(mat, new OpenCvSharp.Point(w / 2, 0), new OpenCvSharp.Point(w / 2, h), lineColor, 3);
            return mat;
        }

        // 隔行点亮
        private OpenCvSharp.Mat GenerateInterlacedPattern(int w, int h, OpenCvSharp.Scalar lineColor, OpenCvSharp.Scalar bgColor)
        {
            var mat = new OpenCvSharp.Mat(h, w, OpenCvSharp.MatType.CV_8UC3, bgColor);
            for (int y = 0; y < h; y += 2)
                OpenCvSharp.Cv2.Line(mat, new OpenCvSharp.Point(0, y), new OpenCvSharp.Point(w, y), lineColor, 1);
            return mat;
        }
        ImageView imgDisplay { get; set; }
        private void Window_Initialized(object sender, EventArgs e)
        { 
            imgDisplay = new ImageView();
            DisplayGrid.Children.Add(imgDisplay);
            this.Closed += (s, e) =>
            {
                currentMat?.Dispose();
                imgDisplay?.Dispose();
            };
        }
    }
}
