#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.EditorTools;
using ColorVision.UI;
using Gu.Wpf.Geometry;
using log4net;
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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{

    /// <summary>
    /// ImageShow.xaml 的交互逻辑
    /// </summary>
    public partial class ImageView : UserControl, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ImageView));
        public ImageViewModel ImageViewModel { get; set; }
        public ImageViewConfig Config => ImageViewModel.Config;

        public ObservableCollection<IDrawingVisual> DrawingVisualLists => ImageViewModel.DrawingVisualLists;

        public ImageView()
        {
            InitializeComponent();
        }


        private void Config_ColormapTypesChanged(object? sender, EventArgs e)
        {
            var ColormapTypes = ColormapConstats.GetColormapHDictionary().First(x => x.Key == Config.ColormapTypes);
            string valuepath = ColormapTypes.Value;
            if (ColormapTypesImage.Dispatcher.CheckAccess())
                ColormapTypesImage.Source = new BitmapImage(new Uri($"/ColorVision.ImageEditor;component/{valuepath}", UriKind.Relative));
            else
                ColormapTypesImage.Source = ColormapTypesImage.Dispatcher.Invoke(() => new BitmapImage(new Uri($"/ColorVision.ImageEditor;component/{valuepath}", UriKind.Relative)));
            DebounceTimer.AddOrResetTimer("PseudoSlider", 50, e => RenderPseudo(), 0);
        }

        private void ImageViewConfig_BalanceChanged(object? sender, EventArgs e)
        {
            DebounceTimer.AddOrResetTimer("AdjustWhiteBalance", 30, AdjustWhiteBalance);
        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ImageViewModel = new ImageViewModel(this, Zoombox1, ImageShow);

            DataContext = ImageViewModel;
            Config.ColormapTypesChanged -= Config_ColormapTypesChanged;
            Config.ColormapTypesChanged += Config_ColormapTypesChanged;
            Config.BalanceChanged += ImageViewConfig_BalanceChanged;

            foreach (var item in ComponentManager.GetInstance().IImageComponents)
                item.Execute(this);


            ImageViewModel.ClearImageEventHandler += Clear;

            ImageShow.VisualsAdd += ImageShow_VisualsAdd;
            ImageShow.VisualsRemove += ImageShow_VisualsRemove;
            PreviewKeyDown += ImageView_PreviewKeyDown;
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
                }
            };
            
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


            ComColormapTypes.ItemsSource = ColormapConstats.GetColormapHDictionary();

            ComboxeType.ItemsSource = from e1 in Enum.GetValues(typeof(MagnigifierType)).Cast<MagnigifierType>()
                                      select new KeyValuePair<MagnigifierType, string>(e1, e1.ToString());

            this.CommandBindings.Add(new CommandBinding( ApplicationCommands.Open, (s, e) => OpenImage(),(s, e) => { e.CanExecute = true; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs, (s, e) => SaveAs(), (s, e) => { e.CanExecute = true; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => Clear(), (s, e) => { e.CanExecute = true; }));

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
            RenderTargetBitmap renderTargetBitmap = new((int)ImageShow.Width, (int)ImageShow.Height, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(ImageShow);

            // 创建一个PngBitmapEncoder对象来保存位图为PNG文件
            PngBitmapEncoder pngEncoder = new();
            pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

            // 将PNG内容保存到文件
            using FileStream fileStream = new(fileName, FileMode.Create);
            pngEncoder.Save(fileStream);
        }


        public void Clear(object? sender, EventArgs e)
        {
            Clear();
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


        private void ImageView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                BorderPropertieslayers.Visibility = BorderPropertieslayers.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                InfoText.Text = Config.GetPropertyString();
            }
            else if (e.Key == Key.E)
            {
                ImageViewModel.ImageEditMode = !ImageViewModel.ImageEditMode;
            }
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
                }
            }
        }

        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                if (ImageViewModel.EraseManager.IsShow)
                {
                    Zoombox1.ActivateOn = toggleButton.IsChecked == true ? ModifierKeys.Control : ModifierKeys.None;
                }
            }
        }

        public void Clear()
        {

            Config.Properties.Clear();
            Config.FilePath = string.Empty;
            FunctionImage = null;
            ViewBitmapSource = null;
            ImageShow.Clear();
            ImageShow.Source = null;
            ImageShow.UpdateLayout();

            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            }
            GC.Collect();
            ComboBoxLayers.Visibility = Visibility.Collapsed;
        }

        public void OpenImage(WriteableBitmap? writeableBitmap)
        {
            if (writeableBitmap != null)
                SetImageSource(writeableBitmap);
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
            if (filePath == null || filePath.Equals(Config.FilePath, StringComparison.Ordinal)) return;
            Button1931.Visibility = Visibility.Collapsed;
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
                    if (ComponentManager.GetInstance().IImageOpens.TryGetValue(ext, out var imageOpen))
                    {
                        ImageViewModel.IImageOpen = imageOpen;
                        Config.AddProperties("ImageViewOpen", ImageViewModel.IImageOpen);
                        ImageViewModel.IImageOpen.OpenImage(this, filePath);
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

                Config.Channel = channels;
                Config.Ochannel = channels;

                if (depth == 16)
                {
                    Config.AddProperties("Max", 65535);
                    PseudoSlider.Maximum = 65535;
                    PseudoSlider.ValueEnd = 65535;
                    thresholdSlider.Maximum = 65535;
                    thresholdSlider.Value = 0;
                }
                else
                {
                    Config.AddProperties("Max", 255);
                    PseudoSlider.Maximum = 255;
                    PseudoSlider.ValueEnd = 255;
                    thresholdSlider.Maximum = 255;
                    thresholdSlider.Value = 0;
                }
                Config.AddProperties("PixelFormat", writeableBitmap.Format);
            }

            ViewBitmapSource = imageSource;
            ImageShow.Source = ViewBitmapSource;

            ImageShow.RaiseImageInitialized();
            ImageViewModel.ToolBarScaleRuler.IsShow = true;

        }


        public ImageSource FunctionImage { get; set; }
        public ImageSource ViewBitmapSource { get; set; }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            DebounceTimer.AddOrResetTimer("PseudoSlider", 50, e => RenderPseudo(), 0);
        }

        private void PseudoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            DebounceTimer.AddOrResetTimer("PseudoSlider", 50, e => RenderPseudo(), 0);
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

                    log.Info($"ImagePath，正在执行PseudoColor,min:{min},max:{max}");
                    Task.Run(() =>
                    {
                        int ret = OpenCVMediaHelper.M_PseudoColor((HImage)HImageCache, out HImage hImageProcessed, min, max, Config.ColormapTypes, channel);
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
                            }
                        });
                    });
                }
                ;
            });
        }


        public void AddVisual(Visual visual) => ImageShow.AddVisualCommand(visual);


        private void reference_Click(object sender, RoutedEventArgs e)
        {
            menuPop1.IsOpen = true;
        }

        private void reference1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                menuPop1.IsOpen = false;




            }
        }


        private void HistogramButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is not BitmapSource bitmapSource) return;

            var (redHistogram, greenHistogram, blueHistogram) = ImageUtils.RenderHistogram(bitmapSource);
            if (bitmapSource.Format.Masks.Count == 1)
            {
                HistogramChartWindow histogramChartWindow = new HistogramChartWindow(redHistogram);
                histogramChartWindow.Show();
            }
            else
            {
                HistogramChartWindow histogramChartWindow = new HistogramChartWindow(redHistogram, greenHistogram, blueHistogram);
                histogramChartWindow.Show();
            }
        }

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
                Config.Channel = Config.Ochannel;
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
                        Config.Channel = 1;
                    }
                });
            });

        }
        public void UpdateZoomAndScale()
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Zoombox1.ZoomUniform();
                ImageViewModel.ToolBarScaleRuler.Render();
            });
        }

        private void CM_AutoLevelsAdjust(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;
            if (toggleButton.IsChecked == false)
            {
                ImageShow.Source = ViewBitmapSource;
                FunctionImage = null;
                return;
            }
            if (HImageCache != null)
            {
                int ret = OpenCVMediaHelper.M_AutoLevelsAdjust((HImage)HImageCache, out HImage hImageProcessed);
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
            }
            ;
        }

        public void AdjustWhiteBalance()
        {
            if (HImageCache != null)
            {
                PixelFormat pixelFormat = Config.GetProperties<PixelFormat>("PixelFormat");
                if (pixelFormat == PixelFormats.Rgb48)
                {
                    //算法本身有余数，这里优化一下
                    int ret = OpenCVMediaHelper.M_GetWhiteBalance((HImage)HImageCache, out HImage hImageProcessed, Config.BlueBalance, Config.GreenBalance, Config.RedBalance);
                    if (ret == 0)
                    {
                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                            {
                                var image = hImageProcessed.ToWriteableBitmap();

                                hImageProcessed.Dispose();

                                FunctionImage = image;
                            }
                            ImageShow.Source = FunctionImage;
                        });
                    }
                }
                else
                {
                    int ret = OpenCVMediaHelper.M_GetWhiteBalance((HImage)HImageCache, out HImage hImageProcessed, Config.RedBalance, Config.GreenBalance, Config.BlueBalance);
                    if (ret == 0)
                    {
                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                            {
                                var image = hImageProcessed.ToWriteableBitmap();
                                hImageProcessed.Dispose();

                                FunctionImage = image;
                            }
                            ImageShow.Source = FunctionImage;
                        });
                    }
                }


            }
            ;
        }

        private void CM_AutomaticColorAdjustment(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;
            if (toggleButton.IsChecked == false)
            {
                ImageShow.Source = ViewBitmapSource;
                FunctionImage = null;
                return;
            }
            if (HImageCache != null)
            {
                int ret = OpenCVMediaHelper.M_AutomaticColorAdjustment((HImage)HImageCache, out HImage hImageProcessed);
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
            }
            ;
        }

        private void Button_3D_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is WriteableBitmap writeableBitmap)
            {
                Window3D window3D = new Window3D(writeableBitmap);
                window3D.Show();
            }
        }



        private void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("ApplyGammaCorrection", 50, a => ApplyGammaCorrection(a), GammaSlider.Value);
        }

        public void ApplyGammaCorrection(double Gamma)
        {
            if (HImageCache == null) return;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"ImagePath，正在执行ApplyGammaCorrection,Gamma{Gamma}");
            int ret = OpenCVMediaHelper.M_ApplyGammaCorrection((HImage)HImageCache, out HImage hImageProcessed, Gamma);
            Application.Current.Dispatcher.BeginInvoke(() =>
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
                    stopwatch.Stop();
                    log.Info($"ApplyGammaCorrection {stopwatch.Elapsed}");
                }
            });
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (FunctionImage is WriteableBitmap writeableBitmap)
            {
                ViewBitmapSource = writeableBitmap;
                ImageShow.Source = ViewBitmapSource; ;
                HImageCache = writeableBitmap.ToHImage();
                Config.Channel = HImageCache.Value.channels;
                FunctionImage = null;
                GammaSlider.Value = 1;
                Config.RedBalance = 1;
                Config.GreenBalance = 1;
                Config.BlueBalance = 1;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            GammaSlider.Value = 1;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Config.RedBalance = 1;
            Config.GreenBalance = 1;
            Config.BlueBalance = 1;
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            OpenImage(Config.FilePath);
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("AdjustBrightnessContrast", 50, AdjustBrightnessContrast, ContrastSlider.Value, BrightnessSlider.Value);
        }
        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("AdjustBrightnessContrast", 50, AdjustBrightnessContrast, ContrastSlider.Value, BrightnessSlider.Value);
        }
        public void AdjustBrightnessContrast(double Contrast, double Brightness)
        {
            if (HImageCache == null) return;
            //实现类似于PS的效果
            Brightness = Brightness * 4 / 5;
            Contrast = Contrast / 300 + 1;
            Brightness = HImageCache.Value.depth == 8 ? Brightness : Brightness * 255;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"ImagePath，正在执行AdjustBrightnessContrast,Brightness{Brightness},Contrast{Contrast}");
            int ret = OpenCVMediaHelper.M_AdjustBrightnessContrast((HImage)HImageCache, out HImage hImageProcessed, Contrast, Brightness);
            Application.Current.Dispatcher.BeginInvoke(() =>
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
                    stopwatch.Stop();
                    log.Info($"AdjustBrightnessContrast {stopwatch.Elapsed}");

                }
            });
        }

        public void InvertImag()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (HImageCache == null) return;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                log.Info($"InvertImag");
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_InvertImage((HImage)HImageCache, out HImage hImageProcessed);
                    Application.Current.Dispatcher.BeginInvoke(() =>
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
                            stopwatch.Stop();
                            log.Info($"InvertImag {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        }

        private void Button_Click_InvertImage(object sender, RoutedEventArgs e)
        {
            InvertImag();
        }


        void ThresholdImg()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (HImageCache == null) return;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                double thresh = thresholdSlider.Value;
                double maxval = Config.GetProperties<int>("Max");


                int type = 0;
                log.Info($"InvertImag");
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_Threshold((HImage)HImageCache, out HImage hImageProcessed, thresh, maxval, type);
                    Application.Current.Dispatcher.BeginInvoke(() =>
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
                            stopwatch.Stop();
                            log.Info($"InvertImag {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        }
        private void RemoveMoire_Click(object sender, RoutedEventArgs e)
        {
            RemoveMoire();
        }

        private void RemoveMoire()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (HImageCache == null) return;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                log.Info($"RemoveMoire");
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_RemoveMoire((HImage)HImageCache, out HImage hImageProcessed);
                    Application.Current.Dispatcher.BeginInvoke(() =>
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
                            stopwatch.Stop();
                            log.Info($"InvertImag {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        }


        private void Threshold_Click(object sender, RoutedEventArgs e)
        {
            ThresholdImg();
        }

        private void thresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("AdjustBrightnessContrast", 50, a => ThresholdImg(), e.NewValue);
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void EditDIExpand_Click(object sender, RoutedEventArgs e)
        {
            GraphicEditingWindow graphicEditingWindow = new GraphicEditingWindow(this) { Owner = Application.Current.GetActiveWindow()};

            // 屏幕坐标
            var point = this.PointToScreen(new Point(this.ActualWidth, this.ActualHeight));

            // 转换为WPF坐标
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                var targetPoint = source.CompositionTarget.TransformFromDevice.Transform(point);

                // 设置弹窗的位置
                graphicEditingWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                graphicEditingWindow.Left = targetPoint.X - graphicEditingWindow.Width;
                graphicEditingWindow.Top = targetPoint.Y - graphicEditingWindow.Height;
            }


            graphicEditingWindow.Show();
        }

        public void Dispose()
        {
            Clear();
            ImageViewModel.ClearImageEventHandler -= Clear;
            ImageViewModel.Dispose();

            ImageShow.VisualsAdd -= ImageShow_VisualsAdd;
            ImageShow.VisualsRemove -= ImageShow_VisualsRemove;

            ImageShow.Dispose();
            PreviewKeyDown -= ImageView_PreviewKeyDown;
            Drop -= ImageView_Drop;

            Zoombox1.Child = null;
            ZoomGrid.Children.Clear();
            GC.Collect();
        }
    }
}
