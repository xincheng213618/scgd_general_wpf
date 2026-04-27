using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using Microsoft.Win32;
using OpenCvSharp.WpfExtensions;
using ProjectStarkSemi.Conoscope;
using ProjectStarkSemi.Layout;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectStarkSemi
{
    public class ConoscopeWindowConfig : WindowConfig
    {
        public static ConoscopeWindowConfig Instance => ConfigService.Instance.GetRequiredService<ConoscopeWindowConfig>();
    }

    /// <summary>
    /// ConoscopeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConoscopeWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeWindow));

        internal static ConoscopeWindow? Instance { get; private set; }
        internal DockLayoutManager? LayoutManager { get; private set; }

        private MVSViewWindow? observationCameraWindow;


        // Polar angle line management
        private ObservableCollection<PolarAngleLine> polarAngleLines = new ObservableCollection<PolarAngleLine>();
        private PolarAngleLine? selectedPolarLine;
        
        // Concentric circle line management
        private ConcentricCircleLine? selectedCircleLine;
        
        // Current image state for dynamic angle addition
        private BitmapSource? currentBitmapSource;
        private Point currentImageCenter;
        private int currentImageRadius;
        private double currentPixelsPerDegree;

        private ConoscopeCoordinateAxisController? coordinateAxisController;
        private PolarAngleLine? coordinateAxisPolarLine;
        private ConcentricCircleLine? coordinateAxisCircleLine;

        public double MaxAngle => ConoscopeConfig.CurrentModelProfile.MaxAngle;

        public ConoscopeModelProfile CurrentModelProfile => ConoscopeConfig.CurrentModelProfile;

        private void RefreshReferenceLineProfileBinding()
        {
            GridSetting.Children.Clear();
            StackPanel settingPanel = new StackPanel();
            settingPanel.Children.Add(CreateSettingGroup("坐标轴", CurrentModelProfile.CoordinateAxisParam));
            GridSetting.Children.Add(settingPanel);
        }

        private static GroupBox CreateSettingGroup(string header, object source)
        {
            return new GroupBox
            {
                Header = header,
                Margin = new Thickness(0, 0, 0, 8),
                Content = PropertyEditorHelper.GenPropertyEditorControl(source)
            };
        }

        private void RefreshModelDependentUi()
        {
            if (tbCurrentModel != null)
            {
                tbCurrentModel.Text = CurrentModelProfile.DisplayName;
            }

            if (btnOpenObservationCamera != null)
            {
                btnOpenObservationCamera.Visibility = CurrentModelProfile.HasObservationCamera
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            RefreshReferenceLineProfileBinding();
            SetReferencePlotLimits();
        }

        internal void RefreshConoscopeConfiguration()
        {
            RefreshModelDependentUi();
            UpdateReferencePlotHeader();
            if (HasXyzData())
            {
                RefreshDisplayedImage();
            }
        }

        public ConoscopeWindow()
        {
            InitializeComponent();
            Instance = this;
            this.ApplyCaption();
            ConoscopeWindowConfig.Instance.SetWindow(this);
            this.Title += Assembly.GetAssembly(typeof(ConoscopeWindow))?.GetName().Version?.ToString() ?? "";
            this.Closing += (s, e) => LayoutManager?.SaveLayout();
            this.Closed += (s, e) => Instance = null;
        }

        public ConoscopeConfig ConoscopeConfig => ConoscopeManager.GetInstance().Config;

        private void Window_Initialized(object sender, EventArgs e)
        {
            void ThemeChange(Theme theme)
            {
                DockingManager.Theme = theme == Theme.Dark
                    ? new AvalonDock.Themes.Vs2013DarkTheme()
                    : new AvalonDock.Themes.Vs2013LightTheme();
            }
            ThemeChange(ThemeManager.Current.CurrentUITheme);
            ThemeManager.Current.CurrentUIThemeChanged += ThemeChange;
            this.Closed += (s, e) => ThemeManager.Current.CurrentUIThemeChanged -= ThemeChange;


            LayoutManager = new DockLayoutManager(DockingManager);
            LayoutManager.RegisterContent("ImageView", ImageViewHost);
            LayoutManager.RegisterContent("ChannelPanel", ChannelPanelPane.Content);
            LayoutManager.RegisterContent("ReferencePlot", ReferencePlotPane.Content);
            LayoutManager.RegisterContent("SettingPanel", SettingPanelPane.Content);
            if (!LayoutManager.LoadLayout())
            {
                LayoutManager.ResetLayout();
            }

            MenuManager.GetInstance().LoadMenuForWindow("Conoscope", menu);


            RefreshReferenceLineProfileBinding();


            cbModelType.ItemsSource = Enum.GetValues(typeof(ConoscopeModelType));
            this.DataContext = ConoscopeManager.GetInstance();
            SelectComboBoxItemByTag(cbDisplayChannel, ConoscopeConfig.DisplayChannel.ToString());
            cbFilterType_SelectionChanged(cbFilterType, new SelectionChangedEventArgs(Selector.SelectionChangedEvent, new List<object>(), new List<object>()));

            var cameras = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList();

            ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig_ModelTypeChanged(sender, ConoscopeConfig.CurrentModel);

            this.Closed += (s, e) =>
            {
                ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            };
            InitializePlot(wpfPlotReference, "参考曲线 (Reference Distribution)");
            UpdateReferencePlotHeader();
        }

        private static void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
            if (tbCurrentModel == null) return;
            RefreshModelDependentUi();
        }

        private void InitializePlot(ScottPlot.WPF.WpfPlot plot, string title)
        {
            plot.Plot.Title(title);
            plot.Plot.XLabel("Degrees");
            plot.Plot.YLabel("Luminance (cd/m²)");
            plot.Plot.Legend.FontName = ScottPlot.Fonts.Detect("中文");

            string fontSample = $"中文 Luminance Voltage";
            plot.Plot.Axes.Title.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Bottom.Label.FontName = ScottPlot.Fonts.Detect(fontSample);

            // Enable grid for better readability
            plot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
            plot.Plot.Grid.MajorLineWidth = 1;
            plot.Plot.Axes.SetLimits(-MaxAngle, MaxAngle, 0, 600);

            plot.Refresh();
        }


        private void cbModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbModelType.SelectedItem is ConoscopeModelType conoscopeModelType)
            {
                ConoscopeConfig.CurrentModel = conoscopeModelType;
            }
        }


        private void btnOpenObservationCamera_Click(object sender, RoutedEventArgs e)
        {
            observationCameraWindow = new MVSViewWindow();
            observationCameraWindow.Show();
        }

        private void cbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbFilterType == null) return;
            
            var selectedFilter = (ImageFilterType)cbFilterType.SelectedIndex;
            
            // Update parameter visibility based on filter type
            if (sliderKernelSize != null && sliderSigma != null && sliderD != null && sliderSigmaColor != null && sliderSigmaSpace != null)
            {
                switch (selectedFilter)
                {
                    case ImageFilterType.None:
                        sliderKernelSize.IsEnabled = false;
                        sliderSigma.IsEnabled = false;
                        sliderD.IsEnabled = false;
                        sliderSigmaColor.IsEnabled = false;
                        sliderSigmaSpace.IsEnabled = false;
                        break;
                    case ImageFilterType.LowPass:
                    case ImageFilterType.MovingAverage:
                    case ImageFilterType.Median:
                        sliderKernelSize.IsEnabled = true;
                        sliderSigma.IsEnabled = false;
                        sliderD.IsEnabled = false;
                        sliderSigmaColor.IsEnabled = false;
                        sliderSigmaSpace.IsEnabled = false;
                        break;
                    case ImageFilterType.Gaussian:
                        sliderKernelSize.IsEnabled = true;
                        sliderSigma.IsEnabled = true;
                        sliderD.IsEnabled = false;
                        sliderSigmaColor.IsEnabled = false;
                        sliderSigmaSpace.IsEnabled = false;
                        break;
                    case ImageFilterType.Bilateral:
                        sliderKernelSize.IsEnabled = false;
                        sliderSigma.IsEnabled = false;
                        sliderD.IsEnabled = true;
                        sliderSigmaColor.IsEnabled = true;
                        sliderSigmaSpace.IsEnabled = true;
                        break;
                }
            }
        }

        /// <summary>
        /// 对单通道Mat应用指定滤波
        /// </summary>
        private OpenCvSharp.Mat ApplyFilterToMat(OpenCvSharp.Mat src, ImageFilterType filterType, int kernelSize, double sigma, int d, double sigmaColor, double sigmaSpace)
        {
            var dst = new OpenCvSharp.Mat();

            OpenCvSharp.Mat src8U = new OpenCvSharp.Mat();
            bool needConvert = src.Depth() != OpenCvSharp.MatType.CV_8U && src.Depth() != OpenCvSharp.MatType.CV_32F;

            OpenCvSharp.Mat workMat = src;

            switch (filterType)
            {
                case ImageFilterType.LowPass:
                    OpenCvSharp.Cv2.Blur(workMat, dst, new OpenCvSharp.Size(kernelSize, kernelSize));
                    break;
                case ImageFilterType.MovingAverage:
                    OpenCvSharp.Cv2.BoxFilter(workMat, dst, workMat.Type(), new OpenCvSharp.Size(kernelSize, kernelSize));
                    break;
                case ImageFilterType.Gaussian:
                    OpenCvSharp.Cv2.GaussianBlur(workMat, dst, new OpenCvSharp.Size(kernelSize, kernelSize), sigma);
                    break;
                case ImageFilterType.Median:
                    // MedianBlur 需要 CV_8U 或 CV_32F
                    if (src.Depth() == OpenCvSharp.MatType.CV_32F)
                    {
                        OpenCvSharp.Cv2.MedianBlur(workMat, dst, kernelSize);
                    }
                    else
                    {
                        // 转为 CV_32F 再处理
                        OpenCvSharp.Mat floatMat = new OpenCvSharp.Mat();
                        workMat.ConvertTo(floatMat, OpenCvSharp.MatType.CV_32FC1);
                        OpenCvSharp.Cv2.MedianBlur(floatMat, dst, kernelSize);
                        // 转回原始类型
                        var result = new OpenCvSharp.Mat();
                        dst.ConvertTo(result, src.Type());
                        floatMat.Dispose();
                        dst.Dispose();
                        dst = result;
                    }
                    break;
                case ImageFilterType.Bilateral:
                    // BilateralFilter 需要 CV_8U 或 CV_32F
                    if (src.Depth() == OpenCvSharp.MatType.CV_32F)
                    {
                        OpenCvSharp.Cv2.BilateralFilter(workMat, dst, d, sigmaColor, sigmaSpace);
                    }
                    else
                    {
                        OpenCvSharp.Mat floatMat = new OpenCvSharp.Mat();
                        workMat.ConvertTo(floatMat, OpenCvSharp.MatType.CV_32FC1);
                        OpenCvSharp.Cv2.BilateralFilter(floatMat, dst, d, sigmaColor, sigmaSpace);
                        var result = new OpenCvSharp.Mat();
                        dst.ConvertTo(result, src.Type());
                        floatMat.Dispose();
                        dst.Dispose();
                        dst = result;
                    }
                    break;
                default:
                    return src.Clone();
            }

            return dst;
        }

        private void btnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filterType = GetSelectedFilterType();

                if (!HasXyzData())
                {
                    MessageBox.Show("请先获取图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (filterType == ImageFilterType.None)
                {
                    RestoreOriginalMats();
                    RefreshDisplayedImage();
                    log.Info("已恢复原始数据");
                    MessageBox.Show("已恢复原始数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                log.Info($"开始应用滤波: {filterType}");
                ApplyFilterToCurrentMats(filterType);
                RefreshDisplayedImage();

                log.Info("滤波应用成功，数据已更新");
                MessageBox.Show("滤波应用成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"应用滤波失败: {ex.Message}", ex);
                MessageBox.Show($"应用滤波失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public OpenCvSharp.Mat? XMat { get; set; }
        public OpenCvSharp.Mat? YMat { get; set; }
        public OpenCvSharp.Mat? ZMat { get; set; }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filename = openFileDialog.FileName;
                OpenConoscope(filename);
            }       
        }

        string Filename = string.Empty;
        private void OpenConoscope(string filename)
        {
            try
            {
                Filename = filename;
                HideCoordinateDragOverlay();
                DisposeCoordinateAxis();
                ImageView.Clear();
                LoadConoscopeData(filename);

                if (chkApplyFilterOnOpen?.IsChecked == true)
                {
                    ConoscopeConfig.ApplyFilterOnOpen = true;
                    ApplyFilterToCurrentMats(GetSelectedFilterType());
                }

                RefreshDisplayedImage();
            }
            catch (Exception ex)
            {
                log.Error($"打开Conoscope图像失败: {ex.Message}", ex);
                MessageBox.Show($"打开图像失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadConoscopeData(string filename)
        {
            if (!CVFileUtil.IsCVCIEFile(filename))
            {
                throw new NotSupportedException("当前视图仅支持 CVCIE XYZ 图像文件");
            }

            ClearMatData();

            CVFileUtil.Read(filename, out CVCIEFile fileInfo);
            if (fileInfo.Channels < 3)
            {
                throw new NotSupportedException($"CVCIE 文件通道数不足: {fileInfo.Channels}");
            }

            int bytesPerPixel = fileInfo.Bpp / 8;
            int channelSize = fileInfo.Cols * fileInfo.Rows * bytesPerPixel;
            if (fileInfo.Data == null || fileInfo.Data.Length < channelSize * 3)
            {
                throw new InvalidDataException("CVCIE 文件数据长度不足，无法拆分 XYZ 通道");
            }

            OpenCvSharp.MatType singleChannelType = GetSingleChannelMatType(fileInfo.Bpp);
            XMat = CreateFloatChannelMat(fileInfo.Data, 0, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
            YMat = CreateFloatChannelMat(fileInfo.Data, channelSize, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
            ZMat = CreateFloatChannelMat(fileInfo.Data, channelSize * 2, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);

            log.Info($"已加载 CVCIE XYZ 数据: {fileInfo.Cols}x{fileInfo.Rows}, Bpp={fileInfo.Bpp}");
        }

        private static OpenCvSharp.MatType GetSingleChannelMatType(int bpp)
        {
            return bpp switch
            {
                8 => OpenCvSharp.MatType.CV_8UC1,
                16 => OpenCvSharp.MatType.CV_16UC1,
                32 => OpenCvSharp.MatType.CV_32FC1,
                64 => OpenCvSharp.MatType.CV_64FC1,
                _ => throw new NotSupportedException($"Bpp {bpp} not supported")
            };
        }

        private static OpenCvSharp.Mat CreateFloatChannelMat(byte[] source, int offset, int channelSize, int rows, int cols, OpenCvSharp.MatType sourceType)
        {
            byte[] channelData = new byte[channelSize];
            Buffer.BlockCopy(source, offset, channelData, 0, channelSize);

            using OpenCvSharp.Mat raw = OpenCvSharp.Mat.FromPixelData(rows, cols, sourceType, channelData);
            OpenCvSharp.Mat copied = raw.Clone();
            if (copied.Type() == OpenCvSharp.MatType.CV_32FC1)
            {
                return copied;
            }

            OpenCvSharp.Mat floatMat = new OpenCvSharp.Mat();
            copied.ConvertTo(floatMat, OpenCvSharp.MatType.CV_32FC1);
            copied.Dispose();
            return floatMat;
        }

        private void ApplyFilterToCurrentMats(ImageFilterType filterType)
        {
            if (filterType == ImageFilterType.None)
            {
                return;
            }

            int kernelSize = NormalizeKernelSize((int)(sliderKernelSize?.Value ?? 55));
            double sigma = sliderSigma?.Value ?? 0;
            int d = (int)(sliderD?.Value ?? 9);
            double sigmaColor = sliderSigmaColor?.Value ?? 75;
            double sigmaSpace = sliderSigmaSpace?.Value ?? 75;

            if (XMat != null)
            {
                OpenCvSharp.Mat filtered = ApplyFilterToMat(XMat, filterType, kernelSize, sigma, d, sigmaColor, sigmaSpace);
                XMat.Dispose();
                XMat = filtered;
            }
            if (YMat != null)
            {
                OpenCvSharp.Mat filtered = ApplyFilterToMat(YMat, filterType, kernelSize, sigma, d, sigmaColor, sigmaSpace);
                YMat.Dispose();
                YMat = filtered;
            }
            if (ZMat != null)
            {
                OpenCvSharp.Mat filtered = ApplyFilterToMat(ZMat, filterType, kernelSize, sigma, d, sigmaColor, sigmaSpace);
                ZMat.Dispose();
                ZMat = filtered;
            }

            log.Info($"滤波应用到XYZ通道完成: {filterType}, kernelSize={kernelSize}");
        }

        private static int NormalizeKernelSize(int kernelSize)
        {
            kernelSize = Math.Max(1, kernelSize);
            return kernelSize % 2 == 0 ? kernelSize + 1 : kernelSize;
        }

        private void RestoreOriginalMats()
        {
            if (string.IsNullOrWhiteSpace(Filename))
            {
                return;
            }

            LoadConoscopeData(Filename);
        }

        private void RefreshDisplayedImage()
        {
            if (!HasXyzData())
            {
                return;
            }

            ExportChannel displayChannel = GetSelectedDisplayChannel();
            using OpenCvSharp.Mat channelMat = CreateDisplayChannelMat(displayChannel);
            using OpenCvSharp.Mat normalized = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat gray8 = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat pseudoColor = new OpenCvSharp.Mat();

            OpenCvSharp.Cv2.Normalize(channelMat, normalized, 0, 255, OpenCvSharp.NormTypes.MinMax);
            normalized.ConvertTo(gray8, OpenCvSharp.MatType.CV_8UC1);
            OpenCvSharp.Cv2.ApplyColorMap(gray8, pseudoColor, OpenCvSharp.ColormapTypes.Jet);
            WriteableBitmap bitmap = pseudoColor.ToWriteableBitmap();
            bitmap.Freeze();

            DisposeCoordinateAxis();
            ImageView.Clear();
            ImageView.SetImageSource(bitmap);
            CreateAndAnalyzePolarLines();
        }

        private OpenCvSharp.Mat CreateDisplayChannelMat(ExportChannel channel)
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                throw new InvalidOperationException("XYZ 数据未加载");
            }

            if (channel == ExportChannel.X)
            {
                return XMat.Clone();
            }
            if (channel == ExportChannel.Y)
            {
                return YMat.Clone();
            }
            if (channel == ExportChannel.Z)
            {
                return ZMat.Clone();
            }

            OpenCvSharp.Mat result = new OpenCvSharp.Mat(YMat.Rows, YMat.Cols, OpenCvSharp.MatType.CV_32FC1);
            for (int row = 0; row < YMat.Rows; row++)
            {
                for (int col = 0; col < YMat.Cols; col++)
                {
                    double value = ConoscopeColorimetry.GetChannelValue(
                        XMat.At<float>(row, col),
                        YMat.At<float>(row, col),
                        ZMat.At<float>(row, col),
                        channel);
                    result.Set(row, col, (float)value);
                }
            }

            return result;
        }

        private bool HasXyzData()
        {
            return XMat != null && YMat != null && ZMat != null;
        }

        private ImageFilterType GetSelectedFilterType()
        {
            if (cbFilterType?.SelectedIndex >= 0)
            {
                return (ImageFilterType)cbFilterType.SelectedIndex;
            }

            return ImageFilterType.Gaussian;
        }

        private ExportChannel GetSelectedDisplayChannel()
        {
            if (cbDisplayChannel?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string channelTag &&
                Enum.TryParse(channelTag, out ExportChannel channel))
            {
                return channel;
            }

            return ConoscopeConfig.DisplayChannel;
        }

        private void DisplayChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ExportChannel channel = GetSelectedDisplayChannel();
            ConoscopeConfig.DisplayChannel = channel;

            if (HasXyzData())
            {
                RefreshDisplayedImage();
            }
        }

        private void ClearMatData()
        {
            XMat?.Dispose();
            XMat = null;
            YMat?.Dispose();
            YMat = null;
            ZMat?.Dispose();
            ZMat = null;
        }

        private void InitializeCoordinateAxis(Point center, int radius)
        {
            var axisParam = CurrentModelProfile.CoordinateAxisParam;
            axisParam.PropertyChanged -= CoordinateAxisParam_PropertyChanged;
            axisParam.PropertyChanged += CoordinateAxisParam_PropertyChanged;
            axisParam.MaxAngle = MaxAngle;
            axisParam.ConoscopeCoefficient = currentPixelsPerDegree;
            axisParam.CenterX = center.X;
            axisParam.CenterY = center.Y;
            axisParam.AxisRadius = radius;
            axisParam.ReferenceRadiusAngle = Math.Max(0, Math.Min(axisParam.ReferenceRadiusAngle, MaxAngle));
            axisParam.NormalizeNDApertures();

            coordinateAxisController?.ReferenceChanged -= CoordinateAxisController_ReferenceChanged;
            coordinateAxisController?.PointerMoved -= CoordinateAxisController_PointerMoved;
            coordinateAxisController?.PointerLeft -= CoordinateAxisController_PointerLeft;
            coordinateAxisController?.Dispose();
            coordinateAxisController = new ConoscopeCoordinateAxisController(ImageView.ImageShow, ImageView.Zoombox1, axisParam);
            coordinateAxisController.ReferenceChanged += CoordinateAxisController_ReferenceChanged;
            coordinateAxisController.PointerMoved += CoordinateAxisController_PointerMoved;
            coordinateAxisController.PointerLeft += CoordinateAxisController_PointerLeft;
            coordinateAxisController.Configure(center, radius, MaxAngle, currentPixelsPerDegree);
            coordinateAxisController.Show();
            UpdateReferencePlotHeader();
        }

        private void CoordinateAxisParam_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceMode))
            {
                ApplyCoordinateAxisReference();
                return;
            }

            if (e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceAngle) ||
                e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceRadiusAngle))
            {
                UpdateReferencePlotHeader();
            }
        }

        private void CoordinateAxisController_ReferenceChanged(object? sender, ConoscopeCoordinateReferenceChangedEventArgs e)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            HideCoordinateDragOverlay();

            if (!e.IsValueChanged && !e.IsFinal)
            {
                return;
            }

            if (e.Mode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                UpdateCoordinateAxisAzimuth(e.Angle);
            }
            else
            {
                UpdateCoordinateAxisPolar(e.RadiusAngle);
            }
        }

        private void CoordinateAxisController_PointerMoved(object? sender, ConoscopeCoordinateReferenceChangedEventArgs e)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            ShowCoordinateDragOverlay(e);
        }

        private void CoordinateAxisController_PointerLeft(object? sender, EventArgs e)
        {
            HideCoordinateDragOverlay();
        }

        private void ShowCoordinateDragOverlay(ConoscopeCoordinateReferenceChangedEventArgs e)
        {
            Point screenPoint = ImageView.ImageShow.PointToScreen(e.Position);
            Point overlayPoint = ImageViewHost.PointFromScreen(screenPoint);
            CoordinateDragOverlayText.Text = GetCoordinateDragOverlayText(e);
            CoordinateDragOverlay.Visibility = Visibility.Visible;
        }

        private string GetCoordinateDragOverlayText(ConoscopeCoordinateReferenceChangedEventArgs e)
        {
            if (currentBitmapSource == null)
            {
                return GetReferenceValueText(e.Mode, e.Angle, e.RadiusAngle);
            }

            int imageWidth = currentBitmapSource.PixelWidth;
            int imageHeight = currentBitmapSource.PixelHeight;
            int ix = ClampToInt((int)Math.Round(e.Position.X), 0, imageWidth - 1);
            int iy = ClampToInt((int)Math.Round(e.Position.Y), 0, imageHeight - 1);

            int xyzWidth = YMat?.Width ?? XMat?.Width ?? ZMat?.Width ?? imageWidth;
            int xyzHeight = YMat?.Height ?? XMat?.Height ?? ZMat?.Height ?? imageHeight;
            int xyzX = ClampToInt(ix, 0, xyzWidth - 1);
            int xyzY = ClampToInt(iy, 0, xyzHeight - 1);
            ExtractXYZValues(xyzX, xyzY, out double X, out double Y, out double Z);

            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(X, Y, Z);
            ExportChannel displayChannel = GetSelectedDisplayChannel();
            double displayValue = GetChannelValue(X, Y, Z, displayChannel);
            double azimuthAngle = GetFullAzimuthAngle(e.Position);
            double polarAngle = GetPolarRadiusAngle(e.Position);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"参考: {GetReferenceValueText(e.Mode, e.Angle, e.RadiusAngle)}");
            builder.AppendLine($"像素: X={ix}, Y={iy}");
            builder.AppendLine($"极坐标: 方位={azimuthAngle:F2}°, 极角={polarAngle:F2}°");
            builder.AppendLine($"{GetChannelLabel(displayChannel)}: {displayValue:F6}");
            builder.AppendLine($"XYZ: X={X:F4}, Y={Y:F4}, Z={Z:F4}");
            builder.AppendLine($"xy: x={chromaticity.x:F6}, y={chromaticity.y:F6}");
            builder.Append($"uv: u={chromaticity.u:F6}, v={chromaticity.v:F6}, CCT={ConoscopeColorimetry.FormatCct(chromaticity.Cct)}");
            return builder.ToString();
        }

        private double GetFullAzimuthAngle(Point point)
        {
            double deltaX = point.X - currentImageCenter.X;
            double deltaY = currentImageCenter.Y - point.Y;
            double angle = Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI;
            return angle < 0 ? angle + 360.0 : angle;
        }

        private double GetPolarRadiusAngle(Point point)
        {
            if (currentImageRadius <= 0)
            {
                return 0;
            }

            double distance = (point - currentImageCenter).Length;
            return Math.Max(0, Math.Min(distance / currentImageRadius * MaxAngle, MaxAngle));
        }

        private static int ClampToInt(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            return Math.Max(min, Math.Min(value, max));
        }

        private void HideCoordinateDragOverlay()
        {
            CoordinateDragOverlay.Visibility = Visibility.Collapsed;
        }

        private void ApplyCoordinateAxisReference()
        {
            if (coordinateAxisController == null)
            {
                return;
            }

            if (coordinateAxisController.Axis.Attribute.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                UpdateCoordinateAxisAzimuth(coordinateAxisController.Axis.Attribute.ReferenceAngle);
            }
            else
            {
                UpdateCoordinateAxisPolar(coordinateAxisController.Axis.Attribute.ReferenceRadiusAngle);
            }

            SetReferencePlotLimits();
            UpdateReferencePlotHeader();
        }

        private void UpdateCoordinateAxisAzimuth(double angle)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            angle = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(angle);

            if (coordinateAxisPolarLine == null)
            {
                coordinateAxisPolarLine = new PolarAngleLine();
            }

            coordinateAxisPolarLine.Angle = angle;
            coordinateAxisPolarLine.RgbData.Clear();
            coordinateAxisPolarLine.Line = null;

            var endpoints = ConoscopeCoordinateAxisVisual.GetAzimuthLineEndpoints(currentImageCenter, currentImageRadius, angle);
            ExtractRgbAlongLine(coordinateAxisPolarLine, endpoints.Start, endpoints.End, currentBitmapSource, currentImageRadius);

            selectedPolarLine = coordinateAxisPolarLine;
            SetReferencePlotLimits();
            UpdateReferencePlotHeader();
            UpdatePlot();
        }

        private void UpdateCoordinateAxisPolar(double radiusAngle)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            radiusAngle = Math.Max(0, Math.Min(radiusAngle, MaxAngle));

            if (coordinateAxisCircleLine == null)
            {
                coordinateAxisCircleLine = new ConcentricCircleLine();
            }

            coordinateAxisCircleLine.RadiusAngle = radiusAngle;
            coordinateAxisCircleLine.RgbData.Clear();
            coordinateAxisCircleLine.Circle = null;
            ExtractRgbAlongCircle(coordinateAxisCircleLine, currentImageCenter, radiusAngle, currentBitmapSource);

            selectedCircleLine = coordinateAxisCircleLine;
            SetReferencePlotLimits();
            UpdateReferencePlotHeader();
            UpdatePlotForCircle();
        }

        private void UpdateReferencePlotHeader()
        {
            var axisParam = CurrentModelProfile.CoordinateAxisParam;
            tbReferenceMode.Text = axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine ? "方位角直线" : "极角圆";
            tbReferenceValue.Text = GetReferenceValueText(axisParam.ReferenceMode, axisParam.ReferenceAngle, axisParam.ReferenceRadiusAngle);
            ReferencePlotPane.Title = axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine ? "方位角" : "极角";
        }

        private static string GetReferenceValueText(ConoscopeCoordinateReferenceMode mode, double angle, double radiusAngle)
        {
            return mode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? $"{angle:F2}°"
                : $"R={radiusAngle:F2}°";
        }

        private void SetReferencePlotLimits()
        {
            if (CurrentModelProfile.CoordinateAxisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                wpfPlotReference.Plot.Axes.SetLimits(-MaxAngle, MaxAngle, 0, 600);
            }
            else
            {
                wpfPlotReference.Plot.Axes.SetLimits(0, 360, 0, 600);
            }
        }

        private void UpdateReferencePlot()
        {
            if (CurrentModelProfile.CoordinateAxisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                UpdatePlot();
            }
            else
            {
                UpdatePlotForCircle();
            }
        }


        private void DisposeCoordinateAxis()
        {
            if (coordinateAxisController == null)
            {
                return;
            }

            coordinateAxisController.ReferenceChanged -= CoordinateAxisController_ReferenceChanged;
            coordinateAxisController.PointerMoved -= CoordinateAxisController_PointerMoved;
            coordinateAxisController.PointerLeft -= CoordinateAxisController_PointerLeft;
            coordinateAxisController.Axis.Attribute.PropertyChanged -= CoordinateAxisParam_PropertyChanged;
            coordinateAxisController.Dispose();
            coordinateAxisController = null;
            coordinateAxisPolarLine = null;
            coordinateAxisCircleLine = null;
        }
        
        /// <summary>
        /// 创建极角线并进行分析
        /// </summary>
        private void CreateAndAnalyzePolarLines()
        {
            try
            {
                if (ImageView.ImageShow.Source == null)
                {
                    log.Warn("图像未加载，无法创建极角线");
                    return;
                }

                BitmapSource bitmapSource = ImageView.ImageShow.Source as BitmapSource;
                if (bitmapSource == null)
                {
                    log.Error("无法获取图像源");
                    return;
                }

                int imageWidth = bitmapSource.PixelWidth;
                int imageHeight = bitmapSource.PixelHeight;

                currentPixelsPerDegree = CurrentModelProfile.GetConoscopeCoefficient(imageWidth, imageHeight);
                int radius = (int)Math.Round(MaxAngle * currentPixelsPerDegree);

                Point center = new Point(imageWidth / 2.0, imageHeight / 2.0);

                // Store current image state for dynamic angle addition
                currentBitmapSource = bitmapSource;
                currentImageCenter = center;
                currentImageRadius = radius;

                InitializeCoordinateAxis(center, radius);

                log.Info($"图像尺寸: {imageWidth}x{imageHeight}, 中心: ({center.X}, {center.Y}), 半径: {radius}, 系数: {currentPixelsPerDegree:F6}px/deg");

                ClearDisplayedCircles();

                polarAngleLines.Clear();
                selectedPolarLine = null;
                coordinateAxisPolarLine = null;

                coordinateAxisController?.BringToFront();
                ApplyCoordinateAxisReference();
            }
            catch (Exception ex)
            {
                log.Error($"创建极角线失败: {ex.Message}", ex);
                MessageBox.Show($"创建极角线失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清除所有显示的极角
        /// </summary>
        private void ClearDisplayedCircles()
        {
            selectedCircleLine = null;
            coordinateAxisCircleLine = null;
        }

        /// <summary>
        /// 按角度模式导出按钮点击事件
        /// </summary>
        private void btnExportAngleMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get selected export channel
                ExportChannel channel = GetSelectedExportChannel();

                // Open save file dialog
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"DiameterLine_Export_{channel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ConoscopeExportService.ExportAngleModeToCsv(saveFileDialog.FileName, channel, CreateExportContext());
                    MessageBox.Show($"数据已成功导出到:\n{saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"成功导出方位角模式CSV: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"方位角模式导出失败: {ex.Message}", ex);
                MessageBox.Show($"方位角模式导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 按极角模式导出按钮点击事件
        /// </summary>
        private void btnExportCircleMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get selected export channel
                ExportChannel channel = GetSelectedExportChannel();

                // Open save file dialog
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"RCircle_Export_{channel}_{ConoscopeConfig.CurrentModel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ConoscopeExportService.ExportCircleModeToCsv(saveFileDialog.FileName, channel, CreateExportContext());
                    MessageBox.Show($"数据已成功导出到:\n{saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"成功导出极角模式CSV: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"极角模式导出失败: {ex.Message}", ex);
                MessageBox.Show($"极角模式导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取选中的导出通道
        /// </summary>
        private ExportChannel GetSelectedExportChannel()
        {
            if (cbExportChannel.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string channelTag)
            {
                if (Enum.TryParse<ExportChannel>(channelTag, out var channel))
                {
                    return channel;
                }
            }
            return ExportChannel.Y;
        }

        private ConoscopeExportContext CreateExportContext()
        {
            if (YMat == null)
            {
                throw new InvalidOperationException("XYZ 数据未加载");
            }

            double pixelsPerDegree = currentPixelsPerDegree > 0
                ? currentPixelsPerDegree
                : CurrentModelProfile.GetConoscopeCoefficient(YMat.Width, YMat.Height);

            return new ConoscopeExportContext
            {
                ModelName = ConoscopeConfig.CurrentModel.ToString(),
                ImageWidth = YMat.Width,
                ImageHeight = YMat.Height,
                Center = currentImageCenter,
                MaxAngle = MaxAngle,
                PixelsPerDegree = pixelsPerDegree,
                ReadXyz = (ix, iy) =>
                {
                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);
                    return new ConoscopeXyzValue(X, Y, Z);
                }
            };
        }

        /// <summary>
        /// 获取指定通道的值
        /// </summary>
        private static double GetChannelValue(RgbSample sample, ExportChannel channel)
        {
            return GetChannelValue(sample.X, sample.Y, sample.Z, channel);
        }

        private static double GetChannelValue(double X, double Y, double Z, ExportChannel channel)
        {
            return ConoscopeColorimetry.GetChannelValue(X, Y, Z, channel);
        }

        private static string GetChannelLabel(ExportChannel channel)
        {
            return ConoscopeColorimetry.GetChannelLabel(channel);
        }

        private static ScottPlot.Color GetPlotColor(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => ScottPlot.Color.FromColor(System.Drawing.Color.Gold),
                ExportChannel.Y => ScottPlot.Color.FromColor(System.Drawing.Color.DimGray),
                ExportChannel.Z => ScottPlot.Color.FromColor(System.Drawing.Color.Violet),
                ExportChannel.CieX => ScottPlot.Color.FromColor(System.Drawing.Color.OrangeRed),
                ExportChannel.CieY => ScottPlot.Color.FromColor(System.Drawing.Color.SeaGreen),
                ExportChannel.CieU => ScottPlot.Color.FromColor(System.Drawing.Color.DodgerBlue),
                ExportChannel.CieV => ScottPlot.Color.FromColor(System.Drawing.Color.MediumPurple),
                _ => ScottPlot.Color.FromColor(System.Drawing.Color.DimGray)
            };
        }

        private static string FormatChannelValue(double value, ExportChannel channel)
        {
            return ConoscopeColorimetry.FormatChannelValue(value, channel);
        }


        /// <summary>
        /// 直接从XMat/YMat/ZMat提取XYZ通道值（参考VAMdemo简洁方式）
        /// </summary>
        private void ExtractXYZValues(int ix, int iy, out double X, out double Y, out double Z)
        {
            X = Y = Z = 0;
            if (XMat != null)
                X = XMat.At<float>(iy, ix);
            if (YMat != null)
                Y = YMat.At<float>(iy, ix);
            if (ZMat != null)
                Z = ZMat.At<float>(iy, ix);
        }


        /// <summary>
        /// 沿线提取RGB数据
        /// </summary>
        private void ExtractRgbAlongLine(PolarAngleLine polarLine, Point start, Point end, BitmapSource bitmapSource, int radius)
        {
            try
            {
                if (YMat == null) return;

                int imageWidth = YMat.Width;
                int imageHeight = YMat.Height;

                // Calculate line length
                double lineLength = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
                int numSamples = (int)lineLength;

                if (numSamples <= 1)
                {
                    log.Warn($"线长度太短 ({numSamples} 像素)，无法采样");
                    return;
                }

                // Sample points along the line
                for (int i = 0; i < numSamples; i++)
                {
                    double t = i / (double)(numSamples - 1);
                    double x = start.X + t * (end.X - start.X);
                    double y = start.Y + t * (end.Y - start.Y);

                    int ix = Math.Max(0, Math.Min(imageWidth - 1, (int)Math.Round(x)));
                    int iy = Math.Max(0, Math.Min(imageHeight - 1, (int)Math.Round(y)));

                    double position = -MaxAngle + (i / (double)(numSamples - 1)) * MaxAngle * 2;

                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);

                    polarLine.RgbData.Add(new RgbSample
                    {
                        Position = position,
                        DX = ix,
                        DY = iy,
                        X = X,
                        Y = Y,
                        Z = Z,
                    });
                }

                log.Info($"完成采样: 方位角{polarLine.Angle}°, 采样点数{polarLine.RgbData.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取数据失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 沿圆周提取RGB数据
        /// </summary>
        private void ExtractRgbAlongCircle(ConcentricCircleLine circleLine, Point center, double radiusAngle, BitmapSource bitmapSource)
        {
            try
            {
                if (YMat == null) return;

                int imageWidth = YMat.Width;
                int imageHeight = YMat.Height;

                // Calculate radius in pixels
                double radiusPixels = radiusAngle * currentPixelsPerDegree;

                int numSamples = 360;
                for (int i = 0; i < numSamples; i++)
                {
                    double anglePos = i * 360.0 / numSamples;
                    double radians = (90 - anglePos) * Math.PI / 180.0;
                    double x = center.X + radiusPixels * Math.Cos(radians);
                    double y = center.Y + radiusPixels * Math.Sin(radians);

                    int ix = Math.Max(0, Math.Min(imageWidth - 1, (int)Math.Round(x)));
                    int iy = Math.Max(0, Math.Min(imageHeight - 1, (int)Math.Round(y)));

                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);

                    circleLine.RgbData.Add(new RgbSample
                    {
                        Position = anglePos,
                        X = X,
                        Y = Y,
                        Z = Z
                    });
                }

                log.Info($"完成采样: 极角半径角度{circleLine.RadiusAngle}°, 采样点数{circleLine.RgbData.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取极角数据失败: {ex.Message}", ex);
            }
        }


        /// <summary>
        /// 更新ScottPlot显示
        /// </summary>
        private void UpdatePlot()
        {
            try
            {
                wpfPlotReference.Plot.Clear();

                if (selectedPolarLine == null || selectedPolarLine.RgbData.Count == 0)
                {
                    wpfPlotReference.Refresh();
                    return;
                }

                double[] positions = selectedPolarLine.RgbData.Select(s => s.Position).ToArray();
                ExportChannel channel = GetSelectedDisplayChannel();
                double[] values = selectedPolarLine.RgbData.Select(s => GetChannelValue(s, channel)).ToArray();
                var scatter = wpfPlotReference.Plot.Add.Scatter(positions, values);
                scatter.Color = GetPlotColor(channel);
                scatter.LineWidth = 2;
                scatter.LegendText = GetChannelLabel(channel);

                wpfPlotReference.Plot.Title($"方位角 {selectedPolarLine.Angle}° {GetChannelLabel(channel)}分布曲线");
                wpfPlotReference.Plot.XLabel("角度 (°)");
                wpfPlotReference.Plot.YLabel(GetChannelLabel(channel));
                wpfPlotReference.Plot.Legend.IsVisible = true;
                wpfPlotReference.Plot.Axes.AutoScale();

                wpfPlotReference.Refresh();

                log.Info($"更新图表: 方位角{selectedPolarLine.Angle}°");
            }
            catch (Exception ex)
            {
                log.Error($"更新图表失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 更新ScottPlot显示极角数据
        /// </summary>
        private void UpdatePlotForCircle()
        {
            try
            {
                wpfPlotReference.Plot.Clear();

                if (selectedCircleLine == null || selectedCircleLine.RgbData.Count == 0)
                {
                    wpfPlotReference.Refresh();
                    return;
                }

                double[] positions = selectedCircleLine.RgbData.Select(s => s.Position).ToArray();
                ExportChannel channel = GetSelectedDisplayChannel();
                double[] values = selectedCircleLine.RgbData.Select(s => GetChannelValue(s, channel)).ToArray();
                var scatter = wpfPlotReference.Plot.Add.Scatter(positions, values);
                scatter.Color = GetPlotColor(channel);
                scatter.LineWidth = 2;
                scatter.LegendText = GetChannelLabel(channel);

                wpfPlotReference.Plot.Title($"极角 {selectedCircleLine.RadiusAngle}° {GetChannelLabel(channel)}圆周分布曲线");
                wpfPlotReference.Plot.XLabel("圆周角度 (°)");
                wpfPlotReference.Plot.YLabel(GetChannelLabel(channel));
                wpfPlotReference.Plot.Legend.IsVisible = true;
                wpfPlotReference.Plot.Axes.AutoScale();

                wpfPlotReference.Refresh();

                log.Info($"更新图表: 极角半径角度{selectedCircleLine.RadiusAngle}°");
            }
            catch (Exception ex)
            {
                log.Error($"更新极角图表失败: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            XMat?.Dispose();
            XMat = null;
            YMat?.Dispose();
            YMat = null;
            ZMat?.Dispose();
            ZMat = null;
            DisposeCoordinateAxis();
            ImageView?.Dispose();
            GC.SuppressFinalize(this);
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            LayoutManager?.SaveLayout();
            this.Dispose();
        }

        private void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            FlowEngineManager.GetInstance().DisplayFlow.RunFlow();
        }

        /// <summary>
        /// 高级导出按钮点击事件
        /// </summary>
        private void btnAdvancedExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new AdvancedExportDialog { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    var settings = dialog.Settings;
                    PerformAdvancedExport(settings);
                }
            }
            catch (Exception ex)
            {
                log.Error($"高级导出失败: {ex.Message}", ex);
                MessageBox.Show($"高级导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行高级导出
        /// </summary>
        private void PerformAdvancedExport(AdvancedExportSettings settings)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                int filesExported = 0;

                // Select output folder
                using (var folderDialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    folderDialog.Description = "选择导出文件夹";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    {
                        return; // User cancelled
                    }

                    string outputFolder = folderDialog.SelectedPath;

                    // Handle cross-section export separately
                    if (settings.EnableCrossSection)
                    {
                        ExportCrossSectionToFolder(settings, timestamp, outputFolder, ref filesExported);
                        MessageBox.Show($"截面导出完成，共导出 {filesExported} 个文件到:\n{outputFolder}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        log.Info($"截面导出完成: {filesExported} 个文件");
                        return;
                    }

                    // Export azimuth data
                    if (settings.ExportAzimuth)
                    {
                        ConoscopeExportContext exportContext = CreateExportContext();
                        foreach (var channel in settings.Channels)
                        {
                            string filename = $"{settings.FilePrefix}_Azimuth_{channel}_{timestamp}.csv";
                            string filePath = Path.Combine(outputFolder, filename);
                            ConoscopeExportService.ExportAzimuthWithStep(filePath, channel, exportContext, settings.AzimuthStep, settings.RadialStep);
                            filesExported++;
                            log.Info($"方位角导出成功: {filePath}");
                        }
                    }

                    // Export polar data
                    if (settings.ExportPolar)
                    {
                        ConoscopeExportContext exportContext = CreateExportContext();
                        foreach (var channel in settings.Channels)
                        {
                            string filename = $"{settings.FilePrefix}_Polar_{channel}_{ConoscopeConfig.CurrentModel}_{timestamp}.csv";
                            string filePath = Path.Combine(outputFolder, filename);
                            ConoscopeExportService.ExportPolarWithStep(filePath, channel, exportContext, settings.PolarStep, settings.CircumferentialStep);
                            filesExported++;
                            log.Info($"极角导出成功: {filePath}");
                        }
                    }

                    MessageBox.Show($"导出完成，共导出 {filesExported} 个文件到:\n{outputFolder}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"高级导出完成: {filesExported} 个文件");
                }
            }
            catch (Exception ex)
            {
                log.Error($"高级导出执行失败: {ex.Message}", ex);
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出截面数据到文件夹
        /// </summary>
        private void ExportCrossSectionToFolder(AdvancedExportSettings settings, string timestamp, string outputFolder, ref int filesExported)
        {
            try
            {
                ConoscopeExportContext exportContext = CreateExportContext();
                foreach (var channel in settings.Channels)
                {
                    string sectionType = settings.CrossSectionType == CrossSectionType.Azimuth ? "Azimuth" : "Polar";
                    string filename = $"{settings.FilePrefix}_CrossSection_{sectionType}_{settings.CrossSectionAngle}deg_{channel}_{timestamp}.csv";
                    string filePath = Path.Combine(outputFolder, filename);
                    
                    if (settings.CrossSectionType == CrossSectionType.Azimuth)
                    {
                        ConoscopeExportService.ExportAzimuthCrossSection(filePath, channel, exportContext, settings.CrossSectionAngle);
                    }
                    else
                    {
                        ConoscopeExportService.ExportPolarCrossSection(filePath, channel, exportContext, settings.CrossSectionAngle);
                    }
                    filesExported++;
                    log.Info($"截面导出成功: {filePath}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"截面导出失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 导出当前参考曲线
        /// </summary>
        private void btnExportCurrentReference_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentModelProfile.CoordinateAxisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                btnExportCurrentAzimuth_Click(sender, e);
            }
            else
            {
                btnExportCurrentPolar_Click(sender, e);
            }
        }

        /// <summary>
        /// 导出当前选中的方位角
        /// </summary>
        private void btnExportCurrentAzimuth_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedPolarLine == null)
                {
                    MessageBox.Show("请先选择一个方位角", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (YMat == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get channel selection
                var channel = GetSelectedExportChannel();
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"Azimuth_{selectedPolarLine.Angle}deg_{channel}_{timestamp}.csv";

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = filename,
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ConoscopeExportService.ExportAzimuthCrossSection(saveFileDialog.FileName, channel, CreateExportContext(), selectedPolarLine.Angle);
                    MessageBox.Show($"方位角 {selectedPolarLine.Angle}° 导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"单个方位角导出成功: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"导出当前方位角失败: {ex.Message}", ex);
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出当前选中的极角
        /// </summary>
        private void btnExportCurrentPolar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedCircleLine == null)
                {
                    MessageBox.Show("请先选择一个极角", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (YMat == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get channel selection
                var channel = GetSelectedExportChannel();
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"Polar_{selectedCircleLine.RadiusAngle}deg_{channel}_{timestamp}.csv";

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = filename,
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ConoscopeExportService.ExportPolarCrossSection(saveFileDialog.FileName, channel, CreateExportContext(), selectedCircleLine.RadiusAngle);
                    MessageBox.Show($"极角 {selectedCircleLine.RadiusAngle}° 导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"单个极角导出成功: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"导出当前极角失败: {ex.Message}", ex);
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
