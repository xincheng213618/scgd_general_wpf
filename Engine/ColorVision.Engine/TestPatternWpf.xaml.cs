using ColorVision.Common.MVVM;
using ColorVision.Engine;
using ColorVision.Engine.Pattern;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Menus;
using OpenCvSharp.WpfExtensions;
using System;
using System.Windows;
using System.Windows.Controls;
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

    public class TestPatternWpfConfig :ViewModelBase,IConfig
    {
        public static TestPatternWpfConfig Instance => ConfigService.Instance.GetRequiredService<TestPatternWpfConfig>();

        public int Width { get => _Width; set { _Width = value; NotifyPropertyChanged(); } }
        private int _Width = 640;

        public int Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private int _Height = 480;

        public int Spacing { get => _Spacing; set { _Spacing = value; NotifyPropertyChanged(); } }
        private int _Spacing = 20;
        public int Radius { get => _Radius; set { _Radius = value; NotifyPropertyChanged(); } }
        private int _Radius = 3;
    }


    /// <summary>
    /// TestPatternWpf.xaml 的交互逻辑
    /// </summary>
    public partial class TestPatternWpf : Window,IDisposable
    {
        private OpenCvSharp.Mat currentMat;
        private readonly string[] patternTypes = { "纯色", "棋盘格", "点阵","SFR" ,"9点","十字图卡","圆环" ,"隔行点亮" };
        private readonly string[] imageFormats = {"bmp" ,"tif","png", "jpg"};
        private readonly (string, int, int)[] commonResolutions =
        {
            ("3840x2160",3840,2160), ("1920x1080",1920,1080), ("1280x720",1280,720), ("1024x768",1024,768),
            ("800x600",800,600), ("640x480",640,480), ("自定义",0,0)
        };
        private System.Windows.Media.Color mainColor = System.Windows.Media.Colors.Black;
        private System.Windows.Media.Color altColor = System.Windows.Media.Colors.White;
        static TestPatternWpfConfig Config => TestPatternWpfConfig.Instance;

        public TestPatternWpf()
        {
            InitializeComponent();
        }

        ImageView imgDisplay { get; set; }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = TestPatternWpfConfig.Instance;
            imgDisplay = new ImageView();
            DisplayGrid.Children.Add(imgDisplay);
            this.Closed += (s, e) => Dispose();
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
                Config.Width = commonResolutions[idx].Item2;
                Config.Height = commonResolutions[idx].Item3;
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
            string pattern = patternTypes[cmbPattern.SelectedIndex];
            currentMat?.Dispose();
            OpenCvSharp.Scalar main = ToScalar(mainColor);
            OpenCvSharp.Scalar alt = ToScalar(altColor);

            switch (pattern)
            {
                case "棋盘格":
                    currentMat = GenerateCheckerboard(Config.Width, Config.Height, 8, 8, main, alt);
                    break;
                case "点阵":
                    currentMat = DotPattern.Generate(Config.Width, Config.Height, Config.Spacing, Config.Radius, main, alt);
                    break;
                case "十字":
                    currentMat = GenerateCrossPattern(Config.Width, Config.Height, main, alt);
                    break;
                case "隔行点亮":
                    currentMat = GenerateInterlacedPattern(Config.Width, Config.Height, main, alt);
                    break;
                case "纯色":
                    currentMat = GenerateSolidPattern(Config.Width, Config.Height, main);
                    break;
                case "SFR":
                    currentMat = SFRPattern.Generate(Config.Width, Config.Height);
                    break;
                case "9点":
                    currentMat = NineDotPattern.Generate(Config.Width, Config.Height, Config.Radius, alt, main);
                    break;
                case "圆环":
                    currentMat = RingPattern.Generate(Config.Width, Config.Height, Config.Radius);
                    break;
                default:
                    currentMat = new OpenCvSharp.Mat(Config.Width, Config.Height,OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.Gray);
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


        private void BtnPickMainColorSet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string tag = button.Tag.ToString();
                if (tag == "R")
                {
                    mainColor = Brushes.Red.Color;
                    rectMainColor.Fill = Brushes.Red;
                }
                if (tag == "G")
                {
                    mainColor = Brushes.Green.Color;
                    rectMainColor.Fill = Brushes.Green;
                }
                if (tag == "B")
                {
                    mainColor = Brushes.Blue.Color;
                    rectMainColor.Fill = Brushes.Blue;
                }
                if (tag == "W")
                {
                    mainColor = Brushes.White.Color;
                    rectMainColor.Fill = Brushes.White;
                }
                if (tag == "K")
                {
                    mainColor = Brushes.Black.Color;
                    rectMainColor.Fill = Brushes.Black;
                }
            }
        }

        private void BtnPickAltColorSet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string tag = button.Tag.ToString();
                if (tag == "R")
                {
                    altColor = Brushes.Red.Color;
                    rectAltColor.Fill = Brushes.Red;
                }
                if (tag == "G")
                {
                    altColor = Brushes.Green.Color;
                    rectAltColor.Fill = Brushes.Green;
                }
                if (tag == "B")
                {
                    altColor = Brushes.Blue.Color;
                    rectAltColor.Fill = Brushes.Blue;
                }
                if (tag == "W")
                {
                    altColor = Brushes.White.Color;
                    rectAltColor.Fill = Brushes.White;
                }
                if (tag == "K")
                {
                    altColor = Brushes.Black.Color;
                    rectAltColor.Fill = Brushes.Black;
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            currentMat?.Dispose();
            imgDisplay?.Dispose();
        }
    }
}
