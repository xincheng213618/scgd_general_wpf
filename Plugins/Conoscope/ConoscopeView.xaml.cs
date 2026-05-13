using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.FullScreen;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using Conoscope.Core;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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
        private ExportChannel currentReferenceScaleChannel = ExportChannel.Y;
        private double currentReferenceScaleMaximum = 1;

        private ConoscopeCoordinateAxisController? coordinateAxisController;
        private PolarAngleLine? coordinateAxisPolarLine;
        private ConcentricCircleLine? coordinateAxisCircleLine;
        private bool isUpdatingQuickControls;
        private OpenCvSharp.Mat? colorDifferenceReferenceUMat;
        private OpenCvSharp.Mat? colorDifferenceReferenceVMat;
        private string? colorDifferenceReferenceFileName;
        private bool isUpdatingDisplayControls;
        private bool isUpdatingColorDifferenceControls;
        private bool isUpdatingFilterControls;
        private ReferencePlotDisplayMode referencePlotDisplayMode;
        private WindowCIE? cieWindow;
        private ImageFullScreenMode? imageFullScreenMode;
        private ConoscopeModelProfile? subscribedModelProfile;
        private const float MinPositiveXyzValue = 0.000001f;
        private const double Conoscope3DInitialHeightScale = 160.0;

        private enum ReferencePlotDisplayMode
        {
            Cartesian,
            Polar
        }

        public double MaxAngle => ConoscopeConfig.CurrentModelProfile.MaxAngle;

        public ConoscopeModelProfile CurrentModelProfile => ConoscopeConfig.CurrentModelProfile;
        public string FileName => Filename;

        private void RefreshReferenceLineProfileBinding()
        {
            GridSetting.Children.Clear();
            GridSetting.RowDefinitions.Clear();
            GridSetting.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            GridSetting.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border fieldOfViewEditorHost = new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                Child = PropertyEditorHelper.GenPropertyEditorControl(new ConoscopeFieldOfViewSettings(CurrentModelProfile))
            };
            GridSetting.Children.Add(fieldOfViewEditorHost);
            Grid.SetRow(fieldOfViewEditorHost, 0);

            UIElement coordinateAxisEditor = PropertyEditorHelper.GenPropertyEditorControl(CurrentModelProfile.CoordinateAxisParam);
            GridSetting.Children.Add(coordinateAxisEditor);
            Grid.SetRow(coordinateAxisEditor, 1);
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
            RefreshPreprocessControlsFromConfig();
            UpdateReferencePlotHeader();
            if (HasXyzData())
            {
                RefreshDisplayedImage();
            }
        }

        internal void RefreshPreprocessControlsFromConfig()
        {
            InitializeFilterControls();
        }

        internal void RefreshRenderingFromConfig()
        {
            RefreshDisplayControlsFromConfig();
            RefreshPreprocessControlsFromConfig();
            UpdatePseudoColorMapPreview();
            if (HasXyzData())
            {
                RefreshDisplayedImage();
            }
        }

        private void RefreshDisplayControlsFromConfig()
        {
            isUpdatingDisplayControls = true;
            try
            {
                SelectComboBoxItemByTag(cbDisplayChannel, ConoscopeConfig.DisplayChannel.ToString());
            }
            finally
            {
                isUpdatingDisplayControls = false;
            }

            UpdateColorDifferencePanelVisibility();
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
            RefreshDisplayControlsFromConfig();
            RefreshQuickControlsFromAxisParam();
            InitializeColorDifferenceControls();
            InitializeFilterControls();
            UpdateReferenceControlVisibility();
            UpdateColorDifferencePanelVisibility();
            AttachCurrentModelProfile();

            ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig_ModelTypeChanged(sender, ConoscopeConfig.CurrentModel);
            InitializePlot(wpfPlotReference, "参考曲线 (Reference Distribution)");
            UpdateReferencePlotDisplayMode();
            UpdateReferencePlotHeader();

            imageFullScreenMode = new ImageFullScreenMode(ImageViewHost);
            ImageView.Zoombox1.ContentMatrixChanged -= Zoombox1_ContentMatrixChanged;
            ImageView.Zoombox1.ContentMatrixChanged += Zoombox1_ContentMatrixChanged;
            UpdatePseudoColorMapPreview();
            UpdateToolbarZoomRatio();
            UpdatePanModeState();
        }

        private void Zoombox1_ContentMatrixChanged(object? sender, EventArgs e)
        {
            UpdateToolbarZoomRatio();
        }

        private void UpdateToolbarZoomRatio()
        {
            if (txtToolbarZoomRatio == null)
            {
                return;
            }

            double zoomRatio = ImageView.Zoombox1.ContentMatrix.M11;
            txtToolbarZoomRatio.Text = double.IsFinite(zoomRatio) ? zoomRatio.ToString("F2", CultureInfo.InvariantCulture) : "1.00";
        }

        private void UpdatePanModeState()
        {
            bool isPanModeEnabled = tglPanMode?.IsChecked == true;
            ImageView.Zoombox1.ActivateOn = isPanModeEnabled ? ModifierKeys.None : ModifierKeys.Control;
            ImageView.Zoombox1.Cursor = isPanModeEnabled ? Cursors.Hand : Cursors.Arrow;
        }

        private void tglPanMode_Checked(object sender, RoutedEventArgs e)
        {
            UpdatePanModeState();
        }

        private void tglPanMode_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdatePanModeState();
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
                if (txtQuickReferenceAngle != null)
                {
                    txtQuickReferenceAngle.Text = axisParam.ReferenceAngle.ToString("F2", CultureInfo.InvariantCulture);
                }

                if (txtQuickReferenceRadius != null)
                {
                    txtQuickReferenceRadius.Text = axisParam.ReferenceRadiusAngle.ToString("F2", CultureInfo.InvariantCulture);
                }
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

        private void QuickReferenceTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            if (ReferenceEquals(sender, txtQuickReferenceAngle))
            {
                ApplyQuickReferenceAngleFromText();
            }
            else if (ReferenceEquals(sender, txtQuickReferenceRadius))
            {
                ApplyQuickReferenceRadiusFromText();
            }

            e.Handled = true;
        }

        private void txtQuickReferenceAngle_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyQuickReferenceAngleFromText();
        }

        private void txtQuickReferenceRadius_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyQuickReferenceRadiusFromText();
        }

        private void ApplyQuickReferenceAngleFromText()
        {
            if (txtQuickReferenceAngle == null)
            {
                return;
            }

            if (!TryParseDouble(txtQuickReferenceAngle.Text, out double angle) || !double.IsFinite(angle))
            {
                RefreshQuickControlsFromAxisParam();
                return;
            }

            CurrentModelProfile.CoordinateAxisParam.ReferenceAngle = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(angle);
            RefreshQuickControlsFromAxisParam();
        }

        private void ApplyQuickReferenceRadiusFromText()
        {
            if (txtQuickReferenceRadius == null)
            {
                return;
            }

            if (!TryParseDouble(txtQuickReferenceRadius.Text, out double radiusAngle) || !double.IsFinite(radiusAngle))
            {
                RefreshQuickControlsFromAxisParam();
                return;
            }

            CurrentModelProfile.CoordinateAxisParam.ReferenceRadiusAngle = Math.Max(0, Math.Min(radiusAngle, MaxAngle));
            RefreshQuickControlsFromAxisParam();
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
            AttachCurrentModelProfile();
            RefreshModelDependentUi();
            if (HasXyzData())
            {
                RefreshDisplayedImage();
            }
        }

        private void AttachCurrentModelProfile()
        {
            if (ReferenceEquals(subscribedModelProfile, CurrentModelProfile))
            {
                return;
            }

            if (subscribedModelProfile != null)
            {
                subscribedModelProfile.PropertyChanged -= CurrentModelProfile_PropertyChanged;
            }

            subscribedModelProfile = CurrentModelProfile;
            subscribedModelProfile.PropertyChanged -= CurrentModelProfile_PropertyChanged;
            subscribedModelProfile.PropertyChanged += CurrentModelProfile_PropertyChanged;
        }

        private void CurrentModelProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ConoscopeModelProfile.MaxAngle)
                && e.PropertyName != nameof(ConoscopeModelProfile.CalculationDiameterPixels)
                && e.PropertyName != nameof(ConoscopeModelProfile.ManualConoscopeCoefficient))
            {
                return;
            }

            CurrentModelProfile.CoordinateAxisParam.MaxAngle = MaxAngle;
            CurrentModelProfile.CoordinateAxisParam.ReferenceRadiusAngle = Math.Max(0, Math.Min(CurrentModelProfile.CoordinateAxisParam.ReferenceRadiusAngle, MaxAngle));
            RefreshQuickControlsFromAxisParam();
            SetReferencePlotLimits();
            UpdateReferencePlotHeader();

            if (HasXyzData())
            {
                RefreshDisplayedImage();
            }
        }

        private void InitializePlot(ScottPlot.WPF.WpfPlot plot, string title)
        {
            plot.Plot.Title(title);
            plot.Plot.XLabel("Degrees");
            plot.Plot.YLabel(GetChannelAxisLabel(ExportChannel.Y));
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

        private void UpdateReferencePlotDisplayMode()
        {
            bool isPolar = referencePlotDisplayMode == ReferencePlotDisplayMode.Polar;

            if (wpfPlotReference != null)
            {
                wpfPlotReference.Visibility = isPolar ? Visibility.Collapsed : Visibility.Visible;
            }

            if (polarPlotReference != null)
            {
                polarPlotReference.Visibility = isPolar ? Visibility.Visible : Visibility.Collapsed;
            }

            if (tglReferencePolarMode != null && tglReferencePolarMode.IsChecked != isPolar)
            {
                tglReferencePolarMode.IsChecked = isPolar;
            }
        }

        private void tglReferencePolarMode_Checked(object sender, RoutedEventArgs e)
        {
            referencePlotDisplayMode = ReferencePlotDisplayMode.Polar;
            UpdateReferencePlotDisplayMode();
            UpdateReferencePlot();
        }

        private void tglReferencePolarMode_Unchecked(object sender, RoutedEventArgs e)
        {
            referencePlotDisplayMode = ReferencePlotDisplayMode.Cartesian;
            UpdateReferencePlotDisplayMode();
            UpdateReferencePlot();
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
            OpenCieWindow();
        }

        private void ToolbarOpenCie_Click(object sender, RoutedEventArgs e)
        {
            OpenCieWindow();
        }

        internal void OpenCieForCurrentView()
        {
            OpenCieWindow();
        }

        private void OpenCieWindow()
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

        private void ToolbarZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.Zoom(1.25);
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.Zoom(0.8);
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomNone_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.ZoomNone();
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomUniform_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.ZoomUniform();
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomUniformToFill_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.ZoomUniformToFill();
            UpdateToolbarZoomRatio();
        }

        private void txtToolbarZoomRatio_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyToolbarZoomRatio();
                e.Handled = true;
            }
        }

        private void txtToolbarZoomRatio_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyToolbarZoomRatio();
        }

        private void ApplyToolbarZoomRatio()
        {
            if (txtToolbarZoomRatio == null)
            {
                return;
            }

            if (!TryParseDouble(txtToolbarZoomRatio.Text, out double zoomRatio) || !double.IsFinite(zoomRatio) || zoomRatio <= 0)
            {
                UpdateToolbarZoomRatio();
                return;
            }

            double currentZoom = ImageView.Zoombox1.ContentMatrix.M11;
            if (!double.IsFinite(currentZoom) || currentZoom <= 0)
            {
                currentZoom = 1;
            }

            ImageView.Zoombox1.Zoom(zoomRatio / currentZoom);
            UpdateToolbarZoomRatio();
        }

        private void ToolbarFullScreen_Click(object sender, RoutedEventArgs e)
        {
            imageFullScreenMode ??= new ImageFullScreenMode(ImageViewHost);
            imageFullScreenMode.ToggleFullScreen();
        }

        private void ToolbarOpen3D_Click(object sender, RoutedEventArgs e)
        {
            Open3DForCurrentView();
        }

        internal void Open3DForCurrentView()
        {
            if (!HasXyzData() || currentBitmapSource == null)
            {
                MessageBox.Show("当前图像尚未准备好 3D 视图", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                WriteableBitmap heightBitmap = Create3DHeightBitmapForCurrentView();
                Window3D window3D = new(heightBitmap, Conoscope3DInitialHeightScale)
                {
                    Owner = Window.GetWindow(this)
                };
                window3D.Show();
            }
            catch (Exception ex)
            {
                log.Error("打开 Conoscope 3D 视图失败", ex);
                MessageBox.Show($"打开 3D 视图失败: {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private WriteableBitmap Create3DHeightBitmapForCurrentView()
        {
            return ConoscopePseudoColorRenderer.CreateHeightMapBitmap(
                XMat!,
                YMat!,
                ZMat!,
                GetSelectedDisplayChannel(),
                CreateColorDifferenceMat,
                currentImageCenter,
                currentImageRadius);
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
            ExtractRgbAlongLine(coordinateAxisPolarLine, endpoints.End, endpoints.Start, currentBitmapSource, currentImageRadius);

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

        private static string GetChannelAxisLabel(ExportChannel channel)
        {
            string label = GetChannelLabel(channel);
            return channel is ExportChannel.X or ExportChannel.Y or ExportChannel.Z
                ? $"{label} (cd/m2)"
                : label;
        }

        private static SolidColorBrush GetChannelPlotBrush(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => Brushes.Gold,
                ExportChannel.Y => Brushes.LimeGreen,
                ExportChannel.Z => Brushes.Violet,
                ExportChannel.CieX => Brushes.OrangeRed,
                ExportChannel.CieY => Brushes.SeaGreen,
                ExportChannel.CieU => Brushes.DodgerBlue,
                ExportChannel.CieV => Brushes.MediumPurple,
                ExportChannel.ColorDifference => Brushes.Crimson,
                _ => Brushes.LimeGreen
            };
        }

        private static double GetNicePolarReferenceRadiusMaximum(double maxValue)
        {
            if (maxValue <= 0)
            {
                return 1;
            }

            const int ringCount = 6;
            double rawStep = maxValue / ringCount;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double normalized = rawStep / magnitude;
            double niceNormalized = normalized <= 1 ? 1
                : normalized <= 1.5 ? 1.5
                : normalized <= 2 ? 2
                : normalized <= 2.5 ? 2.5
                : normalized <= 3 ? 3
                : normalized <= 4 ? 4
                : normalized <= 5 ? 5
                : 10;

            return niceNormalized * magnitude * ringCount;
        }

        private double GetStablePolarReferenceRadiusMaximum(ExportChannel channel, IEnumerable<double> values)
        {
            double curveMaximum = values
                .Where(double.IsFinite)
                .DefaultIfEmpty(0)
                .Max();

            double scaleMaximum = curveMaximum;
            if (channel == currentReferenceScaleChannel
                && double.IsFinite(currentReferenceScaleMaximum)
                && currentReferenceScaleMaximum > 0)
            {
                scaleMaximum = Math.Max(scaleMaximum, currentReferenceScaleMaximum);
            }

            return GetNicePolarReferenceRadiusMaximum(scaleMaximum);
        }

        private static double NormalizePolarPlotAngle(double angleDegrees)
        {
            double normalized = angleDegrees % 360.0;
            return normalized < 0 ? normalized + 360.0 : normalized;
        }

        private void UpdatePolarReferencePlot(IReadOnlyList<PolarPlotPoint> points, ExportChannel channel, bool closePath)
        {
            if (polarPlotReference == null)
            {
                return;
            }

            double radialMaximum = GetStablePolarReferenceRadiusMaximum(channel, points.Select(point => point.Radius));
            polarPlotReference.UpdatePlot(points, GetChannelPlotBrush(channel), $"半径: {GetChannelAxisLabel(channel)}", radialMaximum, closePath);
        }

        private static ScottPlot.Color GetPlotColor(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => ScottPlot.Color.FromColor(System.Drawing.Color.Gold),
                ExportChannel.Y => ScottPlot.Color.FromColor(System.Drawing.Color.LimeGreen),
                ExportChannel.Z => ScottPlot.Color.FromColor(System.Drawing.Color.Violet),
                ExportChannel.CieX => ScottPlot.Color.FromColor(System.Drawing.Color.OrangeRed),
                ExportChannel.CieY => ScottPlot.Color.FromColor(System.Drawing.Color.SeaGreen),
                ExportChannel.CieU => ScottPlot.Color.FromColor(System.Drawing.Color.DodgerBlue),
                ExportChannel.CieV => ScottPlot.Color.FromColor(System.Drawing.Color.MediumPurple),
                ExportChannel.ColorDifference => ScottPlot.Color.FromColor(System.Drawing.Color.Crimson),
                _ => ScottPlot.Color.FromColor(System.Drawing.Color.LimeGreen)
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
                    double radians = anglePos * Math.PI / 180.0;
                    double x = center.X + radiusPixels * Math.Cos(radians);
                    double y = center.Y - radiusPixels * Math.Sin(radians);

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
                if (referencePlotDisplayMode == ReferencePlotDisplayMode.Polar)
                {
                    if (selectedPolarLine == null || selectedPolarLine.RgbData.Count == 0)
                    {
                        polarPlotReference?.Clear();
                        return;
                    }

                    ExportChannel polarChannel = GetSelectedDisplayChannel();
                    PolarPlotPoint[] polarPoints = selectedPolarLine.RgbData
                        .Select(sample => new PolarPlotPoint(NormalizePolarPlotAngle(sample.Position), GetChannelValue(sample, polarChannel)))
                        .ToArray();
                    UpdatePolarReferencePlot(polarPoints, polarChannel, closePath: false);
                    return;
                }

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
                wpfPlotReference.Plot.YLabel(GetChannelAxisLabel(channel));
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
                if (referencePlotDisplayMode == ReferencePlotDisplayMode.Polar)
                {
                    if (selectedCircleLine == null || selectedCircleLine.RgbData.Count == 0)
                    {
                        polarPlotReference?.Clear();
                        return;
                    }

                    ExportChannel polarChannel = GetSelectedDisplayChannel();
                    PolarPlotPoint[] polarPoints = selectedCircleLine.RgbData
                        .Select(sample => new PolarPlotPoint(NormalizePolarPlotAngle(sample.Position), GetChannelValue(sample, polarChannel)))
                        .ToArray();
                    UpdatePolarReferencePlot(polarPoints, polarChannel, closePath: true);
                    return;
                }

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
                wpfPlotReference.Plot.YLabel(GetChannelAxisLabel(channel));
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
            if (subscribedModelProfile != null)
            {
                subscribedModelProfile.PropertyChanged -= CurrentModelProfile_PropertyChanged;
                subscribedModelProfile = null;
            }
            ImageView.Zoombox1.ContentMatrixChanged -= Zoombox1_ContentMatrixChanged;
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

        private sealed class ConoscopeFieldOfViewSettings
        {
            private readonly ConoscopeModelProfile modelProfile;

            public ConoscopeFieldOfViewSettings(ConoscopeModelProfile modelProfile)
            {
                this.modelProfile = modelProfile;
            }

            [Category("视场"), DisplayName("视场角(度)"), Description("设计视场角。分析半径 = 视场角 * 生效视场系数。")]
            public int MaxAngle
            {
                get => modelProfile.MaxAngle;
                set => modelProfile.MaxAngle = value;
            }

            [Category("视场"), DisplayName("完整像素数(px)"), Description("对应 MaxAngle 的像素半径，也就是从圆心到最外圈的完整像素数。填 0 使用图像短边一半。输入 3000 时，ConoscopeCoefficient 按 视场角 / 3000 计算。")]
            public double FullScalePixelCount
            {
                get => modelProfile.FullScalePixelCount;
                set => modelProfile.FullScalePixelCount = value;
            }

            [Category("视场"), DisplayName("ConoscopeCoefficient(度/像素)"), Description("可直接输入 60/3100 这类小数。填 0 表示按完整像素数自动计算。分析半径 = 视场角 / 该系数。")]
            public double DirectConoscopeCoefficient
            {
                get => modelProfile.DirectConoscopeCoefficient;
                set => modelProfile.DirectConoscopeCoefficient = value;
            }
        }

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

        private CurrentCurveExportSettings? ShowCurrentCurveExportDialog()
        {
            var dialog = new CurrentCurveExportDialog
            {
                Owner = Window.GetWindow(this)
            };

            return dialog.ShowDialog() == true ? dialog.Settings : null;
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
                CurrentCurveExportSettings? exportSettings = ShowCurrentCurveExportDialog();
                if (exportSettings == null)
                {
                    return;
                }

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
                    ConoscopeExportService.ExportAzimuthCrossSection(
                        saveFileDialog.FileName,
                        channel,
                        CreateExportContext(),
                        selectedPolarLine.Angle,
                        new ConoscopeCrossSectionExportOptions
                        {
                            StepDegrees = exportSettings.StepDegrees,
                            IncludeMetadata = exportSettings.IncludeMetadata
                        });
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
                CurrentCurveExportSettings? exportSettings = ShowCurrentCurveExportDialog();
                if (exportSettings == null)
                {
                    return;
                }

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
                    ConoscopeExportService.ExportPolarCrossSection(
                        saveFileDialog.FileName,
                        channel,
                        CreateExportContext(),
                        selectedCircleLine.RadiusAngle,
                        new ConoscopeCrossSectionExportOptions
                        {
                            StepDegrees = exportSettings.StepDegrees,
                            IncludeMetadata = exportSettings.IncludeMetadata
                        });
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
