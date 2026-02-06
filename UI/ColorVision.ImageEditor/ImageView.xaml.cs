#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using log4net;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public static class ColormapExtension
    {
        public static Dictionary<ColormapTypes, string> GetColormapHDictionary()
        {
            var colormapDictionary = new Dictionary<ColormapTypes, string>
        {
            { ColormapTypes.COLORMAP_AUTUMN, "Assets/Colormap/colorscale_autumn.jpg" },
            { ColormapTypes.COLORMAP_BONE, "Assets/Colormap/colorscale_bone.jpg" },
            { ColormapTypes.COLORMAP_JET, "Assets/Colormap/colorscale_jet.jpg" },
            { ColormapTypes.COLORMAP_WINTER, "Assets/Colormap/colorscale_winter.jpg" },
            { ColormapTypes.COLORMAP_RAINBOW, "Assets/Colormap/colorscale_rainbow.jpg" },
            { ColormapTypes.COLORMAP_OCEAN, "Assets/Colormap/colorscale_ocean.jpg" },
            { ColormapTypes.COLORMAP_SUMMER, "Assets/Colormap/colorscale_summer.jpg" },
            { ColormapTypes.COLORMAP_SPRING, "Assets/Colormap/colorscale_spring.jpg" },
            { ColormapTypes.COLORMAP_COOL, "Assets/Colormap/colorscale_cool.jpg" },
            { ColormapTypes.COLORMAP_HSV, "Assets/Colormap/colorscale_hsv.jpg" },
            { ColormapTypes.COLORMAP_PINK, "Assets/Colormap/colorscale_pink.jpg" },
            { ColormapTypes.COLORMAP_HOT, "Assets/Colormap/colorscale_hot.jpg" },
            { ColormapTypes.COLORMAP_PARULA, "Assets/Colormap/colorscale_parula.jpg" },
            { ColormapTypes.COLORMAP_MAGMA, "Assets/Colormap/colorscale_magma.jpg" },
            { ColormapTypes.COLORMAP_INFERNO, "Assets/Colormap/colorscale_inferno.jpg" },
            { ColormapTypes.COLORMAP_PLASMA, "Assets/Colormap/colorscale_plasma.jpg" },
            { ColormapTypes.COLORMAP_VIRIDIS, "Assets/Colormap/colorscale_viridis.jpg" },
            { ColormapTypes.COLORMAP_CIVIDIS, "Assets/Colormap/colorscale_cividis.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT, "Assets/Colormap/colorscale_twilight.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT_SHIFTED, "Assets/Colormap/colorscale_twilight_shifted.jpg" },
            { ColormapTypes.COLORMAP_TURBO, "Assets/Colormap/colorscale_turbo.jpg" },
            { ColormapTypes.COLORMAP_DEEPGREEN, "Assets/Colormap/colorscale_deepgreen.jpg" }
        };
            return colormapDictionary;
        }
    }

    /// <summary>
    /// ImageShow.xaml 的交互逻辑
    /// </summary>
    public partial class ImageView : UserControl, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ImageView));
        public ImageViewModel ImageViewModel { get; set; }
        public ImageViewConfig Config => ImageViewModel.EditorContext.Config;

        public ObservableCollection<IDrawingVisual> DrawingVisualLists => ImageViewModel.EditorContext.DrawingVisualLists;

        public event EventHandler ClearImageEventHandler;

        public EditorContext EditorContext { get; set; }
        public bool IsShowScaleRuler { get; set; }


        public ImageView()
        {
            InitializeComponent();
        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ImageViewModel = new ImageViewModel(this);
            EditorContext = ImageViewModel.EditorContext;
            DataContext = ImageViewModel;
            this.Focusable = true;
            this.Focus();

            Config.Cleared += Config_Cleared;
            Config.ColormapTypesChanged += Config_ColormapTypesChanged;

            foreach (var item in ImageViewModel.IEditorToolFactory.IImageComponents)
                item.Execute(this);

            ImageShow.VisualsAdd += ImageShow_VisualsAdd;
            ImageShow.VisualsRemove += ImageShow_VisualsRemove;
            Drop += ImageView_Drop;
            Config.ShowTextChanged += (s, e) =>
            {
                foreach (var drawingVisual in DrawingVisualLists)
                {
                    if (drawingVisual.BaseAttribute is ITextProperties textProperties)
                    {
                        textProperties.IsShowText = Config.IsShowText;
                        drawingVisual.Render();
                    }
                }
            };
            Config.LayoutUpdatedChanged += (s, e) =>
            {
                if (e)
                {
                    Zoombox1.UpdateLayout();
                    ImageShow.IsLayoutUpdated = Config.IsLayoutUpdated;
                    ImageShow.Sacle = 1 / EditorContext.ZoomRatio;
                }
            };
            Zoombox1.LayoutUpdated +=(s,e) => ImageShow.Sacle = 1 / EditorContext.ZoomRatio;
            ImageShow.IsLayoutUpdated = Config.IsLayoutUpdated;
            ImageShow.Sacle = 1 / EditorContext.ZoomRatio;

            Config.ShowMsgChanged += (s, e) =>
            {
                if (!e)
                {
                    foreach (var drawingVisual in DrawingVisualLists)
                    {
                        drawingVisual.BaseAttribute.Msg = string.Empty;
                    }
                }
            };


            ComColormapTypes.ItemsSource = ColormapExtension.GetColormapHDictionary();

            ComboxeType.ItemsSource = from e1 in Enum.GetValues(typeof(MagnigifierType)).Cast<MagnigifierType>()
                                      select new KeyValuePair<MagnigifierType, string>(e1, e1.ToString());

            // Setup commands for file operations
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (s, e) => OpenImage(), (s, e) => { e.CanExecute = true; }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs, (s, e) => SaveAs(), (s, e) => { e.CanExecute = true; }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => Clear(), (s, e) => { e.CanExecute = true; }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, (s, e) => Print(), (s, e) => { e.CanExecute = true; }));

            //// Setup toolbar visibility toggle commands
            SetupToolbarToggleCommands();

            var _visibilityConfig = ConfigService.Instance.GetRequiredService<EditorToolVisibilityConfig>();

            // Initialize editor tools visibility list
            var EditorTools = new ObservableCollection<EditorToolViewModel>();
            foreach (var tool in ImageViewModel.IEditorToolFactory.IEditorTools.OrderBy(t => t.ToolBarLocal).ThenBy(t => t.Order))
            {
                var guidId = tool.GuidId ?? tool.GetType().Name;
                var toolViewModel = new EditorToolViewModel(this, tool, _visibilityConfig)
                {
                    DisplayName = guidId,
                    Location = tool.ToolBarLocal.ToString(),
                    IsVisible = _visibilityConfig.GetToolVisibility(guidId)
                };
                EditorTools.Add(toolViewModel);
            }
        }

        private void Config_Cleared(object? sender, EventArgs e)
        {
            Config.IsPseudo = false;
            FunctionImage = null;
            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            }
            GC.Collect();
        }

        public void SetBackGround(SolidColorBrush color)
        {
            ZoomGrid.Background = color;
        }

        private void Config_ColormapTypesChanged(object? sender, EventArgs e)
        {
            var ColormapTypes = ColormapExtension.GetColormapHDictionary().First(x => x.Key == Config.ColormapTypes);
            string valuepath = ColormapTypes.Value;
            if (ColormapTypesImage.Dispatcher.CheckAccess())
                ColormapTypesImage.Source = new BitmapImage(new Uri($"/ColorVision.ImageEditor;component/{valuepath}", UriKind.Relative));
            else
                ColormapTypesImage.Source = ColormapTypesImage.Dispatcher.Invoke(() => new BitmapImage(new Uri($"/ColorVision.ImageEditor;component/{valuepath}", UriKind.Relative)));
            DebounceTimer.AddOrResetTimer("PseudoSlider", 50, e => RenderPseudo(), 0);
        }
        /// <summary>
        /// 
        /// </summary>
        private void SetupToolbarToggleCommands()
        {
            // Show All Toolbars (Ctrl+Shift+A)
            var showAllToolbarsCommand = new RoutedCommand();
            CommandBindings.Add(new CommandBinding(showAllToolbarsCommand, (s, e) => 
            {
                Config.IsToolBarAlVisible = !Config.IsToolBarAlVisible;
                Config.IsToolBarDrawVisible = !Config.IsToolBarDrawVisible;
                Config.IsToolBarTopVisible = !Config.IsToolBarTopVisible;
                Config.IsToolBarLeftVisible = !Config.IsToolBarLeftVisible;
                Config.IsToolBarRightVisible = !Config.IsToolBarRightVisible;
            }));
            InputBindings.Add(new KeyBinding(showAllToolbarsCommand, Key.H, ModifierKeys.Control));
  
            // Open Toolbar Settings Window (Ctrl+Q)
            var openToolbarSettingsCommand = new RoutedCommand();
            CommandBindings.Add(new CommandBinding(openToolbarSettingsCommand, (s, e) => 
            {
                OpenToolbarSettingsWindow();
            }));
            InputBindings.Add(new KeyBinding(openToolbarSettingsCommand, Key.Q, ModifierKeys.Control));
        }

        private void OpenToolbarSettingsWindow()
        {
            var window = new ToolbarSettingsWindow(this);
            window.Owner = Window.GetWindow(this);
            window.ShowDialog();
        }
        /// <summary>
        /// 打印图像
        /// </summary>
        public void Print()
        {
            PrintDialog printDialog = new();
            if (printDialog.ShowDialog() == true)
            {
                // 创建一个可打印的区域
                Size pageSize = new(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
                ImageShow.Measure(pageSize);
                ImageShow.Arrange(new Rect(5, 5, pageSize.Width, pageSize.Height));

                // 开始打印
                printDialog.PrintVisual(ImageShow, "Printing");
            }
        }
        public void OpenImage()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OpenImage(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// 显示"另存为"对话框
        /// </summary>
        public void SaveAs()
        {
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "Png (*.png) | *.png";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            Save(dialog.FileName);
        }

        /// <summary>
        /// 保存图像到指定文件
        /// </summary>
        /// <param name="fileName">文件路径</param>
        public void Save(string fileName)
        {
            RenderTargetBitmap renderTargetBitmap = new((int)ImageShow.ActualWidth, (int)ImageShow.ActualHeight, Config.GetProperties<double>("DpiX"), Config.GetProperties<double>("DpiY"), PixelFormats.Pbgra32);
            renderTargetBitmap.Render(ImageShow);

            // 创建一个PngBitmapEncoder对象来保存位图为PNG文件
            PngBitmapEncoder pngEncoder = new();
            pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

            // 将PNG内容保存到文件
            using FileStream fileStream = new(fileName, FileMode.Create);
            pngEncoder.Save(fileStream);
        }

        private void ImageShow_VisualsAdd(object? sender, VisualChangedEventArgs e)
        {
            if (e.Visual is IDrawingVisual visual && !DrawingVisualLists.Contains(visual) && sender is Visual visual1)
            {
                DrawingVisualLists.Add(visual);
            }

        }

        private void ImageShow_VisualsRemove(object? sender, VisualChangedEventArgs e)
        {
            if (e.Visual is IDrawingVisual visual)
                DrawingVisualLists.Remove(visual);
        }




        private void ImageView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                var fn = a?.First();
                if (File.Exists(fn))
                {
                    OpenImage(fn);
                    e.Handled = true;
                }
            }
        }

        public void Clear()
        {
            ClearImageEventHandler?.Invoke(this, new EventArgs());
            Config.ClearProperties();
            FunctionImage = null;
            ViewBitmapSource = null;
            ImageShow.Clear();
            ImageShow.Source = null;
            ImageShow.UpdateLayout();
            ComboBoxLayers.Visibility = Visibility.Collapsed;
        }

        public void OpenImage(WriteableBitmap? writeableBitmap)
        {
            if (writeableBitmap != null)
                SetImageSource(writeableBitmap);
            else
            {
                log.Error("传入的 WriteableBitmap 为 null，无法打开图像。");
            }
        }


        private List<SelectionChangedEventHandler> _handlers = new List<SelectionChangedEventHandler>();
        public void AddSelectionChangedHandler(SelectionChangedEventHandler handler)
        {
            ComboBoxLayers.SelectionChanged += handler;
            _handlers.Add(handler);
        }

        public void ClearSelectionChangedHandlers()
        {
            foreach (var handler in _handlers)
            {
                ComboBoxLayers.SelectionChanged -= handler;
            }
            _handlers.Clear();
        }

        public void OpenImage(string? filePath)
        {
            //如果文件已经打开，不会重复打开
            if (filePath == null || filePath.Equals(Config.GetProperties<string>("FilePath"), StringComparison.Ordinal)) return;

            Config.ClearProperties();

            Config.AddProperties("FilePath", filePath);
            ClearSelectionChangedHandlers();
            Config.FilePath = filePath;
            try
            {
                if (filePath != null && File.Exists(filePath))
                {
                    long fileSize = new FileInfo(filePath).Length;
                    Config.AddProperties("FileSize", fileSize);

                    string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                    if (ImageViewModel.IEditorToolFactory.IImageOpens.TryGetValue(ext, out var imageOpen))
                    {
                        EditorContext.IImageOpen = imageOpen;
                        EditorContext.IImageOpen.OpenImage(EditorContext, filePath);
                        return;
                    }
                    else
                    {
                        MessageBox.Show($"不支持的图片格式 {ext}");
                    }
                }
            }
            catch(Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
            }

      }

        public HImage? HImageCache
        {
            get
            {
                if (_hImageCache == null && ViewBitmapSource != null && ViewBitmapSource is WriteableBitmap writeableBitmap)
                {
                    if (writeableBitmap.Dispatcher.CheckAccess())
                    {
                        _hImageCache = writeableBitmap.ToHImage();
                    }
                    else
                    {
                        _hImageCache = writeableBitmap.Dispatcher.Invoke(() => writeableBitmap.ToHImage());
                    }
                }
                return _hImageCache;
            }
            set { _hImageCache = value; }
        }
        private HImage? _hImageCache;


        public void SetImageSource(ImageSource imageSource)
        {
            FunctionImage = null;
            ViewBitmapSource = null;
            ImageShow.Source = null;
            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            }

            ComboBoxLayers.Visibility = Visibility.Visible;
            if (imageSource is WriteableBitmap writeableBitmap)
            {
                int cols = writeableBitmap.PixelWidth;
                int rows = writeableBitmap.PixelHeight;
                int channels, depth;

                switch (writeableBitmap.Format.ToString())
                {
                    case "Bgr32":
                    case "Bgra32":
                    case "Pbgra32":
                        channels = 4; // BGRA format has 4 channels
                        depth = 8; // 8 bits per channel
                        break;
                    case "Bgr24":
                    case "Rgb24":
                        channels = 3; // RGB format has 3 channels
                        depth = 8; // 8 bits per channel
                        break;
                    case "Indexed8":
                        depth = 8; // 8 bits per channel
                        channels = 1;
                        break;
                    case "Rgb48":
                        channels = 3; // RGB format has 3 channels
                        depth = 16; // 8 bits per channel
                        break;
                    case "Gray8":
                        channels = 1; // Gray scale has 1 channel
                        depth = 8; // 8 bits per channel
                        break;
                    case "Gray16":
                        channels = 1; // Gray scale has 1 channel
                        depth = 16; // 16 bits per channel
                        break;
                    case "Gray32Float":
                        channels = 1; // Gray scale has 1 channel
                        depth = 32; // 16 bits per channel
                        break;
                    default:
                        MessageBox.Show($"{writeableBitmap.Format}暂不支持的格式,请联系开发人员");
                        throw new NotSupportedException("The pixel format is not supported.");
                }

                int stride = cols * channels * (depth / 8);

                Config.AddProperties("PixelFormat", writeableBitmap.Format);
                Config.AddProperties("Cols", cols);
                Config.AddProperties("Rows", rows);
                Config.AddProperties("Channel", channels);
                Config.AddProperties("Depth", depth);
                Config.AddProperties("Stride", stride);
                Config.AddProperties("DpiX", writeableBitmap.DpiX);
                Config.AddProperties("DpiY", writeableBitmap.DpiY);


                if (depth == 16)
                {
                    Config.AddProperties("Max", 65535);
                    PseudoSlider.SmallChange = 255;
                    PseudoSlider.LargeChange = 2550;
                    PseudoSlider.Maximum = 65535;
                    PseudoSlider.ValueEnd = 65535;
                }
                else
                {
                    Config.AddProperties("Max", 255);
                    PseudoSlider.SmallChange = 1;
                    PseudoSlider.LargeChange = 10;
                    PseudoSlider.Maximum = 255;
                    PseudoSlider.ValueEnd = 255;

                }
                Config.AddProperties("PixelFormat", writeableBitmap.Format);
            }

            ViewBitmapSource = imageSource;
            ImageShow.Source = ViewBitmapSource;

            ImageShow.RaiseImageInitialized();
            CommandManager.InvalidateRequerySuggested();

        }

        public ImageSource FunctionImage { get; set; }
        public ImageSource ViewBitmapSource { get; set; }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            RenderPseudo();
        }

        private int PseudoSliderTime = 50;

        private void PseudoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            DebounceTimer.AddOrResetTimer("PseudoSlider", PseudoSliderTime, e => RenderPseudo(), 0);
        }
        public void RenderPseudo()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Pseudo.IsChecked == false)
                {
                    ImageShow.Source = ViewBitmapSource;
                    FunctionImage = null;
                    return;
                }

                if (HImageCache != null)
                {
                    // 首先获取滑动条的值，这需要在UI线程中执行

                    uint min = (uint)PseudoSlider.ValueStart;
                    uint max = (uint)PseudoSlider.ValueEnd;
                    int channel = ComboBoxLayers.SelectedIndex - 1;

                    log.Info($"PseudoColor,min:{min},max:{max}");
                    Task.Run(() =>
                    {
                        Stopwatch sw = Stopwatch.StartNew();

                        int ret = OpenCVMediaHelper.M_PseudoColor((HImage)HImageCache, out HImage hImageProcessed, min, max, Config.ColormapTypes, channel);
                        double algoMs = sw.Elapsed.TotalMilliseconds;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (ret == 0)
                            {
                                if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                                {
                                    var image = hImageProcessed.ToWriteableBitmap();
                                    hImageProcessed.Dispose();

                                    FunctionImage = image;
                                }

                                if (Pseudo.IsChecked == true)
                                {
                                    ImageShow.Source = FunctionImage;
                                }


                                sw.Stop();
                                double renderMs = sw.Elapsed.TotalMilliseconds;

                                PseudoSliderTime =(int)renderMs / 1000;

                                // ================== 输出结果 ==================
                                // 建议在界面上加一个 TextBlock (比如 TimeStatus) 显示，比看日志更直观

                                if (log.IsInfoEnabled)
                                {
                                    string perfMsg = $"算法耗时: {algoMs:F2} ms | 渲染耗时: {renderMs - algoMs:F2} ms | 总计: {(renderMs):F2} ms";
                                    log.Info(perfMsg); // 记录日志
                                }
                            }
                        });
                    });
                }
                ;
            });
        }


        public void AddVisual(Visual visual) => ImageShow.AddVisualCommand(visual);


        public List<string> ComboBoxLayerItems { get; set; } = new List<string>() { "Src", "R", "G", "B" };

        public void ComboBoxLayersSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxLayers.SelectedIndex < 0) return;

            if (ComboBoxLayerItems[ComboBoxLayers.SelectedIndex] == "Src")
                ExtractChannel(-1);
            if (ComboBoxLayerItems[ComboBoxLayers.SelectedIndex] == "R")
                ExtractChannel(2);
            if (ComboBoxLayerItems[ComboBoxLayers.SelectedIndex] == "G")
                ExtractChannel(1);
            if (ComboBoxLayerItems[ComboBoxLayers.SelectedIndex] == "B")
                ExtractChannel(0);
        }

        public void ExtractChannel(int channel)
        {
            if (ViewBitmapSource == null) return;

            if (channel == -1)
            {
                ImageShow.Source = ViewBitmapSource;
                return;
            }
            if (HImageCache == null) return;
            Task.Run(() =>
            {
                int ret = OpenCVMediaHelper.M_ExtractChannel((HImage)HImageCache, out HImage hImageProcessed, channel);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ret == 0)
                    {
                        if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                        {
                            var image = hImageProcessed.ToWriteableBitmap();
                            hImageProcessed.Dispose();
                            FunctionImage = image;
                        }
                        ImageShow.Source = FunctionImage;
                    }
                });
            });

        }
        public void UpdateZoomAndScale()
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Zoombox1.ZoomUniform();
            });
        }




        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (FunctionImage is WriteableBitmap writeableBitmap)
            {
                ViewBitmapSource = writeableBitmap;
                ImageShow.Source = ViewBitmapSource; ;
                HImageCache = writeableBitmap.ToHImage();
                FunctionImage = null;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            OpenImage(Config.FilePath);
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }


        public void Dispose()
        {
            Clear();
            ImageViewModel.Dispose();
            Config.Cleared -= Config_Cleared;

            ImageShow.VisualsAdd -= ImageShow_VisualsAdd;
            ImageShow.VisualsRemove -= ImageShow_VisualsRemove;

            ImageShow.Dispose();
            Drop -= ImageView_Drop;

            Zoombox1.Child = null;
            ZoomGrid.Children.Clear();
            GC.Collect();
            GC.SuppressFinalize(this);
        }

    }
}
