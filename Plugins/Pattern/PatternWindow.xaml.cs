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
using Cv2 = OpenCvSharp.Cv2;
using MatType = OpenCvSharp.MatType;
using ImageEncodingParam = OpenCvSharp.ImageEncodingParam;
using ImwriteFlags = OpenCvSharp.ImwriteFlags;
using ColorConversionCodes = OpenCvSharp.ColorConversionCodes;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Windows.Media.PixelFormat;
using CvMat = OpenCvSharp.Mat;

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
        [System.ComponentModel.Description("单色位图 (*.bmp;*.dib)")]
        bmp1,
        [System.ComponentModel.Description("16色位图 (*.bmp;*.dib)")]
        bmp4,
        [System.ComponentModel.Description("256色位图 (*.bmp;*.dib)")]
        bmp8,
        [System.ComponentModel.Description("24位位图 (*.bmp;*.dib)")]
        bmp24,
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
            cmbFormat.ItemsSource = Enum.GetValues(typeof(PatternFormat))
                .Cast<PatternFormat>()
                .Select(f => new KeyValuePair<string, PatternFormat>(GetPatternFormatDescription(f), f));
            cmbFormat.SelectedValue = PatternManager.Config.PatternFormat;
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

            if (PatternMeta == null)
            {
                System.Windows.MessageBox.Show("请先选择图案");
                return;
            }

            string extension = GetFileExtension(PatternManager.Config.PatternFormat);
            string fileName = Path.Combine(
                PatternManager.GetInstance().PatternPath,
                Config.Width + "x" + Config.Height + "_" + PatternMeta.Pattern.GetTemplateName() + extension);

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = GetSaveFileDialogFilter(),
                DefaultExt = extension,
                FileName = fileName,
                AddExtension = true
            };
            if (dlg.ShowDialog() == true)
            {
                SavePatternImage(currentMat, dlg.FileName, PatternManager.Config.PatternFormat);
                System.Windows.MessageBox.Show("保存成功: " + dlg.FileName);
            }
        }

        private static string GetPatternFormatDescription(PatternFormat format)
        {
            var field = typeof(PatternFormat).GetField(format.ToString());
            var attr = field?.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            return attr?.Description ?? format.ToString();
        }

        private static string GetFileExtension(PatternFormat format)
        {
            return format switch
            {
                PatternFormat.bmp1 or PatternFormat.bmp4 or PatternFormat.bmp8 or PatternFormat.bmp24 => ".bmp",
                PatternFormat.tif => ".tif",
                PatternFormat.png => ".png",
                PatternFormat.jpg => ".jpg",
                _ => ".bmp"
            };
        }

        private static string GetSaveFileDialogFilter()
        {
            return string.Join("|", new[]
            {
                "单色位图 (*.bmp;*.dib)|*.bmp;*.dib",
                "16色位图 (*.bmp;*.dib)|*.bmp;*.dib",
                "256色位图 (*.bmp;*.dib)|*.bmp;*.dib",
                "24位位图 (*.bmp;*.dib)|*.bmp;*.dib",
                "PNG (*.png)|*.png",
                "JPEG (*.jpg)|*.jpg",
                "TIFF (*.tif)|*.tif"
            });
        }

        private static BitmapPalette CreatePalette(int colorCount)
        {
            if (colorCount <= 2)
                return BitmapPalettes.BlackAndWhite;

            var colors = new List<System.Windows.Media.Color>(colorCount);
            for (int i = 0; i < colorCount; i++)
            {
                byte gray = (byte)Math.Round(i * 255.0 / (colorCount - 1));
                colors.Add(System.Windows.Media.Color.FromRgb(gray, gray, gray));
            }
            return new BitmapPalette(colors);
        }

        private static BitmapSource CreateBmpBitmapSource(CvMat source, PatternFormat format)
        {
            using var normalized = EnsureGray8(source);
            PixelFormat pixelFormat;
            BitmapPalette? palette;

            switch (format)
            {
                case PatternFormat.bmp1:
                    pixelFormat = PixelFormats.Indexed1;
                    palette = BitmapPalettes.BlackAndWhite;
                    break;
                case PatternFormat.bmp4:
                    pixelFormat = PixelFormats.Indexed4;
                    palette = CreatePalette(16);
                    break;
                case PatternFormat.bmp8:
                    pixelFormat = PixelFormats.Indexed8;
                    palette = BitmapPalettes.Gray256;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported indexed bitmap format: {format}");
            }

            int width = normalized.Width;
            int height = normalized.Height;
            int stride = (width * pixelFormat.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[stride * height];
            var indexer = normalized.GetGenericIndexer<byte>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte value = indexer[y, x];
                    int rowOffset = y * stride;
                    switch (format)
                    {
                        case PatternFormat.bmp1:
                            if (value >= 128)
                                pixels[rowOffset + x / 8] |= (byte)(0x80 >> (x % 8));
                            break;
                        case PatternFormat.bmp4:
                            byte nibble = (byte)(value >> 4);
                            int byteIndex = rowOffset + x / 2;
                            if ((x & 1) == 0)
                                pixels[byteIndex] |= (byte)(nibble << 4);
                            else
                                pixels[byteIndex] |= nibble;
                            break;
                        case PatternFormat.bmp8:
                            pixels[rowOffset + x] = value;
                            break;
                    }
                }
            }

            return BitmapSource.Create(width, height, 96, 96, pixelFormat, palette, pixels, stride);
        }

        private static CvMat EnsureGray8(CvMat source)
        {
            if (source.Type().Channels == 1 && source.Depth() == 0)
                return source.Clone();

            var gray = new CvMat();
            if (source.Type().Channels == 1)
            {
                source.ConvertTo(gray, MatType.CV_8UC1);
                return gray;
            }

            Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
            return gray;
        }

        private static void SaveBitmapUsingEncoder(BitmapSource bitmapSource, string fileName, BitmapEncoder encoder)
        {
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            using var stream = File.Create(fileName);
            encoder.Save(stream);
        }

        private static void SavePatternImage(CvMat source, string fileName, PatternFormat format)
        {
            switch (format)
            {
                case PatternFormat.bmp1:
                case PatternFormat.bmp4:
                case PatternFormat.bmp8:
                    SaveBitmapUsingEncoder(CreateBmpBitmapSource(source, format), fileName, new BmpBitmapEncoder());
                    break;
                case PatternFormat.bmp24:
                    source.SaveImage(fileName);
                    break;
                case PatternFormat.png:
                    source.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.PngCompression, 3));
                    break;
                case PatternFormat.jpg:
                    source.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
                    break;
                case PatternFormat.tif:
                    source.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.TiffCompression, 1));
                    break;
                default:
                    source.SaveImage(fileName);
                    break;
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

            try
            {
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
                    if (pattern != null)
                    {
                        PatternMeta.Pattern.SetConfig(pattern.GetConfig().ToJsonN());
                    }
                }
                
                PatternEditorGrid.Child = PatternMeta.Pattern.GetPatternEditor();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to reset pattern: {ex.Message}", ex);
                MessageBox.Show($"重置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ResetSaveAsDefault_Click(object sender, RoutedEventArgs e)
        {                  
            if (PatternMeta == null) return;

            try
            {
                Type type = PatternMeta.Pattern.GetType();

                // Reset to class default
                IPattern pattern = (IPattern)Activator.CreateInstance(type);
                if (pattern != null)
                {
                    PatternMeta.Pattern.SetConfig(pattern.GetConfig().ToJsonN());
                }
                PatternUserDefaultManager.SaveUserDefault(PatternMeta.Pattern);
                PatternEditorGrid.Child = PatternMeta.Pattern.GetPatternEditor();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to reset pattern: {ex.Message}", ex);
                MessageBox.Show($"重置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

                    SavePatternImage(currentMat, Path.Combine(PatternManager.Config.SaveFilePath, name + GetFileExtension(PatternManager.Config.PatternFormat)), PatternManager.Config.PatternFormat);
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
