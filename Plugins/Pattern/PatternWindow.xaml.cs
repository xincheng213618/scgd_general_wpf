using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Pattern
{
    public class ExportTestPatternWpf : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "图卡生成工具";
        public override int Order => 3;

        public override void Execute()
        {
            new PatternWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    public class PatternWindowConfig :ViewModelBase,IConfig
    {
        public static PatternWindowConfig Instance => ConfigService.Instance.GetRequiredService<PatternWindowConfig>();

        public int Width { get => _Width; set { _Width = value; OnPropertyChanged(); } }
        private int _Width = 640;

        public int Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private int _Height = 480;
    }

    public enum PatternFormat
    {
        bmp,
        tif,
        png,
        jpg
    }

    /// <summary>
    /// PatternWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PatternWindow : Window,IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PatternManager));

        private OpenCvSharp.Mat currentMat;

        private readonly (string, int, int)[] commonResolutions =
        {
            ("3840x2160",3840,2160), ("1920x1080",1920,1080), ("1280x720",1280,720), ("1024x768",1024,768),
            ("800x600",800,600), ("640x480",640,480), ("自定义",0,0)
        };
        static PatternWindowConfig Config => PatternWindowConfig.Instance;

        static PatternManager PatternManager => PatternManager.GetInstance();
        public static List<PatternMeta> Patterns => PatternManager.Patterns;

        public static ObservableCollection<TemplatePatternFile> TemplatePatternFiles => PatternManager.TemplatePatternFiles;
        public PatternMeta PatternMeta { get; set; }
        ImageView imgDisplay { get; set; }

        public PatternWindow()
        {
            InitializeComponent();
            ListViewPattern.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (s, e) =>
            {
                var selectedFilePath = PatternManager.TemplatePatternFiles[ListViewPattern.SelectedIndex].FilePath;
                StringCollection paths = new StringCollection();
                paths.Add(selectedFilePath);
                Clipboard.SetFileDropList(paths);

            }, (s, e) => { e.CanExecute = ListViewPattern.SelectedIndex > -1; }));

            ListViewPattern.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) =>
            {
                var index = PatternManager.TemplatePatternFiles[ListViewPattern.SelectedIndex];
                PatternManager.TemplatePatternFiles.RemoveAt(ListViewPattern.SelectedIndex);
                File.Delete(index.FilePath);
            }, (s, e) => { e.CanExecute = ListViewPattern.SelectedIndex > -1; }));

            ListViewPattern.CommandBindings.Add(new CommandBinding(Commands.ReName, (s, e) => ReName(), (s, e) => e.CanExecute = ListViewPattern.SelectedIndex > -1));

        }

        public void ReName()
        {
            if (ListViewPattern.SelectedIndex > -1 && TemplatePatternFiles[ListViewPattern.SelectedIndex] is TemplatePatternFile templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = PatternManager;
            ListViewPattern.ItemsSource = PatternManager.GetInstance().TemplatePatternFiles;
            ResolutionStackPanel.DataContext = PatternWindowConfig.Instance;
            imgDisplay = new ImageView();
            //这里最好实现成不模糊的样子
            RenderOptions.SetBitmapScalingMode(imgDisplay.ImageShow, BitmapScalingMode.NearestNeighbor);

            DisplayGrid.Children.Add(imgDisplay);
            this.Closed += (s, e) => Dispose();
            cmbFormat.ItemsSource = Enum.GetValues(typeof(PatternFormat));
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
                        PatternMeta = selectedPattern;
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
            string ext = PatternManager.Config.PatternFormat.ToString();
            string json = Path.Combine(PatternManager.GetInstance().PatternPath, Config.Width + "x" + Config.Height + "_" + PatternMeta.Pattern.GetTemplateName() + $".{PatternManager.Config.PatternFormat}");
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp",
                DefaultExt = ext,
                FileName = json
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

        public void SetTemplatePattern(string templatePath)
        {
            try
            {
                string pattern = File.ReadAllText(templatePath);
                TemplatePattern templatePattern = JsonConvert.DeserializeObject<TemplatePattern>(pattern);
                PatternMeta = Patterns.Find(p => p.Name == templatePattern.PatternName);
                Config.Width = templatePattern.PatternWindowConfig.Width;
                Config.Height = templatePattern.PatternWindowConfig.Height;

                PatternMeta.Pattern.SetConfig(templatePattern.Config);

                PatternEditorGrid.Children.Clear();
                PatternEditorGrid.Children.Add(PatternMeta.Pattern.GetPatternEditor());

                cmbPattern1.SelectedItem = PatternMeta;


                if (PatternManager.Config.IsSwitchCreate)
                {
                    currentMat?.Dispose();

                    currentMat = PatternMeta.Pattern.Gen(Config.Height, Config.Width);

                    imgDisplay.ImageShow.Source = currentMat.ToBitmapSource();
                    imgDisplay.Zoombox1.ZoomUniform();
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void TempSave_Click(object sender, RoutedEventArgs e)
        {
            string json = Path.Combine(PatternManager.GetInstance().PatternPath, Config.Width + "x" + Config.Height + "_" + PatternMeta.Pattern.GetTemplateName() + ".json");
            if (File.Exists(json))
            {
               if (MessageBox.Show(Application.Current.GetActiveWindow(), "是否替换模板", "Pattern", MessageBoxButton.YesNo) == MessageBoxResult.No)
                    return;
                if (PatternManager.GetInstance().TemplatePatternFiles.FirstOrDefault(a => a.FilePath == json) is TemplatePatternFile templatePatternFile)
                {
                    PatternManager.GetInstance().TemplatePatternFiles.Remove(templatePatternFile);
                }
            }
          
            TemplatePattern templatePattern = new TemplatePattern();
            templatePattern.PatternName = PatternMeta.Name;
            templatePattern.PatternWindowConfig = Config;
            templatePattern.Config = Patterns[cmbPattern1.SelectedIndex].Pattern.GetConfig().ToJsonN();
            templatePattern.ToJsonNFile(json);
            Application.Current.Dispatcher.Invoke(() =>
            {
                PatternManager.GetInstance().TemplatePatternFiles.Add(new TemplatePatternFile(json));
            });
        }

        private void GenAllTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(PatternManager.Config.SaveFilePath))
                Directory.CreateDirectory(PatternManager.Config.SaveFilePath);

            foreach (var item in TemplatePatternFiles)
            {
                SetTemplatePattern(item.FilePath);
                currentMat?.Dispose();
                currentMat = Patterns[cmbPattern1.SelectedIndex].Pattern.Gen(Config.Height, Config.Width);
                string name = Path.GetFileNameWithoutExtension(item.FilePath);

                currentMat.SaveImage(Path.Combine(PatternManager.Config.SaveFilePath ,name + $".{PatternManager.Config.PatternFormat}"));
            }
            PlatformHelper.OpenFolder(PatternManager.Config.SaveFilePath);
        }

        private void ListViewPattern_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                SetTemplatePattern(TemplatePatternFiles[listView.SelectedIndex].FilePath);
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplatePatternFile templateModelBase)
            {
                templateModelBase.IsEditMode = false;
            }
        }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            PatternManager.GetInstance().TemplatePatternFiles.Clear();
        }
    }
}
