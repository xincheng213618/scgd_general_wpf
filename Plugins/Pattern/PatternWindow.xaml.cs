using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Pattern
{
    public class PatternFeatureLauncher : IFeatureLauncherBase
    {
        public override string? Header { get; set; } = Pattern.Properties.Resources.ChartGenerationTool;

        public override void Execute()
        {
            new PatternWindow() { WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

    public class ExportTestPatternWpf : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Pattern.Properties.Resources.ChartGenerationTool;
        public override int Order => 3;

        public override void Execute()
        {
            new PatternWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
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
            ("800x600",800,600), ("640x480",640,480)
        };
        static PatternWindowConfig Config => PatternWindowConfig.Instance;

        static PatternManager PatternManager => PatternManager.GetInstance();
        public static ObservableCollection<PatternMeta> Patterns => PatternManager.Patterns;

        public static ObservableCollection<TemplatePatternFile> TemplatePatternFiles => PatternManager.TemplatePatternFiles;
        public PatternMeta? PatternMeta { get; set; }
        ImageView imgDisplay { get; set; }

        private ListCollectionView? _templateFilesView;

        public PatternWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            this.Title += "-" + Assembly.GetAssembly(typeof(PatternWindow))?.GetName().Version?.ToString() ?? "";

            // 初始化CollectionView用于筛选
            _templateFilesView = new ListCollectionView(PatternManager.TemplatePatternFiles);
            ListViewPattern.ItemsSource = _templateFilesView;

            // 搜索框事件绑定
            PatternSearchBox.TextChanged += PatternSearchBox_TextChanged;

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

        private void PatternSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_templateFilesView == null) return;
            string keyword = PatternSearchBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                _templateFilesView.Filter = null;
            }
            else
            {
                _templateFilesView.Filter = obj =>
                {
                    if (obj is TemplatePatternFile file)
                        return file.Name != null && file.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                    return false;
                };
            }
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
            //ListViewPattern.ItemsSource = PatternManager.GetInstance().TemplatePatternFiles;
            ResolutionStackPanel.DataContext = PatternWindowConfig.Instance;
            imgDisplay = new ImageView();
            //这里最好实现成不模糊的样子
            RenderOptions.SetBitmapScalingMode(imgDisplay.ImageShow, BitmapScalingMode.NearestNeighbor);

            //DisplayGrid.Children.Add(imgDisplay);
            DisplayGrid.Child = imgDisplay;
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
                    //PatternEditorGrid.Children.Clear();
                    PatternEditorGrid.Child = null;
                    if (selectedPattern.Pattern is IPattern pattern)
                    {
                        PatternMeta = selectedPattern;
                        //PatternEditorGrid.Children.Add(pattern.GetPatternEditor());
                        PatternEditorGrid.Child = pattern.GetPatternEditor();
                    }
                }
            };
            cmbPattern1.ItemsSource = Patterns;
            cmbPattern1.SelectedIndex = 0;

            // Initialize theme icon based on current theme
            UpdateThemeIcon();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            new PropertyEditorWindow(Patterns[cmbPattern1.SelectedIndex].Pattern.GetConfig()).ShowDialog();
        }

        private void PatternGen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                currentMat?.Dispose();
                currentMat = Patterns[cmbPattern1.SelectedIndex].Pattern.Gen(Config.Height, Config.Width);

                imgDisplay.OpenImage(currentMat.ToWriteableBitmap());
                imgDisplay.Zoombox1.ZoomUniform();

            }
            catch(Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
            }


        }

        private void CmbResolution_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int idx = cmbResolution.SelectedIndex;
            if (idx >= 0 && idx < commonResolutions.Length)
            {
                Config.Width = commonResolutions[idx].Item2;
                Config.Height = commonResolutions[idx].Item3;
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

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (PatternMeta == null) return;

            Type type = PatternMeta.Pattern.GetType();
            
            // Try to load user default first
            string userDefault = PatternUserDefaultManager.LoadUserDefault(type);
            if (userDefault != null)
            {
                // Reset to user default
                PatternMeta.Pattern.SetConfig(userDefault);
            }
            else
            {
                // Reset to class default
                IPattern pattern = (IPattern)Activator.CreateInstance(type);
                PatternMeta.Pattern.SetConfig(pattern.GetConfig().ToJsonN());
            }
            
            PatternEditorGrid.Child = PatternMeta.Pattern.GetPatternEditor();
        }

        private void SaveAsDefault_Click(object sender, RoutedEventArgs e)
        {
            if (PatternMeta == null) return;

            try
            {
                PatternUserDefaultManager.SaveUserDefault(PatternMeta.Pattern);
                MessageBox.Show("当前配置已保存为默认值", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SetTemplatePattern(string templatePath)
        {
            try
            {
                string pattern = File.ReadAllText(templatePath);
                TemplatePattern templatePattern = JsonConvert.DeserializeObject<TemplatePattern>(pattern);
                PatternMeta = Patterns.FirstOrDefault(p => p.Name == templatePattern.PatternName);
                if (PatternMeta == null)
                {
                    System.Windows.MessageBox.Show("未找到对应的图案类型: " + templatePattern.PatternName);
                    return;
                }
                Config.Width = templatePattern.PatternWindowConfig.Width;
                Config.Height = templatePattern.PatternWindowConfig.Height;

                PatternMeta.Pattern.SetConfig(templatePattern.Config);
                PatternEditorGrid.Child = null;
                PatternEditorGrid.Child = PatternMeta.Pattern.GetPatternEditor();

                cmbPattern1.SelectedItem = PatternMeta;


                if (PatternManager.Config.IsSwitchCreate)
                {
                    currentMat?.Dispose();

                    currentMat = PatternMeta.Pattern.Gen(Config.Height, Config.Width);

                    imgDisplay.SetImageSource(currentMat.ToWriteableBitmap());
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
                {
                    json = Path.Combine(PatternManager.GetInstance().PatternPath, Config.Width + "x" + Config.Height + "_" + PatternMeta.Pattern.GetTemplateName() + $"{DateTime.UtcNow.Ticks}"+ ".json");
                }
                else
                {
                    if (PatternManager.GetInstance().TemplatePatternFiles.FirstOrDefault(a => a.FilePath == json) is TemplatePatternFile templatePatternFile)
                    {
                        PatternManager.GetInstance().TemplatePatternFiles.Remove(templatePatternFile);
                    }
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
                try
                {
                    SetTemplatePattern(item.FilePath);
                    currentMat?.Dispose();
                    currentMat = Patterns[cmbPattern1.SelectedIndex].Pattern.Gen(Config.Height, Config.Width);
                    string name = Path.GetFileNameWithoutExtension(item.FilePath);

                    currentMat.SaveImage(Path.Combine(PatternManager.Config.SaveFilePath, name + $".{PatternManager.Config.PatternFormat}"));
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                }

            }
            PlatformHelper.OpenFolder(PatternManager.Config.SaveFilePath);
        }

        private void ListViewPattern_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is TemplatePatternFile selectedFile)
            {
                SetTemplatePattern(selectedFile.FilePath);
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

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle between Light and Dark themes only
            Theme newTheme = ThemeManager.Current.CurrentUITheme == Theme.Dark ? Theme.Light : Theme.Dark;
            ThemeConfig.Instance.Theme = newTheme;
            Application.Current.ApplyTheme(newTheme);
            UpdateThemeIcon();
        }

        private void UpdateThemeIcon()
        {
            // E706 = Sunny (show when in dark mode - click to switch to light)
            // E708 = Moon (show when in light mode - click to switch to dark)
            ThemeIconText.Text = ThemeManager.Current.CurrentUITheme == Theme.Dark ? "\uE706" : "\uE708";
        }


    }
}
