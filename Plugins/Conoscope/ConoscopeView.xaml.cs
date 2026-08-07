#pragma warning disable CA1822
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.Core;
using log4net;
using Microsoft.Win32;
using Conoscope.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// A document's editable state. The window ribbon and the view both use this
    /// object; UI controls are never used as backing storage.
    /// </summary>
    internal sealed class ConoscopeViewState
    {
        public ExportChannel DisplayChannel { get; set; } = ExportChannel.Y;
        public ExportChannel ExportChannel { get; set; } = ExportChannel.Y;
        public ColormapTypes PseudoColorMap { get; set; } = ColormapTypes.COLORMAP_JET;
        public bool UsePseudoColor { get; set; } = true;
        public bool UsePseudoColorRangeLimit { get; set; } = true;

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

        public ColorDifferenceReferenceMode ColorDifferenceReferenceMode { get; set; } = ColorDifferenceReferenceMode.D65;
        public double ColorDifferenceCustomU { get; set; } = 0.1978;
        public double ColorDifferenceCustomV { get; set; } = 0.4684;
        public ContrastReferenceKind ContrastImageKind { get; set; } = ContrastReferenceKind.Black;
        public ConoscopeCoordinateAxisParam CoordinateAxis { get; } = new();
    }

    /// <summary>
    /// ConoscopeView.xaml 的交互逻辑
    /// </summary>
    public partial class ConoscopeView : UserControl, IDisposable, IActiveDocumentStatusProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeView));

        private ReferenceCurve? selectedReferenceCurve;
        private PolarAngleLine? selectedPolarLine => selectedReferenceCurve as PolarAngleLine;
        private ConcentricCircleLine? selectedCircleLine => selectedReferenceCurve as ConcentricCircleLine;
        
        // Current image state for dynamic angle addition
        private BitmapSource? currentBitmapSource;
        private Point currentImageCenter;
        private int currentImageRadius;
        private double currentPixelsPerDegree;
        private ExportChannel currentReferenceScaleChannel = ExportChannel.Y;
        private double currentReferenceScaleMaximum = 1;
        private ConoscopeCoordinateAxisController? coordinateAxisController;
        private ReferenceCurve? coordinateAxisReferenceCurve;
        private ReferencePlotDisplayMode referencePlotDisplayMode;
        private WindowCIE? cieWindow;
        private ConoscopeModelProfile? subscribedModelProfile;
        private const float MinPositiveXyzValue = 0.000001f;
        private const double Conoscope3DInitialHeightScale = 160.0;
        private ConoscopeImageZoomMode imageZoomMode = ConoscopeImageZoomMode.Fit;
        private bool applyCircleFitOnNextRefresh;
        private bool isApplyingImageZoomMode;
        internal ConoscopeViewState State { get; } = new();

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

        private void RefreshModelDependentUi()
        {
            NotifyReferenceStateChanged();
            SetReferencePlotLimits();
        }

        internal void RefreshConoscopeConfiguration()
        {
            RefreshModelDependentUi();
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
        // Category aliases keep call sites readable; every value is stored in State.
        private ConoscopeViewState RenderingConfig => State;
        private ConoscopeViewState PreprocessConfig => State;
        private ConoscopeViewState ColorDifferenceConfig => State;
        private ConoscopeCoordinateAxisParam CoordinateAxisConfig => State.CoordinateAxis;

        private void InitializeLocalViewStateFromDefaults()
        {
            ApplyDefaultRenderingStateFromConfig();
            ApplyDefaultPreprocessStateFromConfig();
            State.ColorDifferenceReferenceMode = ConoscopeConfig.ColorDifferenceReferenceMode;
            State.ColorDifferenceCustomU = ConoscopeConfig.ColorDifferenceCustomU;
            State.ColorDifferenceCustomV = ConoscopeConfig.ColorDifferenceCustomV;
            State.ContrastImageKind = ConoscopeConfig.ContrastReferenceKind;
            InitializeLocalCoordinateAxisState(preserveReferenceState: false);
        }

        private void ApplyDefaultRenderingStateFromConfig()
        {
            State.DisplayChannel = ConoscopeConfig.DisplayChannel;
            State.PseudoColorMap = ConoscopeConfig.PseudoColorMap;
            State.UsePseudoColor = ConoscopeConfig.UsePseudoColor;
            State.UsePseudoColorRangeLimit = ConoscopeConfig.UsePseudoColorRangeLimit;
        }

        private void ApplyDefaultPreprocessStateFromConfig()
        {
            State.ApplyFilterOnOpen = ConoscopeConfig.ApplyFilterOnOpen;
            State.ClampNonPositiveXyzOnLoad = ConoscopeConfig.ClampNonPositiveXyzOnLoad;
            State.FilterType = ConoscopeConfig.FilterType;
            State.FilterKernelSize = ConoscopeConfig.FilterKernelSize;
            State.FilterSigma = ConoscopeConfig.FilterSigma;
            State.FilterD = ConoscopeConfig.FilterD;
            State.FilterSigmaColor = ConoscopeConfig.FilterSigmaColor;
            State.FilterSigmaSpace = ConoscopeConfig.FilterSigmaSpace;
            State.DustRemovalEnabled = ConoscopeConfig.DustRemovalEnabled;
            State.DustRemovalMode = ConoscopeConfig.DustRemovalMode;
            State.DustThresholdPercent = ConoscopeConfig.DustThresholdPercent;
            State.DustMinArea = ConoscopeConfig.DustMinArea;
            State.DustMaxArea = ConoscopeConfig.DustMaxArea;
            State.DustRepairRadius = ConoscopeConfig.DustRepairRadius;

            ImageFilterType filterType = NormalizeFilterType(State.FilterType);
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
            this.DataContext = ConoscopeManager.GetInstance();
            RefreshDisplayControlsFromConfig();
            NotifyReferenceStateChanged();
            InitializePreprocessControls();
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
            NotifyReferenceStateChanged();
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
            CancelDeferredXyzLoad();
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

    }
}
