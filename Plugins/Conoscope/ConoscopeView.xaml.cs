using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using OpenCvSharp.WpfExtensions;
using Conoscope.Core;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Conoscope
{
    /// <summary>
    /// ConoscopeView.xaml 的交互逻辑
    /// </summary>
    public partial class ConoscopeView : UserControl, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeView));

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
        private bool isUpdatingQuickControls;
        private OpenCvSharp.Mat? colorDifferenceReferenceUMat;
        private OpenCvSharp.Mat? colorDifferenceReferenceVMat;
        private string? colorDifferenceReferenceFileName;
        private bool isUpdatingColorDifferenceControls;
        private bool isUpdatingFilterControls;
        private WindowCIE? cieWindow;

        public double MaxAngle => ConoscopeConfig.CurrentModelProfile.MaxAngle;

        public ConoscopeModelProfile CurrentModelProfile => ConoscopeConfig.CurrentModelProfile;
        public string FileName => Filename;

        private void RefreshReferenceLineProfileBinding()
        {
            GridSetting.Children.Clear();
            GridSetting.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(CurrentModelProfile.CoordinateAxisParam));
        }


        private void RefreshModelDependentUi()
        {
            RefreshReferenceLineProfileBinding();
            RefreshQuickControlsFromAxisParam();
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

        public ConoscopeView()
        {
            InitializeComponent();
            ConoscopeModuleService.Register(this);
        }

        public ConoscopeConfig ConoscopeConfig => ConoscopeManager.GetInstance().Config;

        private void Window_Initialized(object sender, EventArgs e)
        {
            RefreshReferenceLineProfileBinding();

            this.DataContext = ConoscopeManager.GetInstance();
            SelectComboBoxItemByTag(cbDisplayChannel, ConoscopeConfig.DisplayChannel.ToString());
            RefreshQuickControlsFromAxisParam();
            InitializeColorDifferenceControls();
            InitializeFilterControls();
            UpdateReferenceControlVisibility();
            UpdateColorDifferencePanelVisibility();

            ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig_ModelTypeChanged(sender, ConoscopeConfig.CurrentModel);
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

        private void InitializeFilterControls()
        {
            isUpdatingFilterControls = true;
            try
            {
                MigrateLegacyDustRemovalFilterType();
                SelectComboBoxItemByTag(cbFilterType, NormalizeFilterType(ConoscopeConfig.FilterType).ToString());
                SelectComboBoxItemByTag(cbDustMode, ConoscopeConfig.DustRemovalMode.ToString());
                chkDustRemovalEnabled.IsChecked = ConoscopeConfig.DustRemovalEnabled;

                sliderKernelSize.Value = ConoscopeConfig.FilterKernelSize;
                sliderSigma.Value = ConoscopeConfig.FilterSigma;
                sliderD.Value = ConoscopeConfig.FilterD;
                sliderSigmaColor.Value = ConoscopeConfig.FilterSigmaColor;
                sliderSigmaSpace.Value = ConoscopeConfig.FilterSigmaSpace;
                sliderDustThreshold.Value = ConoscopeConfig.DustThresholdPercent;
                sliderDustMinArea.Value = ConoscopeConfig.DustMinArea;
                sliderDustMaxArea.Value = Math.Max(ConoscopeConfig.DustMinArea, ConoscopeConfig.DustMaxArea);
                sliderDustRepairRadius.Value = ConoscopeConfig.DustRepairRadius;
            }
            finally
            {
                isUpdatingFilterControls = false;
            }

            UpdateFilterParameterVisibility(GetSelectedFilterType());
        }

        private void FilterParameter_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingFilterControls || !IsInitialized)
            {
                return;
            }

            SaveFilterControlsToConfig();
            UpdateFilterParameterVisibility(GetSelectedFilterType());
        }

        private void SaveFilterControlsToConfig()
        {
            ConoscopeConfig.FilterType = NormalizeFilterType(GetSelectedFilterType());
            ConoscopeConfig.FilterKernelSize = NormalizeKernelSize((int)(sliderKernelSize?.Value ?? ConoscopeConfig.FilterKernelSize));
            ConoscopeConfig.FilterSigma = sliderSigma?.Value ?? ConoscopeConfig.FilterSigma;
            ConoscopeConfig.FilterD = Math.Max(1, (int)(sliderD?.Value ?? ConoscopeConfig.FilterD));
            ConoscopeConfig.FilterSigmaColor = sliderSigmaColor?.Value ?? ConoscopeConfig.FilterSigmaColor;
            ConoscopeConfig.FilterSigmaSpace = sliderSigmaSpace?.Value ?? ConoscopeConfig.FilterSigmaSpace;
            ConoscopeConfig.DustRemovalEnabled = IsDustRemovalEnabled();
            ConoscopeConfig.DustRemovalMode = GetSelectedDustRemovalMode();
            ConoscopeConfig.DustThresholdPercent = sliderDustThreshold?.Value ?? ConoscopeConfig.DustThresholdPercent;
            ConoscopeConfig.DustMinArea = Math.Max(1, (int)(sliderDustMinArea?.Value ?? ConoscopeConfig.DustMinArea));
            ConoscopeConfig.DustMaxArea = Math.Max(ConoscopeConfig.DustMinArea, (int)(sliderDustMaxArea?.Value ?? ConoscopeConfig.DustMaxArea));
            ConoscopeConfig.DustRepairRadius = Math.Max(1, (int)(sliderDustRepairRadius?.Value ?? ConoscopeConfig.DustRepairRadius));
        }

        private void MigrateLegacyDustRemovalFilterType()
        {
            const int legacyDustRemovalFilterValue = 6;
            if ((int)ConoscopeConfig.FilterType == legacyDustRemovalFilterValue)
            {
                ConoscopeConfig.DustRemovalEnabled = true;
                ConoscopeConfig.FilterType = ImageFilterType.None;
            }
        }

        private void InitializeColorDifferenceControls()
        {
            isUpdatingColorDifferenceControls = true;
            try
            {
                SelectComboBoxItemByTag(cbColorDifferenceReference, ConoscopeConfig.ColorDifferenceReferenceMode.ToString());
                txtColorDifferenceCustomU.Text = ConoscopeConfig.ColorDifferenceCustomU.ToString("F4", CultureInfo.InvariantCulture);
                txtColorDifferenceCustomV.Text = ConoscopeConfig.ColorDifferenceCustomV.ToString("F4", CultureInfo.InvariantCulture);
            }
            finally
            {
                isUpdatingColorDifferenceControls = false;
            }

            UpdateColorDifferenceReferenceUi();
        }

        private void UpdateColorDifferencePanelVisibility()
        {
            if (gbColorDifference == null)
            {
                return;
            }

            gbColorDifference.Visibility = Visibility.Visible;
        }

        private ColorDifferenceReferenceMode GetSelectedColorDifferenceReferenceMode()
        {
            if (cbColorDifferenceReference?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string modeTag &&
                Enum.TryParse(modeTag, out ColorDifferenceReferenceMode mode))
            {
                return mode;
            }

            return ConoscopeConfig.ColorDifferenceReferenceMode;
        }

        private static ConoscopeUvReference GetStandardColorDifferenceReference(ColorDifferenceReferenceMode mode)
        {
            return mode switch
            {
                ColorDifferenceReferenceMode.D65 => new ConoscopeUvReference(0.1978, 0.4684),
                ColorDifferenceReferenceMode.D50 => new ConoscopeUvReference(0.2009, 0.4707),
                ColorDifferenceReferenceMode.A => new ConoscopeUvReference(0.2560, 0.5242),
                ColorDifferenceReferenceMode.D75 => new ConoscopeUvReference(0.1952, 0.4670),
                _ => throw new InvalidOperationException("当前色差基准不是固定光源")
            };
        }

        private bool TryParseCustomColorDifferenceReference(out ConoscopeUvReference reference)
        {
            reference = default;
            if (!TryParseDouble(txtColorDifferenceCustomU?.Text, out double u) || !TryParseDouble(txtColorDifferenceCustomV?.Text, out double v))
            {
                return false;
            }

            ConoscopeConfig.ColorDifferenceCustomU = u;
            ConoscopeConfig.ColorDifferenceCustomV = v;
            reference = new ConoscopeUvReference(u, v);
            return true;
        }

        private static bool TryParseDouble(string? text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private ConoscopeUvReference ResolvePointColorDifferenceReference()
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode is ColorDifferenceReferenceMode.D65 or ColorDifferenceReferenceMode.D50 or ColorDifferenceReferenceMode.A or ColorDifferenceReferenceMode.D75)
            {
                return GetStandardColorDifferenceReference(mode);
            }

            if (mode == ColorDifferenceReferenceMode.Custom)
            {
                if (!TryParseCustomColorDifferenceReference(out ConoscopeUvReference customReference))
                {
                    throw new InvalidOperationException("请输入有效的自定义 u/v 基准坐标");
                }

                return customReference;
            }

            if (mode == ColorDifferenceReferenceMode.ImageCenter)
            {
                return CalculateImageCenterColorDifferenceReference();
            }

            throw new InvalidOperationException("实测基准图需要保存基准图后逐点计算");
        }

        private ConoscopeUvReference CalculateImageCenterColorDifferenceReference()
        {
            if (!HasXyzData() || XMat == null || YMat == null || ZMat == null)
            {
                throw new InvalidOperationException("请先加载图像");
            }

            int centerX = XMat.Width / 2;
            int centerY = XMat.Height / 2;
            const int roiRadius = 25;
            double sumU = 0;
            double sumV = 0;
            int count = 0;

            int startY = Math.Max(0, centerY - roiRadius);
            int endY = Math.Min(XMat.Height - 1, centerY + roiRadius);
            int startX = Math.Max(0, centerX - roiRadius);
            int endX = Math.Min(XMat.Width - 1, centerX + roiRadius);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy > roiRadius * roiRadius)
                    {
                        continue;
                    }

                    ExtractXYZValues(x, y, out double X, out double Y, out double Z);
                    ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(X, Y, Z);
                    sumU += chromaticity.u;
                    sumV += chromaticity.v;
                    count++;
                }
            }

            if (count == 0)
            {
                throw new InvalidOperationException("图像中心 50px 关注点内没有可用像素");
            }

            return new ConoscopeUvReference(sumU / count, sumV / count);
        }

        private void UpdateColorDifferenceReferenceUi()
        {
            if (panelColorDifferenceCustomUv == null || tbColorDifferenceReferenceStatus == null || btnSaveColorDifferenceReference == null)
            {
                return;
            }

            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            panelColorDifferenceCustomUv.Visibility = mode == ColorDifferenceReferenceMode.Custom ? Visibility.Visible : Visibility.Collapsed;
            tbColorDifferenceReferenceStatus.Text = GetColorDifferenceReferenceStatusText(mode);

            if (colorDifferenceReferenceUMat != null && colorDifferenceReferenceVMat != null)
            {
                btnSaveColorDifferenceReference.Content = "基准图已保存";
                btnSaveColorDifferenceReference.Background = Brushes.LightGreen;
                btnSaveColorDifferenceReference.Foreground = Brushes.Black;
            }
            else
            {
                btnSaveColorDifferenceReference.Content = "保存色差基准图";
                btnSaveColorDifferenceReference.ClearValue(BackgroundProperty);
                btnSaveColorDifferenceReference.ClearValue(ForegroundProperty);
            }
        }

        private string GetColorDifferenceReferenceStatusText(ColorDifferenceReferenceMode mode)
        {
            return mode switch
            {
                ColorDifferenceReferenceMode.D65 => "D65: u=0.1978, v=0.4684",
                ColorDifferenceReferenceMode.D50 => "D50: u=0.2009, v=0.4707",
                ColorDifferenceReferenceMode.A => "A: u=0.2560, v=0.5242",
                ColorDifferenceReferenceMode.D75 => "D75: u=0.1952, v=0.4670",
                ColorDifferenceReferenceMode.ImageCenter => "基准: 当前图像中心直径 50px 关注点平均 uv",
                ColorDifferenceReferenceMode.Custom => $"自定义: u={ConoscopeConfig.ColorDifferenceCustomU:F4}, v={ConoscopeConfig.ColorDifferenceCustomV:F4}",
                ColorDifferenceReferenceMode.ReferenceImage => colorDifferenceReferenceUMat == null
                    ? "基准: 尚未保存实测基准图"
                    : $"基准图: {Path.GetFileName(colorDifferenceReferenceFileName)}",
                _ => string.Empty
            };
        }

        private void RefreshQuickControlsFromAxisParam()
        {
            if (cbQuickReferenceMode == null || sliderQuickReferenceAngle == null || sliderQuickReferenceRadius == null)
            {
                return;
            }

            ConoscopeCoordinateAxisParam axisParam = CurrentModelProfile.CoordinateAxisParam;

            isUpdatingQuickControls = true;
            try
            {
                SelectComboBoxItemByTag(cbQuickReferenceMode, axisParam.ReferenceMode.ToString());
                sliderQuickReferenceAngle.Value = axisParam.ReferenceAngle;
                sliderQuickReferenceRadius.Maximum = MaxAngle;
                sliderQuickReferenceRadius.Value = Math.Max(0, Math.Min(axisParam.ReferenceRadiusAngle, MaxAngle));
            }
            finally
            {
                isUpdatingQuickControls = false;
            }

            UpdateReferenceControlVisibility();
        }

        private void UpdateReferenceControlVisibility()
        {
            if (rowReferenceAngle == null || rowReferenceRadius == null)
            {
                return;
            }

            ConoscopeCoordinateReferenceMode mode = CurrentModelProfile.CoordinateAxisParam.ReferenceMode;
            rowReferenceAngle.Visibility = mode == ConoscopeCoordinateReferenceMode.AzimuthLine ? Visibility.Visible : Visibility.Collapsed;
            rowReferenceRadius.Visibility = mode == ConoscopeCoordinateReferenceMode.PolarCircle ? Visibility.Visible : Visibility.Collapsed;
        }

        private void QuickReferenceMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingQuickControls || cbQuickReferenceMode?.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            if (Enum.TryParse(item.Tag?.ToString(), out ConoscopeCoordinateReferenceMode mode))
            {
                CurrentModelProfile.CoordinateAxisParam.ReferenceMode = mode;
                UpdateReferenceControlVisibility();
                ApplyCoordinateAxisReference();
            }
        }

        private void QuickReferenceAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdatingQuickControls || !IsInitialized)
            {
                return;
            }

            ConoscopeCoordinateAxisParam axisParam = CurrentModelProfile.CoordinateAxisParam;
            axisParam.ReferenceAngle = e.NewValue;
            if (axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                ApplyCoordinateAxisReference();
            }
        }

        private void QuickReferenceRadius_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdatingQuickControls || !IsInitialized)
            {
                return;
            }

            ConoscopeCoordinateAxisParam axisParam = CurrentModelProfile.CoordinateAxisParam;
            axisParam.ReferenceRadiusAngle = Math.Max(0, Math.Min(e.NewValue, MaxAngle));
            if (axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.PolarCircle)
            {
                ApplyCoordinateAxisReference();
            }
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
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

        private void cbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbFilterType == null) return;
            
            var selectedFilter = GetSelectedFilterType();

            if (!isUpdatingFilterControls)
            {
                SaveFilterControlsToConfig();
            }

            UpdateFilterParameterVisibility(selectedFilter);

            if (sliderKernelSize != null && sliderSigma != null && sliderD != null && sliderSigmaColor != null && sliderSigmaSpace != null)
            {
                sliderKernelSize.IsEnabled = false;
                sliderSigma.IsEnabled = false;
                sliderD.IsEnabled = false;
                sliderSigmaColor.IsEnabled = false;
                sliderSigmaSpace.IsEnabled = false;

                switch (selectedFilter)
                {
                    case ImageFilterType.None:
                        break;
                    case ImageFilterType.LowPass:
                    case ImageFilterType.MovingAverage:
                    case ImageFilterType.Median:
                        sliderKernelSize.IsEnabled = true;
                        break;
                    case ImageFilterType.Gaussian:
                        sliderKernelSize.IsEnabled = true;
                        sliderSigma.IsEnabled = true;
                        break;
                    case ImageFilterType.Bilateral:
                        sliderD.IsEnabled = true;
                        sliderSigmaColor.IsEnabled = true;
                        sliderSigmaSpace.IsEnabled = true;
                        break;
                }
            }
        }

        private void UpdateFilterParameterVisibility(ImageFilterType selectedFilter)
        {
            if (rowFilterKernel == null || rowFilterSigma == null || rowFilterD == null || rowFilterSigmaColor == null || rowFilterSigmaSpace == null
                || rowDustMode == null || rowDustThreshold == null || rowDustMinArea == null || rowDustMaxArea == null || rowDustRepairRadius == null)
            {
                return;
            }

            bool showKernel = selectedFilter is ImageFilterType.LowPass or ImageFilterType.MovingAverage or ImageFilterType.Gaussian or ImageFilterType.Median;
            bool showSigma = selectedFilter == ImageFilterType.Gaussian;
            bool showBilateral = selectedFilter == ImageFilterType.Bilateral;
            bool showDust = IsDustRemovalEnabled();

            rowFilterKernel.Visibility = showKernel ? Visibility.Visible : Visibility.Collapsed;
            rowFilterSigma.Visibility = showSigma ? Visibility.Visible : Visibility.Collapsed;
            rowFilterD.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            rowFilterSigmaColor.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            rowFilterSigmaSpace.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            rowDustMode.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustThreshold.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustMinArea.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustMaxArea.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustRepairRadius.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;

            if (sliderDustThreshold != null && sliderDustMinArea != null && sliderDustMaxArea != null && sliderDustRepairRadius != null && cbDustMode != null)
            {
                sliderDustThreshold.IsEnabled = showDust;
                sliderDustMinArea.IsEnabled = showDust;
                sliderDustMaxArea.IsEnabled = showDust;
                sliderDustRepairRadius.IsEnabled = showDust;
                cbDustMode.IsEnabled = showDust;
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
                SaveFilterControlsToConfig();

                if (!HasXyzData())
                {
                    MessageBox.Show("请先获取图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!HasPreprocessEnabled())
                {
                    RestoreOriginalMats();
                    RefreshDisplayedImage();
                    log.Info("已恢复原始数据");
                    MessageBox.Show("已恢复原始数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                RestoreOriginalMats();
                log.Info($"开始应用预处理: dust={ConoscopeConfig.DustRemovalEnabled}, filter={ConoscopeConfig.FilterType}");
                ApplyPreprocessToCurrentMats();
                RefreshDisplayedImage();

                log.Info("预处理应用成功，数据已更新");
                MessageBox.Show("预处理应用成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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

        string Filename = string.Empty;
        public void OpenConoscope(string filename)
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
                    ApplyPreprocessToCurrentMats();
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

        private void ApplyPreprocessToCurrentMats()
        {
            SaveFilterControlsToConfig();

            if (ConoscopeConfig.DustRemovalEnabled)
            {
                ApplyDustRemovalToCurrentMats();
            }

            ImageFilterType filterType = NormalizeFilterType(ConoscopeConfig.FilterType);
            if (filterType != ImageFilterType.None)
            {
                ApplyFilterToCurrentMats(filterType);
            }
        }

        private bool HasPreprocessEnabled()
        {
            return ConoscopeConfig.DustRemovalEnabled || NormalizeFilterType(ConoscopeConfig.FilterType) != ImageFilterType.None;
        }

        private void ApplyFilterToCurrentMats(ImageFilterType filterType)
        {
            if (filterType == ImageFilterType.None)
            {
                return;
            }

            int kernelSize = ConoscopeConfig.FilterKernelSize;
            double sigma = ConoscopeConfig.FilterSigma;
            int d = ConoscopeConfig.FilterD;
            double sigmaColor = ConoscopeConfig.FilterSigmaColor;
            double sigmaSpace = ConoscopeConfig.FilterSigmaSpace;

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

        private void ApplyDustRemovalToCurrentMats()
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                return;
            }

            DustRemovalOptions options = GetDustRemovalOptions();
            int darkComponents;
            int brightComponents;
            using OpenCvSharp.Mat darkMask = ShouldDetectDarkDust(options.Mode)
                ? CreateDustMask(YMat, options, darkSpot: true, out darkComponents)
                : CreateEmptyMask(YMat, out darkComponents);
            using OpenCvSharp.Mat brightMask = ShouldDetectBrightDust(options.Mode)
                ? CreateDustMask(YMat, options, darkSpot: false, out brightComponents)
                : CreateEmptyMask(YMat, out brightComponents);

            int darkPixels = OpenCvSharp.Cv2.CountNonZero(darkMask);
            int brightPixels = OpenCvSharp.Cv2.CountNonZero(brightMask);
            if (darkPixels == 0 && brightPixels == 0)
            {
                log.Info($"灰尘滤除未检测到候选区域: mode={options.Mode}, threshold={options.ThresholdPercent:F1}%");
                return;
            }

            XMat = ReplaceChannelWithDustRepair(XMat, darkMask, brightMask, options);
            YMat = ReplaceChannelWithDustRepair(YMat, darkMask, brightMask, options);
            ZMat = ReplaceChannelWithDustRepair(ZMat, darkMask, brightMask, options);

            log.Info($"灰尘滤除完成: mode={options.Mode}, darkComponents={darkComponents}, brightComponents={brightComponents}, darkPixels={darkPixels}, brightPixels={brightPixels}, threshold={options.ThresholdPercent:F1}%, area={options.MinArea}-{options.MaxArea}, radius={options.RepairRadius}");
        }

        private DustRemovalOptions GetDustRemovalOptions()
        {
            int minArea = Math.Max(1, ConoscopeConfig.DustMinArea);
            int maxArea = Math.Max(minArea, ConoscopeConfig.DustMaxArea);
            return new DustRemovalOptions(
                ConoscopeConfig.DustRemovalMode,
                ConoscopeConfig.DustThresholdPercent,
                minArea,
                maxArea,
                Math.Max(1, ConoscopeConfig.DustRepairRadius));
        }

        private static bool ShouldDetectDarkDust(DustRemovalMode mode)
        {
            return mode is DustRemovalMode.DarkSpot or DustRemovalMode.Both;
        }

        private static bool ShouldDetectBrightDust(DustRemovalMode mode)
        {
            return mode is DustRemovalMode.BrightSpot or DustRemovalMode.Both;
        }

        private static OpenCvSharp.Mat CreateEmptyMask(OpenCvSharp.Mat source, out int componentCount)
        {
            componentCount = 0;
            return new OpenCvSharp.Mat(source.Rows, source.Cols, OpenCvSharp.MatType.CV_8UC1, new OpenCvSharp.Scalar(0));
        }

        private static OpenCvSharp.Mat CreateDustMask(OpenCvSharp.Mat luminance, DustRemovalOptions options, bool darkSpot, out int componentCount)
        {
            using OpenCvSharp.Mat gray8 = NormalizeToGray8(luminance);
            int backgroundKernelSize = NormalizeKernelSize(options.RepairRadius * 2 + 1);
            using OpenCvSharp.Mat kernel = OpenCvSharp.Cv2.GetStructuringElement(
                OpenCvSharp.MorphShapes.Ellipse,
                new OpenCvSharp.Size(backgroundKernelSize, backgroundKernelSize));
            using OpenCvSharp.Mat background = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat diff = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat rawMask = new OpenCvSharp.Mat();

            OpenCvSharp.Cv2.MorphologyEx(gray8, background, darkSpot ? OpenCvSharp.MorphTypes.Close : OpenCvSharp.MorphTypes.Open, kernel);
            if (darkSpot)
            {
                OpenCvSharp.Cv2.Subtract(background, gray8, diff);
            }
            else
            {
                OpenCvSharp.Cv2.Subtract(gray8, background, diff);
            }

            double threshold = Math.Max(1, Math.Min(255, 255.0 * options.ThresholdPercent / 100.0));
            OpenCvSharp.Cv2.Threshold(diff, rawMask, threshold, 255, OpenCvSharp.ThresholdTypes.Binary);

            OpenCvSharp.Mat filteredMask = FilterMaskByArea(rawMask, options.MinArea, options.MaxArea, out componentCount);
            if (componentCount > 0)
            {
                int dilateKernelSize = NormalizeKernelSize(Math.Max(1, options.RepairRadius));
                using OpenCvSharp.Mat dilateKernel = OpenCvSharp.Cv2.GetStructuringElement(
                    OpenCvSharp.MorphShapes.Ellipse,
                    new OpenCvSharp.Size(dilateKernelSize, dilateKernelSize));
                OpenCvSharp.Cv2.Dilate(filteredMask, filteredMask, dilateKernel);
            }

            return filteredMask;
        }

        private static OpenCvSharp.Mat NormalizeToGray8(OpenCvSharp.Mat source)
        {
            OpenCvSharp.Mat normalized = new OpenCvSharp.Mat();
            OpenCvSharp.Mat gray8 = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.Normalize(source, normalized, 0, 255, OpenCvSharp.NormTypes.MinMax);
            normalized.ConvertTo(gray8, OpenCvSharp.MatType.CV_8UC1);
            normalized.Dispose();
            return gray8;
        }

        private static OpenCvSharp.Mat FilterMaskByArea(OpenCvSharp.Mat rawMask, int minArea, int maxArea, out int componentCount)
        {
            OpenCvSharp.Mat filtered = new OpenCvSharp.Mat(rawMask.Rows, rawMask.Cols, OpenCvSharp.MatType.CV_8UC1, new OpenCvSharp.Scalar(0));
            using OpenCvSharp.Mat labels = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat stats = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat centroids = new OpenCvSharp.Mat();

            int labelsCount = OpenCvSharp.Cv2.ConnectedComponentsWithStats(rawMask, labels, stats, centroids);
            componentCount = 0;
            for (int labelIndex = 1; labelIndex < labelsCount; labelIndex++)
            {
                int area = stats.At<int>(labelIndex, 4);
                if (area < minArea || area > maxArea)
                {
                    continue;
                }

                using OpenCvSharp.Mat componentMask = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.InRange(labels, new OpenCvSharp.Scalar(labelIndex), new OpenCvSharp.Scalar(labelIndex), componentMask);
                filtered.SetTo(new OpenCvSharp.Scalar(255), componentMask);
                componentCount++;
            }

            return filtered;
        }

        private static OpenCvSharp.Mat? ReplaceChannelWithDustRepair(OpenCvSharp.Mat? channel, OpenCvSharp.Mat darkMask, OpenCvSharp.Mat brightMask, DustRemovalOptions options)
        {
            if (channel == null)
            {
                return null;
            }

            OpenCvSharp.Mat repaired = ApplyDustRepairToChannel(channel, darkMask, brightMask, options);
            channel.Dispose();
            return repaired;
        }

        private static OpenCvSharp.Mat ApplyDustRepairToChannel(OpenCvSharp.Mat source, OpenCvSharp.Mat darkMask, OpenCvSharp.Mat brightMask, DustRemovalOptions options)
        {
            OpenCvSharp.Mat result = source.Clone();
            int backgroundKernelSize = NormalizeKernelSize(options.RepairRadius * 2 + 1);
            using OpenCvSharp.Mat kernel = OpenCvSharp.Cv2.GetStructuringElement(
                OpenCvSharp.MorphShapes.Ellipse,
                new OpenCvSharp.Size(backgroundKernelSize, backgroundKernelSize));

            if (OpenCvSharp.Cv2.CountNonZero(darkMask) > 0)
            {
                using OpenCvSharp.Mat darkBackground = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.MorphologyEx(source, darkBackground, OpenCvSharp.MorphTypes.Close, kernel);
                darkBackground.CopyTo(result, darkMask);
            }

            if (OpenCvSharp.Cv2.CountNonZero(brightMask) > 0)
            {
                using OpenCvSharp.Mat brightBackground = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.MorphologyEx(source, brightBackground, OpenCvSharp.MorphTypes.Open, kernel);
                brightBackground.CopyTo(result, brightMask);
            }

            return result;
        }

        private readonly struct DustRemovalOptions
        {
            public DustRemovalOptions(DustRemovalMode mode, double thresholdPercent, int minArea, int maxArea, int repairRadius)
            {
                Mode = mode;
                ThresholdPercent = thresholdPercent;
                MinArea = minArea;
                MaxArea = maxArea;
                RepairRadius = repairRadius;
            }

            public DustRemovalMode Mode { get; }
            public double ThresholdPercent { get; }
            public int MinArea { get; }
            public int MaxArea { get; }
            public int RepairRadius { get; }
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
            ImageView.UpdateZoomAndScale();

            CreateAndAnalyzePolarLines();
        }

        private OpenCvSharp.Mat CreateDisplayChannelMat(ExportChannel channel)
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                throw new InvalidOperationException("XYZ 数据未加载");
            }

            if (channel == ExportChannel.ColorDifference)
            {
                return CreateColorDifferenceMat();
            }

            return ConoscopeColorimetry.CreateChannelMat(XMat, YMat, ZMat, channel);
        }

        private OpenCvSharp.Mat CreateColorDifferenceMat()
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                throw new InvalidOperationException("XYZ 数据未加载");
            }

            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode == ColorDifferenceReferenceMode.ReferenceImage)
            {
                EnsureColorDifferenceReferenceReady();
                return ConoscopeColorimetry.CreateColorDifferenceMat(XMat, YMat, ZMat, colorDifferenceReferenceUMat!, colorDifferenceReferenceVMat!);
            }

            ConoscopeUvReference reference = ResolvePointColorDifferenceReference();
            return ConoscopeColorimetry.CreateColorDifferenceMat(XMat, YMat, ZMat, reference.U, reference.V);
        }

        private void EnsureColorDifferenceReferenceReady()
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode == ColorDifferenceReferenceMode.ReferenceImage && (colorDifferenceReferenceUMat == null || colorDifferenceReferenceVMat == null))
            {
                throw new InvalidOperationException("请先点击“保存色差基准图”，再计算实测图色差");
            }

            if (mode == ColorDifferenceReferenceMode.ReferenceImage && XMat != null && colorDifferenceReferenceUMat != null
                && (XMat.Width != colorDifferenceReferenceUMat.Width || XMat.Height != colorDifferenceReferenceUMat.Height))
            {
                throw new InvalidOperationException("当前图像尺寸与色差基准图不一致，无法逐点计算");
            }

            if (mode == ColorDifferenceReferenceMode.Custom && !TryParseCustomColorDifferenceReference(out _))
            {
                throw new InvalidOperationException("请输入有效的自定义 u/v 基准坐标");
            }
        }

        private bool HasXyzData()
        {
            return XMat != null && YMat != null && ZMat != null;
        }

        private ImageFilterType GetSelectedFilterType()
        {
            if (cbFilterType?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string filterTag
                && Enum.TryParse(filterTag, out ImageFilterType filterType))
            {
                return NormalizeFilterType(filterType);
            }

            if (cbFilterType?.SelectedIndex >= 0)
            {
                return NormalizeFilterType((ImageFilterType)cbFilterType.SelectedIndex);
            }

            return NormalizeFilterType(ConoscopeConfig.FilterType);
        }

        private static ImageFilterType NormalizeFilterType(ImageFilterType filterType)
        {
            return Enum.IsDefined(filterType) ? filterType : ImageFilterType.None;
        }

        private bool IsDustRemovalEnabled()
        {
            return chkDustRemovalEnabled?.IsChecked == true;
        }

        private DustRemovalMode GetSelectedDustRemovalMode()
        {
            if (cbDustMode?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string modeTag
                && Enum.TryParse(modeTag, out DustRemovalMode mode))
            {
                return mode;
            }

            return ConoscopeConfig.DustRemovalMode;
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
            UpdateColorDifferencePanelVisibility();

            if (HasXyzData())
            {
                try
                {
                    RefreshDisplayedImage();
                }
                catch (Exception ex)
                {
                    log.Error($"刷新显示通道失败: {ex.Message}", ex);
                    MessageBox.Show(ex.Message, "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ExportChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateColorDifferencePanelVisibility();
        }

        private void btnSaveConoscopeConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFilterControlsToConfig();
                ConfigService.Instance.Save<ConoscopeConfig>();
                MessageBox.Show("配置已保存", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"保存 Conoscope 配置失败: {ex.Message}", ex);
                MessageBox.Show($"保存配置失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ColorDifferenceReference_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingColorDifferenceControls)
            {
                return;
            }

            ConoscopeConfig.ColorDifferenceReferenceMode = GetSelectedColorDifferenceReferenceMode();
            UpdateColorDifferenceReferenceUi();

            if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference && HasXyzData())
            {
                try
                {
                    RefreshDisplayedImage();
                }
                catch (Exception ex)
                {
                    log.Error($"切换色差基准失败: {ex.Message}", ex);
                    MessageBox.Show(ex.Message, "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ColorDifferenceCustom_LostFocus(object sender, RoutedEventArgs e)
        {
            if (isUpdatingColorDifferenceControls)
            {
                return;
            }

            if (!TryParseCustomColorDifferenceReference(out _))
            {
                MessageBox.Show("请输入有效的自定义 u/v 基准坐标", "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateColorDifferenceReferenceUi();
            if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.Custom && HasXyzData())
            {
                RefreshDisplayedImage();
            }
        }

        private void btnSaveColorDifferenceReference_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!HasXyzData() || XMat == null || YMat == null || ZMat == null)
                {
                    MessageBox.Show("请先加载一张实测图", "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                colorDifferenceReferenceUMat?.Dispose();
                colorDifferenceReferenceVMat?.Dispose();
                colorDifferenceReferenceUMat = ConoscopeColorimetry.CreateChannelMat(XMat, YMat, ZMat, ExportChannel.CieU);
                colorDifferenceReferenceVMat = ConoscopeColorimetry.CreateChannelMat(XMat, YMat, ZMat, ExportChannel.CieV);
                colorDifferenceReferenceFileName = Filename;

                isUpdatingColorDifferenceControls = true;
                try
                {
                    SelectComboBoxItemByTag(cbColorDifferenceReference, ColorDifferenceReferenceMode.ReferenceImage.ToString());
                    ConoscopeConfig.ColorDifferenceReferenceMode = ColorDifferenceReferenceMode.ReferenceImage;
                }
                finally
                {
                    isUpdatingColorDifferenceControls = false;
                }

                UpdateColorDifferenceReferenceUi();
            }
            catch (Exception ex)
            {
                log.Error($"保存色差基准图失败: {ex.Message}", ex);
                MessageBox.Show($"保存色差基准图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCalculateColorDifference_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureColorDifferenceReferenceReady();
                SelectComboBoxItemByTag(cbDisplayChannel, ExportChannel.ColorDifference.ToString());
                if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference && HasXyzData())
                {
                    RefreshDisplayedImage();
                }
            }
            catch (Exception ex)
            {
                log.Error($"计算色差失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            RefreshQuickControlsFromAxisParam();

            if (e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceMode))
            {
                ApplyCoordinateAxisReference();
                return;
            }

            if (e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceAngle) ||
                e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceRadiusAngle))
            {
                ApplyCoordinateAxisReference();
            }
        }

        private void CoordinateAxisController_ReferenceChanged(object? sender, ConoscopeCoordinateReferenceChangedEventArgs e)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            UpdateCieWindowSelection(e.Position);
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

            UpdateCieWindowSelection(e.Position);
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

            if (!TryGetChromaticityAtPosition(e.Position, out PixelChromaticitySample sample))
            {
                return GetReferenceValueText(e.Mode, e.Angle, e.RadiusAngle);
            }

            ExportChannel displayChannel = GetSelectedDisplayChannel();
            double displayValue = GetChannelValue(sample.XyzX, sample.XyzY, sample.X, sample.Y, sample.Z, displayChannel);
            double azimuthAngle = GetFullAzimuthAngle(e.Position);
            double polarAngle = GetPolarRadiusAngle(e.Position);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"参考: {GetReferenceValueText(e.Mode, e.Angle, e.RadiusAngle)}");
            builder.AppendLine($"像素: X={sample.ImageX}, Y={sample.ImageY}");
            builder.AppendLine($"极坐标: 方位={azimuthAngle:F2}°, 极角={polarAngle:F2}°");
            builder.AppendLine($"{GetChannelLabel(displayChannel)}: {displayValue:F6}");
            builder.AppendLine($"XYZ: X={sample.X:F4}, Y={sample.Y:F4}, Z={sample.Z:F4}");
            builder.AppendLine($"xy: x={sample.Chromaticity.x:F6}, y={sample.Chromaticity.y:F6}");
            builder.Append($"uv: u={sample.Chromaticity.u:F6}, v={sample.Chromaticity.v:F6}, CCT={ConoscopeColorimetry.FormatCct(sample.Chromaticity.Cct)}");
            return builder.ToString();
        }

        private void btnOpenCieWindow_Click(object sender, RoutedEventArgs e)
        {
            if (!HasXyzData() || currentBitmapSource == null || coordinateAxisController == null)
            {
                MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EnsureCieWindow();
            SyncCieWindowFromCurrentPointer();
        }

        private void EnsureCieWindow()
        {
            if (cieWindow == null)
            {
                cieWindow = new WindowCIE();
                Window? owner = Window.GetWindow(this);
                if (owner != null)
                {
                    cieWindow.Owner = owner;
                }

                cieWindow.Closed += (_, _) => cieWindow = null;
            }

            cieWindow.Show();
            cieWindow.Activate();
        }

        private void SyncCieWindowFromCurrentPointer()
        {
            if (cieWindow == null || coordinateAxisController == null)
            {
                return;
            }

            Point point = Mouse.GetPosition(ImageView.ImageShow);
            if (!coordinateAxisController.Axis.ContainsInteractivePoint(point))
            {
                return;
            }

            UpdateCieWindowSelection(point);
        }

        private void UpdateCieWindowSelection(Point position)
        {
            if (cieWindow == null)
            {
                return;
            }

            if (TryGetChromaticityAtPosition(position, out PixelChromaticitySample sample))
            {
                cieWindow.ChangeSelect(sample.Chromaticity.x, sample.Chromaticity.y);
            }
        }

        private bool TryGetChromaticityAtPosition(Point position, out PixelChromaticitySample sample)
        {
            sample = default;
            if (currentBitmapSource == null || !HasXyzData())
            {
                return false;
            }

            int imageWidth = currentBitmapSource.PixelWidth;
            int imageHeight = currentBitmapSource.PixelHeight;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return false;
            }

            int imageX = ClampToInt((int)Math.Round(position.X), 0, imageWidth - 1);
            int imageY = ClampToInt((int)Math.Round(position.Y), 0, imageHeight - 1);

            int xyzWidth = YMat?.Width ?? XMat?.Width ?? ZMat?.Width ?? imageWidth;
            int xyzHeight = YMat?.Height ?? XMat?.Height ?? ZMat?.Height ?? imageHeight;
            if (xyzWidth <= 0 || xyzHeight <= 0)
            {
                return false;
            }

            int xyzX = ClampToInt(imageX, 0, xyzWidth - 1);
            int xyzY = ClampToInt(imageY, 0, xyzHeight - 1);
            ExtractXYZValues(xyzX, xyzY, out double X, out double Y, out double Z);
            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(X, Y, Z);
            sample = new PixelChromaticitySample(imageX, imageY, xyzX, xyzY, X, Y, Z, chromaticity);
            return true;
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
                SetReferencePlotLimits();
                UpdateReferencePlotHeader();
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
            ExportAngleMode();
        }

        public void ExportAngleMode()
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
            ExportCircleMode();
        }

        public void ExportCircleMode()
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
                },
                ReadColorDifference = (ix, iy) =>
                {
                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);
                    return GetColorDifferenceValue(ix, iy, X, Y, Z);
                }
            };
        }

        /// <summary>
        /// 获取指定通道的值
        /// </summary>
        private double GetChannelValue(RgbSample sample, ExportChannel channel)
        {
            return GetChannelValue((int)Math.Round(sample.DX), (int)Math.Round(sample.DY), sample.X, sample.Y, sample.Z, channel);
        }

        private double GetChannelValue(int ix, int iy, double X, double Y, double Z, ExportChannel channel)
        {
            if (channel == ExportChannel.ColorDifference)
            {
                return GetColorDifferenceValue(ix, iy, X, Y, Z);
            }

            return ConoscopeColorimetry.GetChannelValue(X, Y, Z, channel);
        }

        private double GetColorDifferenceValue(int ix, int iy, double X, double Y, double Z)
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(X, Y, Z);

            if (mode == ColorDifferenceReferenceMode.ReferenceImage)
            {
                EnsureColorDifferenceReferenceReady();
                if (colorDifferenceReferenceUMat == null || colorDifferenceReferenceVMat == null)
                {
                    return 0;
                }

                int x = ClampToInt(ix, 0, colorDifferenceReferenceUMat.Width - 1);
                int y = ClampToInt(iy, 0, colorDifferenceReferenceUMat.Height - 1);
                double referenceU = colorDifferenceReferenceUMat.At<float>(y, x);
                double referenceV = colorDifferenceReferenceVMat.At<float>(y, x);
                return ConoscopeColorimetry.CalculateColorDifferenceFromUv(chromaticity.u, chromaticity.v, referenceU, referenceV);
            }

            ConoscopeUvReference reference = ResolvePointColorDifferenceReference();
            return ConoscopeColorimetry.CalculateColorDifferenceFromUv(chromaticity.u, chromaticity.v, reference.U, reference.V);
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
                ExportChannel.ColorDifference => ScottPlot.Color.FromColor(System.Drawing.Color.Crimson),
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
                        DX = ix,
                        DY = iy,
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
            ConoscopeModuleService.Unregister(this);
            ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            cieWindow?.Close();
            cieWindow = null;
            XMat?.Dispose();
            XMat = null;
            YMat?.Dispose();
            YMat = null;
            ZMat?.Dispose();
            ZMat = null;
            colorDifferenceReferenceUMat?.Dispose();
            colorDifferenceReferenceUMat = null;
            colorDifferenceReferenceVMat?.Dispose();
            colorDifferenceReferenceVMat = null;
            DisposeCoordinateAxis();
            ImageView?.Dispose();
            GC.SuppressFinalize(this);
        }

        private readonly record struct PixelChromaticitySample(
            int ImageX,
            int ImageY,
            int XyzX,
            int XyzY,
            double X,
            double Y,
            double Z,
            ConoscopeChromaticity Chromaticity);

        public void AdvancedExport()
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new AdvancedExportDialog { Owner = Window.GetWindow(this) };
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
