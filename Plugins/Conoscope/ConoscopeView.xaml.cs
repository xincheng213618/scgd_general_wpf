using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.Core;
using log4net;
using Microsoft.Win32;
using Conoscope.Core;
using Conoscope.Presentation.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    public partial class ConoscopeView : UserControl, IDisposable, IActiveDocumentStatusProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeView));

        private sealed class ViewRenderingState
        {
            public ExportChannel DisplayChannel { get; set; } = ExportChannel.Y;
            public ColormapTypes PseudoColorMap { get; set; } = ColormapTypes.COLORMAP_JET;
            public bool UsePseudoColor { get; set; } = true;
            public bool UsePseudoColorRangeLimit { get; set; } = true;
        }

        private sealed class ViewPreprocessState
        {
            public bool ApplyFilterOnOpen { get; set; } = true;
            public bool ClampNonPositiveXyzOnLoad { get; set; } = true;
            public ImageFilterType FilterType { get; set; } = ImageFilterType.Gaussian;
            public int FilterKernelSize { get; set; } = 55;
            public double FilterSigma { get; set; } = 1.0;
            public int FilterD { get; set; } = 5;
            public double FilterSigmaColor { get; set; } = 75;
            public double FilterSigmaSpace { get; set; } = 75;
            public bool DustRemovalEnabled { get; set; }
            public DustRemovalMode DustRemovalMode { get; set; } = DustRemovalMode.DarkSpot;
            public double DustThresholdPercent { get; set; } = 12;
            public int DustMinArea { get; set; } = 1;
            public int DustMaxArea { get; set; } = 500;
            public int DustRepairRadius { get; set; } = 3;
        }

        private sealed class ViewColorDifferenceState
        {
            public ColorDifferenceReferenceMode ReferenceMode { get; set; } = ColorDifferenceReferenceMode.D65;
            public double CustomU { get; set; } = 0.1978;
            public double CustomV { get; set; } = 0.4684;
        }

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
        private ExportChannel selectedExportChannel = ExportChannel.Y;

        private ConoscopeCoordinateAxisController? coordinateAxisController;
        private PolarAngleLine? coordinateAxisPolarLine;
        private ConcentricCircleLine? coordinateAxisCircleLine;
        private bool isUpdatingQuickControls;
        private bool isUpdatingDisplayControls;
        private bool isUpdatingColorDifferenceControls;
        private bool isUpdatingContrastControls;
        private ContrastReferenceKind contrastImageKind = ContrastReferenceKind.Black;
        private ReferencePlotDisplayMode referencePlotDisplayMode;
        private WindowCIE? cieWindow;
        private ConoscopeModelProfile? subscribedModelProfile;
        private const float MinPositiveXyzValue = 0.000001f;
        private const double Conoscope3DInitialHeightScale = 160.0;
        private ConoscopeImageZoomMode imageZoomMode = ConoscopeImageZoomMode.Fit;
        private bool applyCircleFitOnNextRefresh;
        private bool isApplyingImageZoomMode;
        private readonly ViewRenderingState viewRenderingConfig = new();
        private readonly ViewPreprocessState viewPreprocessConfig = new();
        private readonly ViewColorDifferenceState viewColorDifferenceConfig = new();
        private readonly ConoscopeCoordinateAxisParam viewCoordinateAxisConfig = new();

        public event EventHandler StatusBarItemsChanged;

        private enum ReferencePlotDisplayMode
        {
            Cartesian,
            Polar
        }

        private enum ConoscopeImageZoomMode
        {
            Fit,
            Fill,
            ActualSize,
            CircleFit,
            Custom
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

            UIElement coordinateAxisEditor = PropertyEditorHelper.GenPropertyEditorControl(CoordinateAxisConfig);
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
            InitializeColorDifferenceControls();
            InitializeContrastControls();
            RefreshRenderingFromConfig();
            UpdateReferencePlotHeader();
        }

        internal void RefreshPreprocessControlsFromConfig()
        {
            InitializePreprocessControls();
        }

        internal void RefreshRenderingFromConfig()
        {
            RefreshDisplayControlsFromConfig();
            RefreshPreprocessControlsFromConfig();
            UpdatePseudoColorMapPreview();
            if (HasDisplayData())
            {
                EnsureSelectedDisplayChannelAvailable();

                RefreshDisplayedImage();
            }
        }

        internal void RefreshGlobalReferenceState()
        {
            RefreshChannelAvailability();
            UpdateColorDifferenceReferenceUi();
            UpdateContrastReferenceUi();

            if (!HasXyzData())
            {
                RaiseWindowQuickControlStateChanged();
                return;
            }

            ExportChannel channel = GetSelectedDisplayChannel();
            bool usesGlobalReference = channel == ExportChannel.Contrast
                || (channel == ExportChannel.ColorDifference && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.ReferenceImage);

            if (!usesGlobalReference)
            {
                RaiseWindowQuickControlStateChanged();
                return;
            }

            EnsureSelectedDisplayChannelAvailable();

            try
            {
                RefreshDisplayedImage();
                UpdateReferencePlot();
            }
            catch (Exception ex)
            {
                log.Warn($"刷新全局基准图状态失败: {ex.Message}", ex);
            }
        }

        private void EnsureSelectedDisplayChannelAvailable()
        {
            ExportChannel channel = GetSelectedDisplayChannel();
            bool isAvailable = channel switch
            {
                ExportChannel.X or ExportChannel.Z or ExportChannel.CieX or ExportChannel.CieY or ExportChannel.CieU or ExportChannel.CieV => HasXyzData(),
                ExportChannel.ColorDifference => CanRefreshColorDifferenceDisplay(),
                ExportChannel.Contrast => CanRefreshContrastDisplay(),
                _ => HasDisplayData()
            };

            if (isAvailable)
            {
                return;
            }

            RenderingConfig.DisplayChannel = ExportChannel.Y;
            RefreshDisplayControlsFromConfig();
        }

        private void RefreshDisplayControlsFromConfig()
        {
            RefreshChannelAvailability();

            isUpdatingDisplayControls = true;
            try
            {
                ComboBoxHelper.SelectItemByTag(cbDisplayChannel, RenderingConfig.DisplayChannel.ToString());
            }
            finally
            {
                isUpdatingDisplayControls = false;
            }

            UpdateColorDifferencePanelVisibility();
        }

        public IEnumerable<StatusBarMeta> GetActiveStatusBarItems()
        {
            List<StatusBarMeta> items = new();
            if (YMat != null)
            {
                items.Add(new StatusBarMeta
                {
                    Id = "ConoscopeImageDimensions",
                    Name = Properties.Resources.StatusImageSize,
                    Description = $"{YMat.Cols} x {YMat.Rows}",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 100,
                    Source = this,
                });
            }

            if (!string.IsNullOrWhiteSpace(Filename))
            {
                items.Add(new StatusBarMeta
                {
                    Id = "ConoscopeFileType",
                    Name = Properties.Resources.StatusFileType,
                    Description = Path.GetExtension(Filename).ToUpperInvariant(),
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 102,
                    Source = this,
                });
            }

            if (HasCaptureExposureSummary)
            {
                items.Add(new StatusBarMeta
                {
                    Id = "ConoscopeExposure",
                    Name = Properties.Resources.LabelExposure,
                    Description = CaptureExposureSummary,
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 103,
                    Source = this,
                });
            }

            return items;
        }

        public ConoscopeView()
        {
            InitializeComponent();
            InitializeFocusToolbarIcons();
            InitializeLocalViewStateFromDefaults();
            ImageView.FocusCircleCalculationRequested += ImageView_FocusCircleCalculationRequested;
            ImageView.FocusCircleEditRequested += ImageView_FocusCircleEditRequested;
            ImageView.FocusCirclesChanged += ImageView_FocusCirclesChanged;
            ImageView.FocusCircleSelectionChanged += ImageView_FocusCircleSelectionChanged;
            ConoscopeModuleService.Register(this);
        }

        private void InitializeFocusToolbarIcons()
        {
            SetFocusToolbarIcon(tglFocusCircleMode, "DrawingImagedrag");
            SetFocusToolbarIcon(tglFocusCircleDrawTool, "DrawingImageCircle");
            SetFocusToolbarIcon(tglFocusCircleEraseTool, "DrawingImageeraser");
            SetFocusToolbarIcon(btnCircleFit, "DrawingImage1_1");
            SetFocusToolbarIcon(btnCalculateFocusCircles, "DrawingImageAlgorithm");
            SetFocusToolbarIcon(btnSaveFocusPoiTemplate, "DrawingImageSave");
        }

        private static void SetFocusToolbarIcon(ContentControl? control, string resourceKey)
        {
            if (control == null)
            {
                return;
            }

            Image icon = IEditorToolFactory.TryFindResource(resourceKey);
            if (Application.Current.TryFindResource("ToolBarImage") is Style toolBarImageStyle)
            {
                icon.Style = toolBarImageStyle;
            }

            control.Content = icon;
        }

        private void ImageView_FocusCirclesChanged(object? sender, EventArgs e)
        {
            UpdateFocusCircleToolbarState();
            UpdateSelectedFocusPointInfo();
        }

        private void ImageView_FocusCircleSelectionChanged(object? sender, EventArgs e)
        {
            UpdateSelectedFocusPointInfo();
        }

        public ConoscopeConfig ConoscopeConfig => ConoscopeManager.GetInstance().Config;
        private ConoscopeGlobalReferenceStore GlobalReferences => ConoscopeManager.GetInstance().GlobalReferences;
        private ViewRenderingState RenderingConfig => viewRenderingConfig;
        private ViewPreprocessState PreprocessConfig => viewPreprocessConfig;
        private ViewColorDifferenceState ColorDifferenceConfig => viewColorDifferenceConfig;
        private ConoscopeCoordinateAxisParam CoordinateAxisConfig => viewCoordinateAxisConfig;

        private void InitializeLocalViewStateFromDefaults()
        {
            ApplyDefaultRenderingStateFromConfig();
            ApplyDefaultPreprocessStateFromConfig();
            InitializeLocalCoordinateAxisState(preserveReferenceState: false);
        }

        private void ApplyDefaultRenderingStateFromConfig()
        {
            viewRenderingConfig.DisplayChannel = ConoscopeConfig.DisplayChannel;
            viewRenderingConfig.PseudoColorMap = ConoscopeConfig.PseudoColorMap;
            viewRenderingConfig.UsePseudoColor = ConoscopeConfig.UsePseudoColor;
            viewRenderingConfig.UsePseudoColorRangeLimit = ConoscopeConfig.UsePseudoColorRangeLimit;
        }

        private void ApplyDefaultPreprocessStateFromConfig()
        {
            viewPreprocessConfig.ApplyFilterOnOpen = ConoscopeConfig.ApplyFilterOnOpen;
            viewPreprocessConfig.ClampNonPositiveXyzOnLoad = ConoscopeConfig.ClampNonPositiveXyzOnLoad;
            viewPreprocessConfig.FilterType = ConoscopeConfig.FilterType;
            viewPreprocessConfig.FilterKernelSize = ConoscopeConfig.FilterKernelSize;
            viewPreprocessConfig.FilterSigma = ConoscopeConfig.FilterSigma;
            viewPreprocessConfig.FilterD = ConoscopeConfig.FilterD;
            viewPreprocessConfig.FilterSigmaColor = ConoscopeConfig.FilterSigmaColor;
            viewPreprocessConfig.FilterSigmaSpace = ConoscopeConfig.FilterSigmaSpace;
            viewPreprocessConfig.DustRemovalEnabled = ConoscopeConfig.DustRemovalEnabled;
            viewPreprocessConfig.DustRemovalMode = ConoscopeConfig.DustRemovalMode;
            viewPreprocessConfig.DustThresholdPercent = ConoscopeConfig.DustThresholdPercent;
            viewPreprocessConfig.DustMinArea = ConoscopeConfig.DustMinArea;
            viewPreprocessConfig.DustMaxArea = ConoscopeConfig.DustMaxArea;
            viewPreprocessConfig.DustRepairRadius = ConoscopeConfig.DustRepairRadius;

            ImageFilterType filterType = NormalizeFilterType(viewPreprocessConfig.FilterType);
            lastEnabledFilterType = filterType == ImageFilterType.None ? ImageFilterType.LowPass : filterType;
        }

        internal void ApplyWindowRenderingDefaults()
        {
            ApplyDefaultRenderingStateFromConfig();
            RefreshDisplayControlsFromConfig();
            RefreshPreprocessControlsFromConfig();
            UpdatePseudoColorMapPreview();

            if (HasXyzData())
            {
                EnsureSelectedDisplayChannelAvailable();
                RefreshDisplayedImage();
            }
        }

        internal void ApplyWindowPreprocessDefaults()
        {
            ApplyDefaultPreprocessStateFromConfig();
            RefreshPreprocessControlsFromConfig();
        }

        private void InitializeLocalCoordinateAxisState(bool preserveReferenceState)
        {
            ConoscopeCoordinateAxisParam source = CurrentModelProfile.CoordinateAxisParam;
            ConoscopeCoordinateReferenceMode referenceMode = CoordinateAxisConfig.ReferenceMode;
            double referenceAngle = CoordinateAxisConfig.ReferenceAngle;
            double referenceRadiusAngle = CoordinateAxisConfig.ReferenceRadiusAngle;
            bool isInteractionEnabled = CoordinateAxisConfig.IsInteractionEnabled;

            CoordinateAxisConfig.IsInteractionEnabled = preserveReferenceState ? isInteractionEnabled : source.IsInteractionEnabled;
            CoordinateAxisConfig.MaxAngle = source.MaxAngle;
            CoordinateAxisConfig.ConoscopeCoefficient = source.ConoscopeCoefficient;
            CoordinateAxisConfig.CenterX = source.CenterX;
            CoordinateAxisConfig.CenterY = source.CenterY;
            CoordinateAxisConfig.AxisRadius = source.AxisRadius;
            CoordinateAxisConfig.AzimuthStep = source.AzimuthStep;
            CoordinateAxisConfig.PolarStep = source.PolarStep;
            CoordinateAxisConfig.LineWidth = source.LineWidth;
            CoordinateAxisConfig.AxisBrush = source.AxisBrush;
            CoordinateAxisConfig.ReferenceMode = preserveReferenceState ? referenceMode : source.ReferenceMode;
            CoordinateAxisConfig.ReferenceAngle = preserveReferenceState ? referenceAngle : source.ReferenceAngle;
            CoordinateAxisConfig.ReferenceRadiusAngle = preserveReferenceState
                ? Math.Max(0, Math.Min(referenceRadiusAngle, source.MaxAngle))
                : Math.Max(0, Math.Min(source.ReferenceRadiusAngle, source.MaxAngle));
            CoordinateAxisConfig.ReferenceLineWidth = source.ReferenceLineWidth;
            CoordinateAxisConfig.ReferenceBrush = source.ReferenceBrush;
            CoordinateAxisConfig.IsMaskVisible = source.IsMaskVisible;
            CoordinateAxisConfig.MaskOpacity = source.MaskOpacity;
            CoordinateAxisConfig.MaskColor = source.MaskColor;
            CoordinateAxisConfig.IsTextVisible = source.IsTextVisible;
            CoordinateAxisConfig.FontSize = source.FontSize;
            CoordinateAxisConfig.TextBrush = source.TextBrush;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            RefreshReferenceLineProfileBinding();

            this.DataContext = ConoscopeManager.GetInstance();
            RefreshDisplayControlsFromConfig();
            RefreshQuickControlsFromAxisParam();
            InitializeColorDifferenceControls();
            InitializeContrastControls();
            InitializePreprocessControls();
            UpdateReferenceControlVisibility();
            UpdateColorDifferencePanelVisibility();
            AttachCurrentModelProfile();

            ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig_ModelTypeChanged(sender, ConoscopeConfig.CurrentModel);
            InitializePlot(wpfPlotReference, Properties.Resources.ReferenceCurve);
            UpdateReferencePlotDisplayMode();
            UpdateReferencePlotHeader();

            ImageView.Zoombox1.ContentMatrixChanged -= Zoombox1_ContentMatrixChanged;
            ImageView.Zoombox1.ContentMatrixChanged += Zoombox1_ContentMatrixChanged;
            UpdatePseudoColorMapPreview();
            InitializeFocusPointTools();
            UpdatePanModeState();
        }

        private void Zoombox1_ContentMatrixChanged(object? sender, EventArgs e)
        {
            if (!isApplyingImageZoomMode)
            {
                imageZoomMode = ConoscopeImageZoomMode.Custom;
            }
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
            AttachCurrentModelProfile();
            InitializeLocalCoordinateAxisState(preserveReferenceState: true);
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

            InitializeLocalCoordinateAxisState(preserveReferenceState: true);
            RefreshQuickControlsFromAxisParam();
            SetReferencePlotLimits();
            UpdateReferencePlotHeader();

            if (HasXyzData())
            {
                RefreshDisplayedImage();
            }
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
            ImageView.FocusCircleCalculationRequested -= ImageView_FocusCircleCalculationRequested;
            ImageView.FocusCircleEditRequested -= ImageView_FocusCircleEditRequested;
            ImageView.FocusCirclesChanged -= ImageView_FocusCirclesChanged;
            ImageView.FocusCircleSelectionChanged -= ImageView_FocusCircleSelectionChanged;
            cieWindow?.Close();
            cieWindow = null;
            XMat?.Dispose();
            XMat = null;
            YMat?.Dispose();
            YMat = null;
            ZMat?.Dispose();
            ZMat = null;
            DisposePseudoColorRangeMasks();
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

            [Display(Name = "Con_FOV_Angle", GroupName = "Con_Category_FOV", Description = "设计视场角。分析半径 = 视场角 * 生效视场系数。", ResourceType = typeof(Properties.Resources))]
            public int MaxAngle
            {
                get => modelProfile.MaxAngle;
                set => modelProfile.MaxAngle = value;
            }

            [Display(Name = "Con_FOV_Pixels", GroupName = "Con_Category_FOV", Description = "对应 MaxAngle 的像素半径，也就是从圆心到最外圈的完整像素数。填 0 使用图像短边一半。输入 3000 时，ConoscopeCoefficient 按 视场角 / 3000 计算。", ResourceType = typeof(Properties.Resources))]
            public double FullScalePixelCount
            {
                get => modelProfile.FullScalePixelCount;
                set => modelProfile.FullScalePixelCount = value;
            }

            [Display(Name = "Con_FOV_Coefficient", GroupName = "Con_Category_FOV", Description = "可直接输入 60/3100 这类小数。填 0 表示按完整像素数自动计算。分析半径 = 视场角 / 该系数。", ResourceType = typeof(Properties.Resources))]
            public double DirectConoscopeCoefficient
            {
                get => modelProfile.DirectConoscopeCoefficient;
                set => modelProfile.DirectConoscopeCoefficient = value;
            }
        }
    }
}
