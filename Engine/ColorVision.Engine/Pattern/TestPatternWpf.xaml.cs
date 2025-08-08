using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Pattern
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
    }

    public class PatternMeta : ViewModelBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public IPattern Pattern { get; set; } 
    }

    public class PatternManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PatternManager));
        private static PatternManager _instance;
        private static readonly object _locker = new();
        public static PatternManager GetInstance() { lock (_locker) { _instance ??= new PatternManager(); return _instance; } }

        public List<PatternMeta> Patterns { get; set; } = new List<PatternMeta>();
        private PatternManager()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IPattern).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        var displayName = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
                        var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                        var category = type.GetCustomAttribute<CategoryAttribute>()?.Category ?? "";

                        IPattern pattern = (IPattern)Activator.CreateInstance(type);
                        if (pattern != null)
                        {
                            var patternMeta = new PatternMeta
                            {
                                Name = displayName,
                                Description = description,
                                Category = category,
                                Pattern = pattern
                            };
                            Patterns.Add(patternMeta);
                            log.Info($"已加载图案生成器: {type.FullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"加载图案生成器失败: {type.FullName}", ex);
                    }

                }
            }
        }
    }


    /// <summary>
    /// TestPatternWpf.xaml 的交互逻辑
    /// </summary>
    public partial class TestPatternWpf : Window,IDisposable
    {
        private OpenCvSharp.Mat currentMat;

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

        public static List<PatternMeta> Patterns => PatternManager.GetInstance().Patterns;

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = TestPatternWpfConfig.Instance;
            imgDisplay = new ImageView();
            //这里最好实现成不模糊的样子
            RenderOptions.SetBitmapScalingMode(imgDisplay.ImageShow, BitmapScalingMode.NearestNeighbor);

            DisplayGrid.Children.Add(imgDisplay);
            this.Closed += (s, e) => Dispose();
            cmbFormat.ItemsSource = imageFormats;
            cmbFormat.SelectedIndex = 0;
            cmbResolution.ItemsSource = Array.ConvertAll(commonResolutions, t => t.Item1);
            cmbResolution.SelectedIndex = 4; // 默认640x480

            cmbResolution.SelectionChanged += CmbResolution_SelectionChanged;

            cmbPattern1.SelectionChanged += (s, e) =>
            {
                if (cmbPattern1.SelectedItem is PatternMeta selectedPattern)
                {
                    PatternEditorGrid.Children.Clear();
                    if (selectedPattern.Pattern is IPattern pattern)
                    {
                        PatternEditorGrid.Children.Add(pattern.GetPatternEditor());
                    }
                }
            };
            cmbPattern1.ItemsSource = Patterns;
            cmbPattern1.SelectedIndex = 0;

        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            new PropertyEditorWindow(Patterns[cmbPattern1.SelectedIndex].Pattern.GetConfig()).ShowDialog();
        }

        private void PatternGen_Click(object sender, RoutedEventArgs e)
        {
            currentMat?.Dispose();

            currentMat = Patterns[cmbPattern1.SelectedIndex].Pattern.Gen(Config.Height,Config.Width);

            imgDisplay.ImageShow.Source = currentMat.ToBitmapSource();
            imgDisplay.Zoombox1.ZoomUniform();
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



        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (currentMat == null)
            {
                System.Windows.MessageBox.Show("请先生成图案");
                return;
            }
            string ext = imageFormats[cmbFormat.SelectedIndex];
            string pattern = Patterns[cmbPattern1.SelectedIndex].Name;
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

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            currentMat?.Dispose();
            imgDisplay?.Dispose();
        }


    }
}
