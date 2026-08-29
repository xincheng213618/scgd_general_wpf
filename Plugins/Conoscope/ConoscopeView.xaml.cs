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
using ColorVision.Common.MVVM;
using System.Runtime.CompilerServices;
using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Processing.Preprocess;
using Conoscope.Presentation.Formatters;
using Conoscope.Presentation.Helpers;
using System.Windows.Threading;
using Conoscope.ApplicationServices.Analysis;
using Conoscope.ApplicationServices.FocusPoints;
using Conoscope.Properties;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using Conoscope.Analysis;
using CVCommCore.CVAlgorithm;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
#pragma warning disable CA1863

namespace Conoscope
{
    /// <summary>
    /// The bindable semantic state of one open document. WPF controls project this
    /// state; they are not used as backing storage.
    /// </summary>
    internal sealed class ConoscopeViewState : ViewModelBase
    {
        private ExportChannel displayChannel = ExportChannel.Y;
        private ColormapTypes pseudoColorMap = ColormapTypes.COLORMAP_JET;
        private bool usePseudoColor = true;
        private bool usePseudoColorRangeLimit = true;
        private bool applyFilterOnOpen = true;
        private bool clampNonPositiveXyzOnLoad = true;
        private ImageFilterType filterType = ImageFilterType.Gaussian;
        private int filterKernelSize = 7;
        private double filterSigma = 1.0;
        private int filterD = 5;
        private double filterSigmaColor = 75;
        private double filterSigmaSpace = 75;
        private bool dustRemovalEnabled;
        private DustRemovalMode dustRemovalMode = DustRemovalMode.DarkSpot;
        private double dustThresholdPercent = 12;
        private int dustMinArea = 1;
        private int dustMaxArea = 500;
        private int dustRepairRadius = 3;
        private ColorDifferenceReferenceMode colorDifferenceReferenceMode = ColorDifferenceReferenceMode.D65;
        private double colorDifferenceCustomU = 0.1978;
        private double colorDifferenceCustomV = 0.4684;
        private ContrastReferenceKind contrastImageKind = ContrastReferenceKind.Black;
        private bool hasDisplayData;
        private bool canUseDerivedChannels;
        private bool canUseContrastChannel;

        public ExportChannel DisplayChannel { get => displayChannel; set => Set(ref displayChannel, value); }
        public ColormapTypes PseudoColorMap { get => pseudoColorMap; set => Set(ref pseudoColorMap, value); }
        public bool UsePseudoColor { get => usePseudoColor; set => Set(ref usePseudoColor, value); }
        public bool UsePseudoColorRangeLimit { get => usePseudoColorRangeLimit; set => Set(ref usePseudoColorRangeLimit, value); }
        public bool ApplyFilterOnOpen { get => applyFilterOnOpen; set => Set(ref applyFilterOnOpen, value); }
        public bool ClampNonPositiveXyzOnLoad { get => clampNonPositiveXyzOnLoad; set => Set(ref clampNonPositiveXyzOnLoad, value); }
        public ImageFilterType FilterType { get => filterType; set => Set(ref filterType, value); }
        public int FilterKernelSize { get => filterKernelSize; set => Set(ref filterKernelSize, value); }
        public double FilterSigma { get => filterSigma; set => Set(ref filterSigma, value); }
        public int FilterD { get => filterD; set => Set(ref filterD, value); }
        public double FilterSigmaColor { get => filterSigmaColor; set => Set(ref filterSigmaColor, value); }
        public double FilterSigmaSpace { get => filterSigmaSpace; set => Set(ref filterSigmaSpace, value); }
        public bool DustRemovalEnabled { get => dustRemovalEnabled; set => Set(ref dustRemovalEnabled, value); }
        public DustRemovalMode DustRemovalMode { get => dustRemovalMode; set => Set(ref dustRemovalMode, value); }
        public double DustThresholdPercent { get => dustThresholdPercent; set => Set(ref dustThresholdPercent, value); }
        public int DustMinArea { get => dustMinArea; set => Set(ref dustMinArea, value); }
        public int DustMaxArea { get => dustMaxArea; set => Set(ref dustMaxArea, value); }
        public int DustRepairRadius { get => dustRepairRadius; set => Set(ref dustRepairRadius, value); }
        public ColorDifferenceReferenceMode ColorDifferenceReferenceMode { get => colorDifferenceReferenceMode; set => Set(ref colorDifferenceReferenceMode, value); }
        public double ColorDifferenceCustomU { get => colorDifferenceCustomU; set => Set(ref colorDifferenceCustomU, value); }
        public double ColorDifferenceCustomV { get => colorDifferenceCustomV; set => Set(ref colorDifferenceCustomV, value); }
        public ContrastReferenceKind ContrastImageKind { get => contrastImageKind; set => Set(ref contrastImageKind, value); }
        public bool HasDisplayData { get => hasDisplayData; private set => Set(ref hasDisplayData, value); }
        public bool CanUseDerivedChannels { get => canUseDerivedChannels; private set => Set(ref canUseDerivedChannels, value); }
        public bool CanUseContrastChannel { get => canUseContrastChannel; private set => Set(ref canUseContrastChannel, value); }
        public ConoscopeCoordinateAxisParam CoordinateAxis { get; } = new();

        internal void SetCapabilities(bool displayDataAvailable, bool derivedChannelsAvailable, bool contrastChannelAvailable)
        {
            HasDisplayData = displayDataAvailable;
            CanUseDerivedChannels = derivedChannelsAvailable;
            CanUseContrastChannel = contrastChannelAvailable;
        }

        internal void RefreshDisplayChannelBinding()
        {
            OnPropertyChanged(nameof(DisplayChannel));
        }

        private bool Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// ConoscopeView.xaml 的交互逻辑
    /// </summary>
    public partial class ConoscopeView : UserControl, IDisposable, IActiveDocumentStatusProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeView));
        private readonly ConoscopeDocument document = new(log);

        private ReferenceCurve? selectedReferenceCurve;
        private PolarAngleLine? selectedPolarLine => selectedReferenceCurve as PolarAngleLine;
        private ConcentricCircleLine? selectedCircleLine => selectedReferenceCurve as ConcentricCircleLine;
        
        // Current image state for dynamic angle addition
        private BitmapSource? currentBitmapSource;
        private Point currentImageCenter;
        private int currentImageRadius;
        private double currentPixelsPerDegree;
        private ConoscopeUvReference? imageCenterColorDifferenceReference;
        private int imageCenterColorDifferenceReferenceVersion = -1;
        private ExportChannel currentReferenceScaleChannel = ExportChannel.Y;
        private double currentReferenceScaleMaximum = 1;
        private ConoscopeCoordinateAxisController? coordinateAxisController;
        private ReferenceCurve? coordinateAxisReferenceCurve;
        private ReferencePlotDisplayMode referencePlotDisplayMode;
        private WindowCIE? cieWindow;
        private ConoscopeModelProfile? subscribedModelProfile;
        private DispatcherOperation? pendingModelProfileRefreshOperation;
        private int appliedProfileMaxAngle;
        private double appliedProfileCalculationDiameterPixels = double.NaN;
        private double appliedProfileManualCoefficient = double.NaN;
        private bool disposed;
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
        public string FileName => document.FileName;
        public OpenCvSharp.Mat? XMat => document.X;
        public OpenCvSharp.Mat? YMat => document.Y;
        public OpenCvSharp.Mat? ZMat => document.Z;
        public bool HasCaptureExposureSummary => !string.IsNullOrWhiteSpace(document.ExposureSummary);
        public string CaptureExposureSummary => document.ExposureSummary ?? Properties.Resources.StatusNotRecorded;

        private void RefreshModelDependentUi()
        {
            NotifyReferenceStateChanged();
            SetReferencePlotLimits();
        }

        internal void RefreshRenderingFromConfig()
        {
            RefreshDisplayControlsFromConfig();
            UpdatePseudoColorMapPreview();
            if (HasDisplayData())
            {
                EnsureSelectedDisplayChannelAvailable();

                RefreshDisplayedImage();
            }
        }

        internal void RefreshGlobalReferenceState()
        {
            ExportChannel previousChannel = GetSelectedDisplayChannel();
            RefreshChannelAvailability();

            if (!HasDisplayData())
            {
                return;
            }

            ExportChannel channel = GetSelectedDisplayChannel();
            bool channelFellBack = channel != previousChannel;
            bool usesGlobalReference = previousChannel == ExportChannel.Contrast
                || (previousChannel == ExportChannel.ColorDifference && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.ReferenceImage);

            if (!usesGlobalReference && !channelFellBack)
            {
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
            if (GetChannelNotReadyReason(channel) == null)
            {
                return;
            }

            State.DisplayChannel = ExportChannel.Y;
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

            if (!string.IsNullOrWhiteSpace(FileName))
            {
                items.Add(new StatusBarMeta
                {
                    Id = "ConoscopeFileType",
                    Name = Properties.Resources.StatusFileType,
                    Description = Path.GetExtension(FileName).ToUpperInvariant(),
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
            document.Changed += Document_Changed;
            document.LoadFailed += Document_LoadFailed;
            InitializeFocusToolbarIcons();
            InitializeLocalViewStateFromDefaults();
            ImageView.FocusCircleCalculationRequested += ImageView_FocusCircleCalculationRequested;
            ImageView.FocusCircleEditRequested += ImageView_FocusCircleEditRequested;
            ImageView.FocusCirclesChanged += ImageView_FocusCirclesChanged;
            ImageView.FocusCircleSelectionChanged += ImageView_FocusCircleSelectionChanged;
        }

        public void OpenConoscope(string filename, string? exposureSummary = null)
        {
            PrepareDisplayStateForNewImage();
            HideCoordinateDragOverlay();
            DisposeCoordinateAxis();
            DisposePseudoColorRangeMasks();
            currentBitmapSource = null;
            imageCenterColorDifferenceReference = null;
            imageCenterColorDifferenceReferenceVersion = -1;
            ImageView.ResetDocument();
            isFocusCircleModeEnabled = false;
            tglFocusCircleMode.IsChecked = false;
            SetFocusCircleToolSelection(FocusCircleInteractionMode.Select);
            UpdateFocusCircleModeState();
            _ = document.OpenAsync(
                filename,
                exposureSummary,
                CreatePreprocessOptions(),
                State.ApplyFilterOnOpen && HasPreprocessEnabled());
            RefreshChannelAvailability();
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PrepareDisplayStateForNewImage()
        {
            State.DisplayChannel = ExportChannel.Y;
            currentReferenceScaleChannel = ExportChannel.Y;
            currentReferenceScaleMaximum = 1;

            if (!IsLoaded)
            {
                return;
            }

            RefreshDisplayControlsFromConfig();
        }

        private void Document_Changed(object? sender, ConoscopeDocumentChangedEventArgs e)
        {
            imageCenterColorDifferenceReference = null;
            imageCenterColorDifferenceReferenceVersion = -1;
            RefreshChannelAvailability();
            if (e.Kind == ConoscopeDocumentChangeKind.InitialDisplayReady)
            {
                try
                {
                    applyCircleFitOnNextRefresh = true;
                    EnsureSelectedDisplayChannelAvailable();
                    RefreshDisplayedImage();
                    SyncCieWindowFromCurrentPointer();
                }
                catch (Exception ex)
                {
                    log.Error($"Conoscope 文档已加载，但初始显示失败: {ex.Message}", ex);
                    MessageBox.Show(ex.Message, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                RefreshChannelAvailability();
            }

            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Document_LoadFailed(object? sender, ConoscopeDocumentLoadFailedEventArgs e)
        {
            if (!e.InitialDisplayCompleted)
            {
                MessageBox.Show(
                    Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgOpenImageFailed, e.Exception.Message),
                    Properties.Resources.TitleError,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RestoreOriginalMats()
        {
            try
            {
                document.Reload(CreatePreprocessOptions());
            }
            finally
            {
                imageCenterColorDifferenceReference = null;
                imageCenterColorDifferenceReferenceVersion = -1;
                RefreshChannelAvailability();
            }
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

        public ConoscopeConfig ConoscopeConfig => ConoscopeManager.Instance.Config;
        private ConoscopeGlobalReferenceStore GlobalReferences => ConoscopeManager.Instance.GlobalReferences;
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

        }

        internal void ApplyWindowRenderingDefaults()
        {
            ApplyDefaultRenderingStateFromConfig();
            RefreshDisplayControlsFromConfig();
            UpdatePseudoColorMapPreview();

            if (HasDisplayData())
            {
                EnsureSelectedDisplayChannelAvailable();
                RefreshDisplayedImage();
            }
        }

        internal void ApplyWindowPreprocessDefaults()
        {
            ApplyDefaultPreprocessStateFromConfig();
        }

        private void InitializeLocalCoordinateAxisState(bool preserveReferenceState)
        {
            ConoscopeCoordinateAxisParam source = CurrentModelProfile.CoordinateAxisParam;
            ConoscopeCoordinateReferenceMode referenceMode = State.CoordinateAxis.ReferenceMode;
            double referenceAngle = State.CoordinateAxis.ReferenceAngle;
            double referenceRadiusAngle = State.CoordinateAxis.ReferenceRadiusAngle;
            bool isInteractionEnabled = State.CoordinateAxis.IsInteractionEnabled;

            State.CoordinateAxis.IsInteractionEnabled = preserveReferenceState ? isInteractionEnabled : source.IsInteractionEnabled;
            State.CoordinateAxis.MaxAngle = source.MaxAngle;
            State.CoordinateAxis.ConoscopeCoefficient = source.ConoscopeCoefficient;
            State.CoordinateAxis.CenterX = source.CenterX;
            State.CoordinateAxis.CenterY = source.CenterY;
            State.CoordinateAxis.AxisRadius = source.AxisRadius;
            State.CoordinateAxis.AzimuthStep = source.AzimuthStep;
            State.CoordinateAxis.PolarStep = source.PolarStep;
            State.CoordinateAxis.LineWidth = source.LineWidth;
            State.CoordinateAxis.AxisBrush = source.AxisBrush;
            State.CoordinateAxis.ReferenceMode = preserveReferenceState ? referenceMode : source.ReferenceMode;
            State.CoordinateAxis.ReferenceAngle = preserveReferenceState ? referenceAngle : source.ReferenceAngle;
            State.CoordinateAxis.ReferenceRadiusAngle = preserveReferenceState
                ? Math.Max(0, Math.Min(referenceRadiusAngle, source.MaxAngle))
                : Math.Max(0, Math.Min(source.ReferenceRadiusAngle, source.MaxAngle));
            State.CoordinateAxis.ReferenceLineWidth = source.ReferenceLineWidth;
            State.CoordinateAxis.ReferenceBrush = source.ReferenceBrush;
            State.CoordinateAxis.IsMaskVisible = source.IsMaskVisible;
            State.CoordinateAxis.MaskOpacity = source.MaskOpacity;
            State.CoordinateAxis.MaskColor = source.MaskColor;
            State.CoordinateAxis.IsTextVisible = source.IsTextVisible;
            State.CoordinateAxis.FontSize = source.FontSize;
            State.CoordinateAxis.TextBrush = source.TextBrush;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            RefreshDisplayControlsFromConfig();
            NotifyReferenceStateChanged();
            AttachCurrentModelProfile();

            ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig_ModelTypeChanged(sender, ConoscopeConfig.CurrentModel);
            InitializePlot(wpfPlotReference, Properties.Resources.ReferenceCurve);
            UpdateReferencePlotDisplayMode();
            UpdateReferencePlotHeader();

            ImageView.ZoomChanged -= ImageView_ZoomChanged;
            ImageView.ZoomChanged += ImageView_ZoomChanged;
            UpdatePseudoColorMapPreview();
            InitializeFocusPointTools();
            UpdatePanModeState();
        }

        private void ImageView_ZoomChanged(object? sender, EventArgs e)
        {
            if (!isApplyingImageZoomMode)
            {
                imageZoomMode = ConoscopeImageZoomMode.Custom;
            }
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
            AttachCurrentModelProfile();
            ApplyCurrentModelDefaults(geometryChanged: true, refreshNow: true);
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

            if (HaveAppliedProfileGeometrySettings()
                || pendingModelProfileRefreshOperation?.Status == DispatcherOperationStatus.Pending)
            {
                return;
            }

            pendingModelProfileRefreshOperation = Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
            {
                pendingModelProfileRefreshOperation = null;
                if (!disposed && !HaveAppliedProfileGeometrySettings())
                {
                    ApplyCurrentModelDefaults(geometryChanged: true, refreshNow: true);
                }
            }));
        }

        private bool HaveAppliedProfileGeometrySettings()
        {
            return appliedProfileMaxAngle == CurrentModelProfile.MaxAngle
                && appliedProfileCalculationDiameterPixels == CurrentModelProfile.CalculationDiameterPixels
                && appliedProfileManualCoefficient == CurrentModelProfile.ManualConoscopeCoefficient;
        }

        internal void ApplyCurrentModelDefaults(bool geometryChanged, bool refreshNow)
        {
            pendingModelProfileRefreshOperation?.Abort();
            pendingModelProfileRefreshOperation = null;
            bool hasDisplayData = HasDisplayData();
            if (hasDisplayData)
            {
                // Stop the old visual before copying the parameter set so a single
                // settings apply does not render the discarded overlay per property.
                DisposeCoordinateAxis();
            }

            InitializeLocalCoordinateAxisState(preserveReferenceState: true);
            appliedProfileMaxAngle = CurrentModelProfile.MaxAngle;
            appliedProfileCalculationDiameterPixels = CurrentModelProfile.CalculationDiameterPixels;
            appliedProfileManualCoefficient = CurrentModelProfile.ManualConoscopeCoefficient;
            RefreshModelDependentUi();
            UpdateReferencePlotHeader();

            if (!refreshNow || !hasDisplayData)
            {
                return;
            }

            if (geometryChanged && State.UsePseudoColorRangeLimit)
            {
                RefreshDisplayedImage();
                return;
            }

            // Axis styling and geometry do not change image pixels when the
            // pseudo-color range mask is disabled. Rebuild only the overlay and
            // derived geometry instead of re-running statistics and bitmap render.
            DisposeCoordinateAxis();
            CreateAndAnalyzePolarLines();
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
            if (disposed)
            {
                return;
            }

            disposed = true;
            pendingModelProfileRefreshOperation?.Abort();
            pendingModelProfileRefreshOperation = null;
            document.Changed -= Document_Changed;
            document.LoadFailed -= Document_LoadFailed;
            ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            if (subscribedModelProfile != null)
            {
                subscribedModelProfile.PropertyChanged -= CurrentModelProfile_PropertyChanged;
                subscribedModelProfile = null;
            }
            ImageView.ZoomChanged -= ImageView_ZoomChanged;
            ImageView.FocusCircleCalculationRequested -= ImageView_FocusCircleCalculationRequested;
            ImageView.FocusCircleEditRequested -= ImageView_FocusCircleEditRequested;
            ImageView.FocusCirclesChanged -= ImageView_FocusCirclesChanged;
            ImageView.FocusCircleSelectionChanged -= ImageView_FocusCircleSelectionChanged;
            cieWindow?.Close();
            cieWindow = null;
            document.Dispose();
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

        internal void ApplyPreprocessFromCurrentSettings()
        {
            try
            {
                if (!HasXyzData())
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!HasPreprocessEnabled())
                {
                    RestoreOriginalMats();
                    RefreshDisplayedImage();
                    log.Info("已恢复原始数据");
                    MessageBox.Show(Properties.Resources.MsgOriginalDataRestored, Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                RestoreOriginalMats();
                log.Info($"开始应用预处理: clamp={State.ClampNonPositiveXyzOnLoad}, dust={State.DustRemovalEnabled}, filter={State.FilterType}");
                try
                {
                    ApplyPreprocessToCurrentMats();
                }
                catch
                {
                    try
                    {
                        // Recovery rereads the source only on the exceptional path. It
                        // keeps normal 6200² processing on the existing low-peak path.
                        RestoreOriginalMats();
                    }
                    catch (Exception recoveryException)
                    {
                        log.Error($"预处理失败后恢复原始数据失败: {recoveryException.Message}", recoveryException);
                    }

                    throw;
                }

                RefreshDisplayedImage();

                log.Info("预处理应用成功，数据已更新");
                MessageBox.Show(Properties.Resources.MsgPreprocessApplied, Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"应用滤波失败: {ex.Message}", ex);
                RecoverDisplayAfterPreprocessFailure();
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgPreprocessFailedDetail, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyPreprocessToCurrentMats()
        {
            try
            {
                document.ApplyPreprocess(CreatePreprocessOptions());
            }
            finally
            {
                imageCenterColorDifferenceReference = null;
                imageCenterColorDifferenceReferenceVersion = -1;
                RefreshChannelAvailability();
            }
        }

        private void RecoverDisplayAfterPreprocessFailure()
        {
            if (HasDisplayData())
            {
                try
                {
                    RefreshDisplayedImage();
                    StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }
                catch (Exception refreshException)
                {
                    log.Error($"预处理失败后刷新显示失败: {refreshException.Message}", refreshException);
                }
            }

            currentBitmapSource = null;
            imageCenterColorDifferenceReference = null;
            imageCenterColorDifferenceReferenceVersion = -1;
            DisposeCoordinateAxis();
            DisposePseudoColorRangeMasks();
            ImageView.ResetDocument();
            UpdatePseudoColorLegendVisibility(false);
            RefreshChannelAvailability();
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        private ConoscopePreprocessOptions CreatePreprocessOptions()
        {
            int minArea = Math.Max(1, State.DustMinArea);
            int maxArea = Math.Max(minArea, State.DustMaxArea);
            ImageFilterType filterType = NormalizeFilterType(State.FilterType);

            return new ConoscopePreprocessOptions(
                State.ClampNonPositiveXyzOnLoad,
                MinPositiveXyzValue,
                State.DustRemovalEnabled,
                new DustRemovalOptions(
                    State.DustRemovalMode,
                    State.DustThresholdPercent,
                    minArea,
                    maxArea,
                    Math.Max(1, State.DustRepairRadius)),
                new ImageFilterOptions(
                    filterType,
                    ConoscopeNumericHelper.NormalizeOddKernelSize(State.FilterKernelSize),
                    State.FilterSigma,
                    Math.Max(1, State.FilterD),
                    State.FilterSigmaColor,
                    State.FilterSigmaSpace));
        }

        private static ImageFilterType NormalizeFilterType(ImageFilterType filterType)
        {
            return Enum.IsDefined(filterType) ? filterType : ImageFilterType.None;
        }

        private bool HasPreprocessEnabled()
        {
            return State.ClampNonPositiveXyzOnLoad
                || State.DustRemovalEnabled
                || NormalizeFilterType(State.FilterType) != ImageFilterType.None;
        }

        private OpenCvSharp.Mat? pseudoColorRangeMask;
        private OpenCvSharp.Mat? pseudoColorRangeOutsideMask;
        private int pseudoColorRangeMaskWidth;
        private int pseudoColorRangeMaskHeight;
        private int pseudoColorRangeMaskCenterX;
        private int pseudoColorRangeMaskCenterY;
        private int pseudoColorRangeMaskRadius;

        private void RefreshDisplayedImage()
        {
            if (!HasDisplayData())
            {
                UpdatePseudoColorLegendVisibility(false);
                return;
            }

            EnsureSelectedDisplayChannelAvailable();
            ExportChannel displayChannel = GetSelectedDisplayChannel();
            OpenCvSharp.Mat displayBaseMat = YMat!;
            OpenCvSharp.Mat? rangeMask = GetPseudoColorRangeMask(displayBaseMat.Width, displayBaseMat.Height);
            ConoscopePseudoColorRenderResult renderResult = ConoscopePseudoColorRenderer.Render(
                XMat ?? displayBaseMat,
                YMat!,
                ZMat ?? displayBaseMat,
                displayChannel,
                State.PseudoColorMap,
                () => CreateColorDifferenceMat() ?? throw new InvalidOperationException(GetChannelNotReadyReason(ExportChannel.ColorDifference) ?? Properties.Resources.MsgLoadImageFirstColorDiff),
                () => CreateContrastMat() ?? throw new InvalidOperationException(GetChannelNotReadyReason(ExportChannel.Contrast) ?? Properties.Resources.MsgLoadImageFirst),
                State.UsePseudoColor,
                rangeMask,
                rangeMask == null ? null : pseudoColorRangeOutsideMask);

            UpdateReferenceScale(renderResult.Channel, renderResult.MaxValue);
            if (State.UsePseudoColor)
            {
                UpdatePseudoColorLegend(renderResult.Channel, renderResult.MinValue, renderResult.MaxValue);
            }
            else
            {
                UpdatePseudoColorLegendVisibility(false);
            }

            DisposeCoordinateAxis();
            ImageView.ReplaceDisplayedImage(renderResult.Bitmap);
            CreateAndAnalyzePolarLines();
            ApplyZoomAfterDisplayRefresh();
        }

        private void UpdatePseudoColorLegend(ExportChannel channel, double minValue, double maxValue)
        {
            UpdateReferenceScale(channel, maxValue);

            if (tbPseudoColorLegendTitle == null || tbPseudoColorLegendMin == null || tbPseudoColorLegendMax == null)
            {
                return;
            }

            UpdatePseudoColorMapPreview();
            tbPseudoColorLegendTitle.Text = ConoscopeChannelDisplayFormatter.GetLabel(channel);
            tbPseudoColorLegendMin.Text = ConoscopeChannelDisplayFormatter.FormatValue(minValue, channel);
            tbPseudoColorLegendMax.Text = ConoscopeChannelDisplayFormatter.FormatValue(maxValue, channel);
            UpdatePseudoColorLegendVisibility(true);
        }

        private void UpdateReferenceScale(ExportChannel channel, double maxValue)
        {
            currentReferenceScaleChannel = channel;
            currentReferenceScaleMaximum = maxValue;
        }

        private OpenCvSharp.Mat? GetPseudoColorRangeMask(int imageWidth, int imageHeight)
        {
            if (!State.UsePseudoColorRangeLimit)
            {
                return null;
            }

            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return null;
            }

            double pixelsPerDegree = CurrentModelProfile.GetConoscopeCoefficient(imageWidth, imageHeight);
            double radiusValue = MaxAngle * pixelsPerDegree;
            if (!double.IsFinite(radiusValue) || radiusValue <= 0)
            {
                return null;
            }

            int centerX = (int)Math.Round(imageWidth / 2.0);
            int centerY = (int)Math.Round(imageHeight / 2.0);
            int radius = Math.Max(1, (int)Math.Round(radiusValue));

            if (pseudoColorRangeMask != null
                && pseudoColorRangeOutsideMask != null
                && pseudoColorRangeMaskWidth == imageWidth
                && pseudoColorRangeMaskHeight == imageHeight
                && pseudoColorRangeMaskCenterX == centerX
                && pseudoColorRangeMaskCenterY == centerY
                && pseudoColorRangeMaskRadius == radius)
            {
                return pseudoColorRangeMask;
            }

            DisposePseudoColorRangeMasks();

            pseudoColorRangeMaskWidth = imageWidth;
            pseudoColorRangeMaskHeight = imageHeight;
            pseudoColorRangeMaskCenterX = centerX;
            pseudoColorRangeMaskCenterY = centerY;
            pseudoColorRangeMaskRadius = radius;

            pseudoColorRangeMask = new OpenCvSharp.Mat(imageHeight, imageWidth, OpenCvSharp.MatType.CV_8UC1, OpenCvSharp.Scalar.All(0));
            OpenCvSharp.Cv2.Circle(
                pseudoColorRangeMask,
                new OpenCvSharp.Point(centerX, centerY),
                radius,
                OpenCvSharp.Scalar.All(255),
                -1,
                OpenCvSharp.LineTypes.Link8);

            pseudoColorRangeOutsideMask = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.BitwiseNot(pseudoColorRangeMask, pseudoColorRangeOutsideMask);
            return pseudoColorRangeMask;
        }

        private void DisposePseudoColorRangeMasks()
        {
            pseudoColorRangeMask?.Dispose();
            pseudoColorRangeMask = null;
            pseudoColorRangeOutsideMask?.Dispose();
            pseudoColorRangeOutsideMask = null;
            pseudoColorRangeMaskWidth = 0;
            pseudoColorRangeMaskHeight = 0;
            pseudoColorRangeMaskCenterX = 0;
            pseudoColorRangeMaskCenterY = 0;
            pseudoColorRangeMaskRadius = 0;
        }

        private void UpdatePseudoColorMapPreview()
        {
            if (imgPseudoColorLegend == null)
            {
                return;
            }

            imgPseudoColorLegend.Source = ColormapConstats.CreatePreviewImage(State.PseudoColorMap);
        }

        private void UpdatePseudoColorLegendVisibility(bool isVisible)
        {
            if (PseudoColorLegendPanel == null)
            {
                return;
            }

            PseudoColorLegendPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool HasXyzData()
        {
            return XMat != null && YMat != null && ZMat != null;
        }

        private bool HasDisplayData()
        {
            return YMat != null;
        }

        private void RefreshChannelAvailability()
        {
            if (GetChannelNotReadyReason(State.DisplayChannel) != null)
            {
                State.DisplayChannel = ExportChannel.Y;
            }

            UpdateStateCapabilities();
        }

        private void UpdateStateCapabilities()
        {
            State.SetCapabilities(
                HasDisplayData(),
                HasXyzData(),
                HasDisplayData() && CanRefreshContrastDisplay());
        }

        private ExportChannel GetSelectedDisplayChannel() => State.DisplayChannel;

        private void UpdatePanModeState()
        {
            bool isFocusCircleInteractionEnabled = ImageView.InteractionMode != FocusCircleInteractionMode.Browse;
            ImageView.SetPanModifier(isFocusCircleInteractionEnabled ? ModifierKeys.Control : ModifierKeys.None);
            if (!isFocusCircleInteractionEnabled)
            {
                ImageView.ResetInteractionCursor();
            }
        }

        private void btnCircleFit_Click(object sender, RoutedEventArgs e)
        {
            ApplyCircleFitZoomMode();
        }

        internal void OpenCieForCurrentView()
        {
            if (!HasXyzData() || currentBitmapSource == null || coordinateAxisController == null)
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
            SyncCieWindowFromCurrentPointer();
        }

        private void SyncCieWindowFromCurrentPointer()
        {
            if (cieWindow == null || coordinateAxisController == null)
            {
                return;
            }

            Point point = ImageView.GetPointerPosition();
            if (!coordinateAxisController.Axis.ContainsInteractivePoint(point))
            {
                return;
            }

            UpdateCieWindowSelection(point);
        }

        private void ApplyZoomAfterDisplayRefresh()
        {
            if (applyCircleFitOnNextRefresh)
            {
                applyCircleFitOnNextRefresh = false;
                imageZoomMode = ConoscopeImageZoomMode.CircleFit;
            }

            switch (imageZoomMode)
            {
                case ConoscopeImageZoomMode.ActualSize:
                    ApplyImageZoomMode(ConoscopeImageZoomMode.ActualSize, ImageView.ZoomActualSize);
                    break;
                case ConoscopeImageZoomMode.Fill:
                    ApplyImageZoomMode(ConoscopeImageZoomMode.Fill, ImageView.ZoomToFill);
                    break;
                case ConoscopeImageZoomMode.CircleFit:
                    ApplyImageZoomMode(ConoscopeImageZoomMode.CircleFit, () =>
                    {
                        if (!TryApplyCircleFitZoom())
                        {
                            ImageView.ZoomToFit();
                        }
                    });
                    break;
                case ConoscopeImageZoomMode.Custom:
                    break;
                case ConoscopeImageZoomMode.Fit:
                default:
                    ApplyImageZoomMode(ConoscopeImageZoomMode.Fit, () => ImageView.UpdateZoomAndScale());
                    break;
            }
        }

        private void ApplyCircleFitZoomMode()
        {
            if (!HasDisplayData())
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ApplyImageZoomMode(ConoscopeImageZoomMode.CircleFit, () =>
            {
                if (!TryApplyCircleFitZoom())
                {
                    ImageView.ZoomToFit();
                }
            });
        }

        private void ApplyImageZoomMode(ConoscopeImageZoomMode zoomMode, Action zoomAction)
        {
            imageZoomMode = zoomMode;
            isApplyingImageZoomMode = true;
            try
            {
                zoomAction();
            }
            finally
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => isApplyingImageZoomMode = false));
            }
        }

        private bool TryApplyCircleFitZoom()
        {
            if (!TryGetCurrentCircleBounds(out Rect circleBounds))
            {
                return false;
            }

            ImageView.ZoomToImageRect(circleBounds);
            return true;
        }

        private bool TryGetCurrentCircleBounds(out Rect circleBounds)
        {
            circleBounds = Rect.Empty;

            int imageWidth = currentBitmapSource?.PixelWidth ?? YMat?.Width ?? 0;
            int imageHeight = currentBitmapSource?.PixelHeight ?? YMat?.Height ?? 0;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return false;
            }

            Point center = currentImageCenter;
            double radius = currentImageRadius;
            if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || radius <= 0)
            {
                center = new Point(imageWidth / 2.0, imageHeight / 2.0);
                double pixelsPerDegree = CurrentModelProfile.GetConoscopeCoefficient(imageWidth, imageHeight);
                radius = MaxAngle * pixelsPerDegree;
            }

            if (!double.IsFinite(radius) || radius <= 0)
            {
                return false;
            }

            double left = Math.Max(0, center.X - radius);
            double top = Math.Max(0, center.Y - radius);
            double right = Math.Min(imageWidth, center.X + radius);
            double bottom = Math.Min(imageHeight, center.Y + radius);
            double width = right - left;
            double height = bottom - top;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            circleBounds = new Rect(left, top, width, height);
            return true;
        }

        internal void Open3DForCurrentView()
        {
            ExportChannel channel = GetSelectedDisplayChannel();
            string? channelError = GetChannelNotReadyReason(channel);
            if (currentBitmapSource == null || channelError != null)
            {
                MessageBox.Show(channelError ?? Properties.Resources.Msg3DViewNotReady, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                OpenCvSharp.Mat fallback = YMat!;
                WriteableBitmap heightBitmap = ConoscopePseudoColorRenderer.CreateHeightMapBitmap(
                    XMat ?? fallback,
                    YMat!,
                    ZMat ?? fallback,
                    channel,
                    () => CreateColorDifferenceMat() ?? throw new InvalidOperationException(GetChannelNotReadyReason(ExportChannel.ColorDifference) ?? Properties.Resources.MsgLoadImageFirstColorDiff),
                    () => CreateContrastMat() ?? throw new InvalidOperationException(GetChannelNotReadyReason(ExportChannel.Contrast) ?? Properties.Resources.MsgLoadImageFirst),
                    currentImageCenter,
                    currentImageRadius);
                Window3D window3D = new(heightBitmap, Conoscope3DInitialHeightScale)
                {
                    Owner = Window.GetWindow(this)
                };
                window3D.Show();
            }
            catch (Exception ex)
            {
                log.Error("打开 Conoscope 3D 视图失败", ex);
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.Msg3DViewOpenFailed, ex.Message), Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public ContrastReferenceKind GetCurrentContrastImageKind()
        {
            return State.ContrastImageKind;
        }

        private ContrastReferenceKind GetRequiredContrastReferenceKind()
        {
            return State.ContrastImageKind == ContrastReferenceKind.Black
                ? ContrastReferenceKind.White
                : ContrastReferenceKind.Black;
        }

        private static string GetContrastReferenceKindText(ContrastReferenceKind kind)
        {
            return kind == ContrastReferenceKind.Black ? Properties.Resources.ContrastReferenceBlackField : Properties.Resources.ContrastReferenceWhiteField;
        }

        private void ApplyContrastImageKind(ContrastReferenceKind kind, bool refreshDisplay)
        {
            if (State.ContrastImageKind == kind)
            {
                return;
            }

            ContrastReferenceKind previousKind = State.ContrastImageKind;
            State.ContrastImageKind = kind;
            UpdateStateCapabilities();

            if (!refreshDisplay || GetSelectedDisplayChannel() != ExportChannel.Contrast || !HasDisplayData())
            {
                return;
            }

            if (!EnsureChannelReady(ExportChannel.Contrast, Properties.Resources.TitleContrastCalc))
            {
                State.ContrastImageKind = previousKind;
                UpdateStateCapabilities();
                return;
            }

            try
            {
                RefreshDisplayedImage();
                UpdateReferencePlot();
            }
            catch (Exception ex)
            {
                State.ContrastImageKind = previousKind;
                UpdateStateCapabilities();
                RestoreDisplayAfterRejectedStateChange();
                log.Error($"切换对比度图像类型失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, Properties.Resources.GroupContrast, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool CanRefreshContrastDisplay()
        {
            return GetChannelNotReadyReason(ExportChannel.Contrast) == null;
        }

        private OpenCvSharp.Mat? CreateContrastMat()
        {
            if (YMat == null || !CanRefreshContrastDisplay())
            {
                return null;
            }

            ContrastReferenceKind referenceKind = GetRequiredContrastReferenceKind();
            return ConoscopeColorimetry.CreateContrastMat(YMat, GlobalReferences.GetContrastReferenceYMat(referenceKind)!, referenceKind);
        }

        private double GetContrastValue(int ix, int iy, double currentY)
        {
            ContrastReferenceKind referenceKind = GetRequiredContrastReferenceKind();
            OpenCvSharp.Mat? referenceYMat = GlobalReferences.GetContrastReferenceYMat(referenceKind);
            if (referenceYMat == null || YMat == null)
            {
                return double.NaN;
            }

            if (YMat.Width != referenceYMat.Width || YMat.Height != referenceYMat.Height)
            {
                return double.NaN;
            }

            if (ix < 0 || iy < 0 || ix >= referenceYMat.Width || iy >= referenceYMat.Height)
            {
                return double.NaN;
            }

            double referenceY = referenceYMat.At<float>(iy, ix);
            return ConoscopeColorimetry.CalculateContrast(currentY, referenceY, referenceKind);
        }

        public void SaveCurrentAsGlobalContrastReference(ContrastReferenceKind referenceKind)
        {
            if (YMat == null)
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleContrastCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GlobalReferences.SaveContrastReference(referenceKind, YMat, FileName);
        }

        private void ApplyColorDifferenceReferenceMode(ColorDifferenceReferenceMode mode, bool refreshDisplay)
        {
            ColorDifferenceReferenceMode previousMode = State.ColorDifferenceReferenceMode;
            State.ColorDifferenceReferenceMode = mode;
            if (refreshDisplay
                && GetSelectedDisplayChannel() == ExportChannel.ColorDifference
                && !EnsureChannelReady(ExportChannel.ColorDifference, Properties.Resources.PanelColorDiff))
            {
                State.ColorDifferenceReferenceMode = previousMode;
                return;
            }

            try
            {
                RefreshColorDifferenceDisplayIfNeeded(refreshDisplay);
            }
            catch (Exception ex)
            {
                State.ColorDifferenceReferenceMode = previousMode;
                RestoreDisplayAfterRejectedStateChange();
                log.Error($"刷新色差显示失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplyColorDifferenceCustomReference(double u, double v, bool refreshDisplay)
        {
            double previousU = State.ColorDifferenceCustomU;
            double previousV = State.ColorDifferenceCustomV;
            State.ColorDifferenceCustomU = u;
            State.ColorDifferenceCustomV = v;
            if (refreshDisplay
                && GetSelectedDisplayChannel() == ExportChannel.ColorDifference
                && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.Custom
                && !EnsureChannelReady(ExportChannel.ColorDifference, Properties.Resources.PanelColorDiff))
            {
                State.ColorDifferenceCustomU = previousU;
                State.ColorDifferenceCustomV = previousV;
                return;
            }

            try
            {
                RefreshColorDifferenceDisplayIfNeeded(refreshDisplay && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.Custom);
            }
            catch (Exception ex)
            {
                State.ColorDifferenceCustomU = previousU;
                State.ColorDifferenceCustomV = previousV;
                RestoreDisplayAfterRejectedStateChange();
                log.Error($"刷新色差显示失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshColorDifferenceDisplayIfNeeded(bool refreshDisplay)
        {
            if (!refreshDisplay || GetSelectedDisplayChannel() != ExportChannel.ColorDifference || !HasXyzData())
            {
                return;
            }

            RefreshDisplayedImage();
            UpdateReferencePlot();
        }

        internal void SetColorDifferenceReferenceMode(ColorDifferenceReferenceMode mode)
        {
            if (State.ColorDifferenceReferenceMode == mode)
            {
                return;
            }

            ApplyColorDifferenceReferenceMode(mode, refreshDisplay: true);
        }

        internal void SetColorDifferenceCustomReference(double u, double v)
        {
            ApplyColorDifferenceCustomReference(u, v, refreshDisplay: true);
        }

        public void SaveCurrentAsGlobalColorDifferenceReference()
        {
            if (!HasXyzData() || XMat == null || YMat == null || ZMat == null)
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirstColorDiff, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using OpenCvSharp.Mat referenceUMat = ConoscopeColorimetry.CreateChannelMat(XMat, YMat, ZMat, ExportChannel.CieU);
            using OpenCvSharp.Mat referenceVMat = ConoscopeColorimetry.CreateChannelMat(XMat, YMat, ZMat, ExportChannel.CieV);
            GlobalReferences.SaveColorDifferenceReference(referenceUMat, referenceVMat, FileName);

            ApplyColorDifferenceReferenceMode(ColorDifferenceReferenceMode.ReferenceImage, refreshDisplay: true);
        }

        private ColorDifferenceReferenceMode GetSelectedColorDifferenceReferenceMode()
        {
            return State.ColorDifferenceReferenceMode;
        }

        private static ConoscopeUvReference GetStandardColorDifferenceReference(ColorDifferenceReferenceMode mode)
        {
            return mode switch
            {
                ColorDifferenceReferenceMode.D65 => new ConoscopeUvReference(0.1978, 0.4684),
                ColorDifferenceReferenceMode.D50 => new ConoscopeUvReference(0.2009, 0.4707),
                ColorDifferenceReferenceMode.A => new ConoscopeUvReference(0.2560, 0.5242),
                ColorDifferenceReferenceMode.D75 => new ConoscopeUvReference(0.1952, 0.4670),
                _ => throw new InvalidOperationException(Properties.Resources.MsgNoFixedLightSource)
            };
        }

        private bool TryParseCustomColorDifferenceReference(out ConoscopeUvReference reference)
        {
            reference = default;
            double u = State.ColorDifferenceCustomU;
            double v = State.ColorDifferenceCustomV;
            if (!double.IsFinite(u) || !double.IsFinite(v))
            {
                return false;
            }

            reference = new ConoscopeUvReference(u, v);
            return true;
        }

        private ConoscopeUvReference? TryResolvePointColorDifferenceReference()
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode is ColorDifferenceReferenceMode.D65 or ColorDifferenceReferenceMode.D50 or ColorDifferenceReferenceMode.A or ColorDifferenceReferenceMode.D75)
            {
                return GetStandardColorDifferenceReference(mode);
            }

            if (mode == ColorDifferenceReferenceMode.Custom)
            {
                return TryParseCustomColorDifferenceReference(out ConoscopeUvReference customReference) ? customReference : null;
            }

            if (mode == ColorDifferenceReferenceMode.ImageCenter)
            {
                return TryCalculateImageCenterColorDifferenceReference();
            }

            return null;
        }

        private ConoscopeUvReference? TryCalculateImageCenterColorDifferenceReference()
        {
            int dataVersion = document.DataVersion;
            if (imageCenterColorDifferenceReferenceVersion == dataVersion
                && imageCenterColorDifferenceReference.HasValue)
            {
                return imageCenterColorDifferenceReference;
            }

            imageCenterColorDifferenceReference = null;
            imageCenterColorDifferenceReferenceVersion = dataVersion;

            if (XMat == null || YMat == null || ZMat == null)
            {
                return null;
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
                return null;
            }

            imageCenterColorDifferenceReference = new ConoscopeUvReference(sumU / count, sumV / count);
            return imageCenterColorDifferenceReference;
        }

        private OpenCvSharp.Mat? CreateColorDifferenceMat()
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                return null;
            }

            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode == ColorDifferenceReferenceMode.ReferenceImage)
            {
                if (!CanRefreshColorDifferenceDisplay()) return null;
                return ConoscopeColorimetry.CreateColorDifferenceMat(XMat, YMat, ZMat, GlobalReferences.ColorDifferenceReferenceUMat!, GlobalReferences.ColorDifferenceReferenceVMat!);
            }

            ConoscopeUvReference? reference = TryResolvePointColorDifferenceReference();
            if (reference == null) return null;
            return ConoscopeColorimetry.CreateColorDifferenceMat(XMat, YMat, ZMat, reference.Value.U, reference.Value.V);
        }

        private bool CanRefreshColorDifferenceDisplay()
        {
            return GetChannelNotReadyReason(ExportChannel.ColorDifference) == null;
        }

        private double GetChannelValue(RgbSample sample, ExportChannel channel)
        {
            return GetChannelValue(sample.DX, sample.DY, sample.X, sample.Y, sample.Z, channel);
        }

        private double GetChannelValue(int ix, int iy, double X, double Y, double Z, ExportChannel channel)
        {
            if (channel == ExportChannel.ColorDifference)
            {
                return GetColorDifferenceValue(ix, iy, X, Y, Z);
            }

            if (channel == ExportChannel.Contrast)
            {
                return GetContrastValue(ix, iy, Y);
            }

            return ConoscopeColorimetry.GetChannelValue(X, Y, Z, channel);
        }

        private double GetColorDifferenceValue(int ix, int iy, double X, double Y, double Z)
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();

            if (mode == ColorDifferenceReferenceMode.ReferenceImage)
            {
                if (GlobalReferences.ColorDifferenceReferenceUMat == null || GlobalReferences.ColorDifferenceReferenceVMat == null)
                {
                    return 0;
                }

                int sx = ConoscopeNumericHelper.ClampToInt(ix, 0, GlobalReferences.ColorDifferenceReferenceUMat.Width - 1);
                int sy = ConoscopeNumericHelper.ClampToInt(iy, 0, GlobalReferences.ColorDifferenceReferenceUMat.Height - 1);
                return ConoscopeColorimetry.CalculateColorDifference(X, Y, Z, GlobalReferences.ColorDifferenceReferenceUMat.At<float>(sy, sx), GlobalReferences.ColorDifferenceReferenceVMat.At<float>(sy, sx));
            }

            ConoscopeUvReference? reference = TryResolvePointColorDifferenceReference();
            if (reference == null) return 0;
            return ConoscopeColorimetry.CalculateColorDifference(X, Y, Z, reference.Value.U, reference.Value.V);
        }

        private void NotifyReferenceStateChanged()
        {
            SyncReferenceInteractionToggle();
        }

        private void InitializeCoordinateAxis(Point center, int radius)
        {
            ConoscopeCoordinateAxisParam axisParam = State.CoordinateAxis;
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
            coordinateAxisController = new ConoscopeCoordinateAxisController(ImageView.DrawingCanvas, ImageView.Viewport, axisParam);
            coordinateAxisController.ReferenceChanged += CoordinateAxisController_ReferenceChanged;
            coordinateAxisController.PointerMoved += CoordinateAxisController_PointerMoved;
            coordinateAxisController.PointerLeft += CoordinateAxisController_PointerLeft;
            coordinateAxisController.Configure(center, radius, MaxAngle, currentPixelsPerDegree);
            coordinateAxisController.Show();
            UpdateReferencePlotHeader();
        }

        private void CoordinateAxisParam_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifyReferenceStateChanged();

            if (e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceMode))
            {
                ApplyCoordinateAxisReference();
                return;
            }

            if (e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceAngle)
                || e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceRadiusAngle))
            {
                if (coordinateAxisController?.IsUpdatingReference != true)
                {
                    ApplyCoordinateAxisReference();
                }
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

            if (!e.IsValueChanged)
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
            double azimuthAngle = FocusPointMeasurementService.GetFullAzimuthAngle(e.Position, currentImageCenter);
            double polarAngle = FocusPointMeasurementService.GetPolarRadiusAngle(e.Position, currentImageCenter, currentImageRadius, MaxAngle);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.ReferenceFormat, GetReferenceValueText(e.Mode, e.Angle, e.RadiusAngle)));
            builder.AppendLine(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.PixelCoordFormat, sample.ImageX, sample.ImageY));
            builder.AppendLine(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.PolarCoordFormat, azimuthAngle.ToString("F2"), polarAngle.ToString("F2")));
            builder.AppendLine($"{ConoscopeChannelDisplayFormatter.GetLabel(displayChannel)}: {ConoscopeChannelDisplayFormatter.FormatValue(displayValue, displayChannel)}");
            builder.AppendLine($"XYZ: X={sample.X:F4}, Y={sample.Y:F4}, Z={sample.Z:F4}");
            builder.AppendLine($"xy: x={sample.Chromaticity.x:F6}, y={sample.Chromaticity.y:F6}");
            builder.Append($"uv: u={sample.Chromaticity.u:F6}, v={sample.Chromaticity.v:F6}, CCT={(sample.Chromaticity.Cct > 0 ? $"{sample.Chromaticity.Cct:F0}K" : "--")}");
            return builder.ToString();
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

            int imageX = ConoscopeNumericHelper.ClampToInt((int)Math.Round(position.X), 0, imageWidth - 1);
            int imageY = ConoscopeNumericHelper.ClampToInt((int)Math.Round(position.Y), 0, imageHeight - 1);

            int xyzWidth = YMat?.Width ?? XMat?.Width ?? ZMat?.Width ?? imageWidth;
            int xyzHeight = YMat?.Height ?? XMat?.Height ?? ZMat?.Height ?? imageHeight;
            if (xyzWidth <= 0 || xyzHeight <= 0)
            {
                return false;
            }

            int xyzX = ConoscopeNumericHelper.ClampToInt(imageX, 0, xyzWidth - 1);
            int xyzY = ConoscopeNumericHelper.ClampToInt(imageY, 0, xyzHeight - 1);
            ExtractXYZValues(xyzX, xyzY, out double X, out double Y, out double Z);
            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(X, Y, Z);
            sample = new PixelChromaticitySample(imageX, imageY, xyzX, xyzY, X, Y, Z, chromaticity);
            return true;
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

            PolarAngleLine curve = coordinateAxisReferenceCurve as PolarAngleLine ?? new PolarAngleLine();
            curve.Angle = angle;
            curve.Samples.Clear();

            (Point Start, Point End) endpoints = ConoscopeCoordinateAxisVisual.GetAzimuthLineEndpoints(currentImageCenter, currentImageRadius, angle);
            ExtractRgbAlongLine(curve, endpoints.End, endpoints.Start);

            coordinateAxisReferenceCurve = curve;
            selectedReferenceCurve = curve;
            SetReferencePlotLimits();
            UpdateReferencePlotHeader();
            UpdateReferencePlot();
        }

        private void UpdateCoordinateAxisPolar(double radiusAngle)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            radiusAngle = Math.Clamp(radiusAngle, 0, MaxAngle);

            ConcentricCircleLine curve = coordinateAxisReferenceCurve as ConcentricCircleLine ?? new ConcentricCircleLine();
            curve.RadiusAngle = radiusAngle;
            curve.Samples.Clear();
            ExtractRgbAlongCircle(curve, currentImageCenter, radiusAngle);

            coordinateAxisReferenceCurve = curve;
            selectedReferenceCurve = curve;
            SetReferencePlotLimits();
            UpdateReferencePlotHeader();
            UpdateReferencePlot();
        }

        private void DisposeCoordinateAxis()
        {
            if (coordinateAxisController != null)
            {
                coordinateAxisController.ReferenceChanged -= CoordinateAxisController_ReferenceChanged;
                coordinateAxisController.PointerMoved -= CoordinateAxisController_PointerMoved;
                coordinateAxisController.PointerLeft -= CoordinateAxisController_PointerLeft;
                coordinateAxisController.Axis.Attribute.PropertyChanged -= CoordinateAxisParam_PropertyChanged;
                coordinateAxisController.Dispose();
                coordinateAxisController = null;
            }

            selectedReferenceCurve = null;
            coordinateAxisReferenceCurve = null;
        }

        private void CreateAndAnalyzePolarLines()
        {
            try
            {
                if (ImageView.Source == null)
                {
                    log.Warn("图像未加载，无法创建极角线");
                    return;
                }

                BitmapSource? bitmapSource = ImageView.Source as BitmapSource;
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

                currentBitmapSource = bitmapSource;
                currentImageCenter = center;
                currentImageRadius = radius;

                ImageView.SetFocusCircleBoundary(center, radius);

                InitializeCoordinateAxis(center, radius);

                log.Info($"图像尺寸: {imageWidth}x{imageHeight}, 中心: ({center.X}, {center.Y}), 半径: {radius}, 系数: {currentPixelsPerDegree:F6}px/deg");

                selectedReferenceCurve = null;
                coordinateAxisReferenceCurve = null;

                coordinateAxisController?.BringToFront();
                ApplyCoordinateAxisReference();
            }
            catch (Exception ex)
            {
                log.Error($"创建极角线失败: {ex.Message}", ex);
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgPolarLineCreateFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializePlot(ScottPlot.WPF.WpfPlot plot, string title)
        {
            plot.Plot.Title(title);
            plot.Plot.XLabel("Degrees");
            plot.Plot.YLabel(ConoscopeChannelDisplayFormatter.GetAxisLabel(ExportChannel.Y));
            plot.Plot.Legend.FontName = ScottPlot.Fonts.Detect("中文");

            string fontSample = "中文 Luminance Voltage";
            plot.Plot.Axes.Title.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Bottom.Label.FontName = ScottPlot.Fonts.Detect(fontSample);

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

        private void UpdateReferencePlotHeader()
        {
            ConoscopeCoordinateAxisParam axisParam = State.CoordinateAxis;
            tbReferenceMode.Text = axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine ? Properties.Resources.RefAzimuthLine : Properties.Resources.RefPolarCircle;
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
            if (State.CoordinateAxis.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                wpfPlotReference.Plot.Axes.SetLimitsX(-MaxAngle, MaxAngle);
            }
            else
            {
                wpfPlotReference.Plot.Axes.SetLimitsX(0, 360);
            }
        }

        private void UpdateReferencePlot()
        {
            ReferenceCurve? curve = State.CoordinateAxis.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? selectedPolarLine
                : selectedCircleLine;
            UpdateReferenceCurvePlot(curve);
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
                ExportChannel.Contrast => Brushes.DeepSkyBlue,
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

        private double GetStablePolarReferenceRadiusMaximum(ExportChannel channel, IReadOnlyList<PolarPlotPoint> points)
        {
            double curveMaximum = 0;
            for (int index = 0; index < points.Count; index++)
            {
                double radius = points[index].Radius;
                if (double.IsFinite(radius))
                {
                    curveMaximum = Math.Max(curveMaximum, radius);
                }
            }

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

        private static double ConvertCircleAngleToPolarDisplayAngle(double angleDegrees)
        {
            return NormalizePolarPlotAngle(90.0 - angleDegrees);
        }

        private void UpdatePolarReferencePlot(IReadOnlyList<PolarPlotPoint> points, ExportChannel channel, bool closePath)
        {
            if (polarPlotReference == null)
            {
                return;
            }

            double radialMaximum = GetStablePolarReferenceRadiusMaximum(channel, points);
            polarPlotReference.UpdatePlot(
                points,
                GetChannelPlotBrush(channel),
                Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.RadiusFormat, ConoscopeChannelDisplayFormatter.GetAxisLabel(channel)),
                radialMaximum,
                closePath);
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
                ExportChannel.Contrast => ScottPlot.Color.FromColor(System.Drawing.Color.DeepSkyBlue),
                _ => ScottPlot.Color.FromColor(System.Drawing.Color.LimeGreen)
            };
        }

        private void ExtractRgbAlongLine(PolarAngleLine curve, Point start, Point end)
        {
            try
            {
                if (YMat == null)
                {
                    return;
                }

                int imageWidth = YMat.Width;
                int imageHeight = YMat.Height;

                double deltaX = end.X - start.X;
                double deltaY = end.Y - start.Y;
                double lineLength = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                int numSamples = (int)lineLength;

                if (numSamples <= 1)
                {
                    log.Warn($"线长度太短 ({numSamples} 像素)，无法采样");
                    return;
                }

                curve.Samples.EnsureCapacity(numSamples);
                for (int i = 0; i < numSamples; i++)
                {
                    double t = i / (double)(numSamples - 1);
                    double x = start.X + t * deltaX;
                    double y = start.Y + t * deltaY;

                    int ix = Math.Clamp((int)Math.Round(x), 0, imageWidth - 1);
                    int iy = Math.Clamp((int)Math.Round(y), 0, imageHeight - 1);

                    double position = -MaxAngle + t * MaxAngle * 2;

                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);
                    curve.Samples.Add(new RgbSample(position, ix, iy, X, Y, Z));
                }

                log.Info($"完成采样: 方位角{curve.Angle}°, 采样点数{curve.Samples.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取数据失败: {ex.Message}", ex);
            }
        }

        private void ExtractRgbAlongCircle(ConcentricCircleLine curve, Point center, double radiusAngle)
        {
            try
            {
                if (YMat == null)
                {
                    return;
                }

                int imageWidth = YMat.Width;
                int imageHeight = YMat.Height;
                double radiusPixels = radiusAngle * currentPixelsPerDegree;

                const int numSamples = 360;
                curve.Samples.EnsureCapacity(numSamples);
                for (int i = 0; i < numSamples; i++)
                {
                    double anglePos = i * 360.0 / numSamples;
                    double radians = anglePos * Math.PI / 180.0;
                    double x = center.X + radiusPixels * Math.Cos(radians);
                    double y = center.Y - radiusPixels * Math.Sin(radians);

                    int ix = Math.Clamp((int)Math.Round(x), 0, imageWidth - 1);
                    int iy = Math.Clamp((int)Math.Round(y), 0, imageHeight - 1);

                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);
                    curve.Samples.Add(new RgbSample(anglePos, ix, iy, X, Y, Z));
                }

                log.Info($"完成采样: 极角半径角度{curve.RadiusAngle}°, 采样点数{curve.Samples.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取极角数据失败: {ex.Message}", ex);
            }
        }

        private void UpdateReferenceCurvePlot(ReferenceCurve? curve)
        {
            try
            {
                if (curve == null || curve.Samples.Count == 0)
                {
                    wpfPlotReference.Plot.Clear();
                    wpfPlotReference.Refresh();
                    polarPlotReference?.Clear();
                    return;
                }

                ExportChannel channel = GetSelectedDisplayChannel();
                if (referencePlotDisplayMode == ReferencePlotDisplayMode.Polar)
                {
                    PolarPlotPoint[] points = new PolarPlotPoint[curve.Samples.Count];
                    for (int index = 0; index < curve.Samples.Count; index++)
                    {
                        RgbSample sample = curve.Samples[index];
                        double angle = curve.IsClosed
                            ? ConvertCircleAngleToPolarDisplayAngle(sample.Position)
                            : NormalizePolarPlotAngle(sample.Position);
                        points[index] = new PolarPlotPoint(angle, GetChannelValue(sample, channel));
                    }

                    UpdatePolarReferencePlot(points, channel, curve.IsClosed);
                    return;
                }

                wpfPlotReference.Plot.Clear();
                double[] positions = new double[curve.Samples.Count];
                double[] values = new double[curve.Samples.Count];
                for (int index = 0; index < curve.Samples.Count; index++)
                {
                    RgbSample sample = curve.Samples[index];
                    positions[index] = sample.Position;
                    values[index] = GetChannelValue(sample, channel);
                }

                ScottPlot.Plottables.Scatter scatter = wpfPlotReference.Plot.Add.Scatter(positions, values);
                scatter.Color = GetPlotColor(channel);
                scatter.LineWidth = 2;
                scatter.LegendText = ConoscopeChannelDisplayFormatter.GetLabel(channel);

                string channelLabel = ConoscopeChannelDisplayFormatter.GetLabel(channel);
                string title = curve is ConcentricCircleLine circle
                    ? string.Format(Properties.Resources.Conoscope_CircleDistributionTitle, circle.RadiusAngle, channelLabel)
                    : string.Format(Properties.Resources.Conoscope_PolarDistributionTitle, ((PolarAngleLine)curve).Angle, channelLabel);
                wpfPlotReference.Plot.Title(title);
                wpfPlotReference.Plot.XLabel(curve.IsClosed ? Properties.Resources.Conoscope_CircleAngleDegrees : Properties.Resources.Conoscope_AngleDegrees);
                wpfPlotReference.Plot.YLabel(ConoscopeChannelDisplayFormatter.GetAxisLabel(channel));
                wpfPlotReference.Plot.Legend.IsVisible = true;
                wpfPlotReference.Plot.Axes.AutoScale();
                wpfPlotReference.Refresh();

                log.Info($"更新参考曲线: {curve}");
            }
            catch (Exception ex)
            {
                log.Error($"更新参考曲线失败: {ex.Message}", ex);
            }
        }

        private bool isUpdatingFocusCircleToolSelection;
        private bool isFocusCircleModeEnabled;
        private FocusCircleInteractionMode selectedFocusCircleTool = FocusCircleInteractionMode.Select;
        private int lastFocusPoiTemplateId = -1;
        private bool isUpdatingFocusPoiTemplateSelection;

        private void InitializeFocusPointTools()
        {
            SyncReferenceInteractionToggle();
            SetFocusCircleToolSelection(FocusCircleInteractionMode.Select);
            UpdateFocusCircleModeState();
            LoadFocusPoiTemplates();
            UpdateSelectedFocusPointInfo();
        }

        private void LoadFocusPoiTemplates(int preferredTemplateId = -1, bool applySelectedTemplate = true)
        {
            int templateId = preferredTemplateId > 0 ? preferredTemplateId : lastFocusPoiTemplateId;
            SetFocusPoiTemplateControlsEnabled(false);

            if (!FocusPoiTemplateRepository.IsAvailable || cbFocusPoiTemplate == null)
            {
                return;
            }

            ObservableCollection<TemplateModel<PoiParam>> templates = FocusPoiTemplateRepository.Load();

            isUpdatingFocusPoiTemplateSelection = true;
            try
            {
                cbFocusPoiTemplate.ItemsSource = templates;
                int selectedIndex = 0;
                if (templateId > 0)
                {
                    int matchedIndex = templates.Select((item, index) => new { item, index })
                        .FirstOrDefault(item => item.item.Value.Id == templateId)?.index ?? -1;
                    if (matchedIndex >= 0)
                    {
                        selectedIndex = matchedIndex;
                    }
                }

                cbFocusPoiTemplate.SelectedIndex = selectedIndex;
            }
            finally
            {
                isUpdatingFocusPoiTemplateSelection = false;
            }

            SetFocusPoiTemplateControlsEnabled(true);
            if (applySelectedTemplate && cbFocusPoiTemplate.SelectedValue is PoiParam poiParam && poiParam.Id != -1)
            {
                ApplyFocusPoiTemplate(poiParam);
            }
        }

        private void SetFocusPoiTemplateControlsEnabled(bool isEnabled)
        {
            if (cbFocusPoiTemplate != null)
            {
                cbFocusPoiTemplate.IsEnabled = isEnabled;
            }

            if (btnSaveFocusPoiTemplate != null)
            {
                btnSaveFocusPoiTemplate.IsEnabled = isEnabled;
            }

            if (btnManageFocusPoiTemplate != null)
            {
                btnManageFocusPoiTemplate.IsEnabled = isEnabled;
            }
        }

        private void cbFocusPoiTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingFocusPoiTemplateSelection || cbFocusPoiTemplate.SelectedValue is not PoiParam poiParam)
            {
                return;
            }

            if (poiParam.Id == -1)
            {
                lastFocusPoiTemplateId = -1;
                ImageView.ClearFocusCircles();
                UpdateFocusCircleToolbarState();
                return;
            }

            lastFocusPoiTemplateId = poiParam.Id;
            ApplyFocusPoiTemplate(poiParam);
        }

        private void btnSaveFocusPoiTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetFocusPoiTemplateForSave(out PoiParam? poiParam) || poiParam == null)
            {
                return;
            }

            if (ImageView.FocusCircles.Count == 0)
            {
                MessageBox.Show(Properties.Resources.MsgDrawFocusPointsFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!SaveFocusPoiTemplate(poiParam))
            {
                return;
            }

            lastFocusPoiTemplateId = poiParam.Id;
            LoadFocusPoiTemplates(poiParam.Id, applySelectedTemplate: false);
            MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgFocusPoiTemplateSaved, poiParam.Name), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnManageFocusPoiTemplate_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = cbFocusPoiTemplate.SelectedIndex > 0 ? cbFocusPoiTemplate.SelectedIndex - 1 : 0;
            TemplateEditorWindow templateEditorWindow = new(new TemplatePoi(), selectedIndex)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            templateEditorWindow.ShowDialog();
            LoadFocusPoiTemplates(lastFocusPoiTemplateId, applySelectedTemplate: false);
        }

        private bool TryGetFocusPoiTemplateForSave(out PoiParam? poiParam)
        {
            poiParam = null;
            if (!FocusPoiTemplateRepository.IsAvailable)
            {
                MessageBox.Show(Properties.Resources.MsgFocusPoiTemplateSaveRequiresDatabase, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string templateName = string.Format(Properties.Resources.Conoscope_FocusPointTemplateName, DateTime.Now.ToString("yyyyMMddHHmmss"));
            poiParam = FocusPoiTemplateRepository.GetOrCreate(cbFocusPoiTemplate.SelectedValue as PoiParam, templateName);
            if (poiParam != null)
            {
                return true;
            }

            MessageBox.Show(Properties.Resources.MsgFocusPoiTemplateCreateFailed, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private void ApplyFocusPoiTemplate(PoiParam poiParam)
        {
            if (poiParam.Id == -1)
            {
                return;
            }

            lastFocusPoiTemplateId = poiParam.Id;
            FocusPoiTemplateRepository.LoadDetails(poiParam);
            ImageView.ReplaceFocusCirclesFromPoiPoints(poiParam.PoiPoints);
            UpdateFocusCircleToolbarState();
        }

        private bool SaveFocusPoiTemplate(PoiParam poiParam)
        {
            if (ImageView.Source is BitmapSource bmp)
            {
                poiParam.Width = bmp.PixelWidth;
                poiParam.Height = bmp.PixelHeight;
            }
            else if (ImageView.Source != null)
            {
                poiParam.Width = (int)Math.Round(ImageView.Source.Width);
                poiParam.Height = (int)Math.Round(ImageView.Source.Height);
            }
            poiParam.PoiPoints.Clear();
            foreach (DVCircleText circle in ImageView.FocusCircles)
            {
                double radiusX = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
                double radiusY = Math.Max(circle.Attribute.RadiusY, ConoscopeImageHost.MinimumFocusCircleRadius);
                poiParam.PoiPoints.Add(new PoiPoint
                {
                    Id = 0,
                    Name = ResolveFocusCircleName(circle),
                    PointType = PoiShape.Circle,
                    PixX = circle.Attribute.Center.X,
                    PixY = circle.Attribute.Center.Y,
                    PixWidth = Math.Max(1, radiusX * 2),
                    PixHeight = Math.Max(1, radiusY * 2)
                });
            }
            try
            {
                if (!FocusPoiTemplateRepository.Save(poiParam))
                {
                    MessageBox.Show(Properties.Resources.MsgFocusPoiTemplateSaveFailedCheckLog, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.Error("保存 Conoscope 关注点 POI 模板失败", ex);
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgFocusPoiTemplateSaveFailedDetail, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void SyncReferenceInteractionToggle()
        {
            bool isInteractionEnabled = !isFocusCircleModeEnabled;
            if (State.CoordinateAxis.IsInteractionEnabled != isInteractionEnabled)
            {
                State.CoordinateAxis.IsInteractionEnabled = isInteractionEnabled;
            }

            if (!isInteractionEnabled)
            {
                HideCoordinateDragOverlay();
            }
        }

        private void tglFocusCircleMode_Checked(object sender, RoutedEventArgs e)
        {
            isFocusCircleModeEnabled = true;
            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleMode_Unchecked(object sender, RoutedEventArgs e)
        {
            isFocusCircleModeEnabled = false;
            UpdateFocusCircleModeState();
        }

        private void UpdateFocusCircleModeState()
        {
            if (ImageView == null)
            {
                return;
            }

            SyncReferenceInteractionToggle();
            ImageView.InteractionMode = isFocusCircleModeEnabled
                ? selectedFocusCircleTool
                : FocusCircleInteractionMode.Browse;
            UpdateFocusCircleToolbarState();
            UpdatePanModeState();
        }

        private void UpdateFocusCircleToolbarState()
        {
            if (tglFocusCircleDrawTool != null)
            {
                tglFocusCircleDrawTool.IsEnabled = isFocusCircleModeEnabled;
            }

            if (tglFocusCircleSelectTool != null)
            {
                tglFocusCircleSelectTool.IsEnabled = isFocusCircleModeEnabled;
            }

            if (tglFocusCircleEraseTool != null)
            {
                tglFocusCircleEraseTool.IsEnabled = isFocusCircleModeEnabled;
            }

            bool hasFocusCircles = ImageView.FocusCircles.Count > 0;
            if (btnCalculateFocusCircles != null)
            {
                btnCalculateFocusCircles.IsEnabled = hasFocusCircles;
            }

            if (btnClearFocusCircles != null)
            {
                btnClearFocusCircles.IsEnabled = hasFocusCircles;
            }
        }

        private void SetFocusCircleToolSelection(FocusCircleInteractionMode toolKind)
        {
            if (isUpdatingFocusCircleToolSelection)
            {
                return;
            }

            selectedFocusCircleTool = toolKind == FocusCircleInteractionMode.Browse
                ? FocusCircleInteractionMode.Select
                : toolKind;
            isUpdatingFocusCircleToolSelection = true;
            try
            {
                if (tglFocusCircleDrawTool != null)
                {
                    tglFocusCircleDrawTool.IsChecked = selectedFocusCircleTool == FocusCircleInteractionMode.Draw;
                }

                if (tglFocusCircleSelectTool != null)
                {
                    tglFocusCircleSelectTool.IsChecked = selectedFocusCircleTool == FocusCircleInteractionMode.Select;
                }

                if (tglFocusCircleEraseTool != null)
                {
                    tglFocusCircleEraseTool.IsChecked = selectedFocusCircleTool == FocusCircleInteractionMode.Erase;
                }
            }
            finally
            {
                isUpdatingFocusCircleToolSelection = false;
            }
        }

        private void tglFocusCircleDrawTool_Checked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingFocusCircleToolSelection)
            {
                return;
            }

            SetFocusCircleToolSelection(FocusCircleInteractionMode.Draw);
            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleDrawTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingFocusCircleToolSelection)
            {
                return;
            }

            if (selectedFocusCircleTool == FocusCircleInteractionMode.Draw)
            {
                SetFocusCircleToolSelection(FocusCircleInteractionMode.Select);
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleSelectTool_Checked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingFocusCircleToolSelection)
            {
                return;
            }

            SetFocusCircleToolSelection(FocusCircleInteractionMode.Select);
            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleSelectTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingFocusCircleToolSelection)
            {
                return;
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleEraseTool_Checked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingFocusCircleToolSelection)
            {
                return;
            }

            SetFocusCircleToolSelection(FocusCircleInteractionMode.Erase);
            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleEraseTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingFocusCircleToolSelection)
            {
                return;
            }

            if (selectedFocusCircleTool == FocusCircleInteractionMode.Erase)
            {
                SetFocusCircleToolSelection(FocusCircleInteractionMode.Select);
            }

            UpdateFocusCircleModeState();
        }

        private void btnCalculateFocusCircles_Click(object sender, RoutedEventArgs e)
        {
            CalculateFocusPoints(ImageView.FocusCircles);
            UpdateFocusCircleToolbarState();
        }

        private void btnClearFocusCircles_Click(object sender, RoutedEventArgs e)
        {
            ImageView.ClearFocusCircles();
            UpdateFocusCircleToolbarState();
        }

        private void ImageView_FocusCircleCalculationRequested(object? sender, ConoscopeFocusCircleCalculationRequestedEventArgs e)
        {
            CalculateFocusPoints(e.Circles);
        }

        private void ImageView_FocusCircleEditRequested(object? sender, ConoscopeFocusCircleEditRequestedEventArgs e)
        {
            OpenFocusPointPolarEditor(e.Circle);
        }

        private void OpenFocusPointPolarEditor(DVCircleText circle)
        {
            FocusPointPolarEditModel editModel = new(
                circle,
                currentImageCenter,
                currentImageRadius,
                currentPixelsPerDegree,
                MaxAngle);

            PropertyEditorWindow editorWindow = new(editModel)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Title = Properties.Resources.TitleFocusPointPolarEditor
            };
            editorWindow.Submitted += (_, _) =>
            {
                editModel.ApplyTo(circle);
                ImageView.ConstrainFocusCircleToBoundary(circle);
                ImageView.RefreshFocusCircleSelection();
                UpdateSelectedFocusPointInfo();
            };
            editorWindow.ShowDialog();
        }

        private void UpdateSelectedFocusPointInfo()
        {
            if (tbSelectedFocusPointInfo == null || sepSelectedFocusPointInfo == null || ImageView == null)
            {
                return;
            }

            DVCircleText? circle = isFocusCircleModeEnabled ? null : ImageView.SelectedFocusCircle;
            if (circle == null)
            {
                tbSelectedFocusPointInfo.Text = string.Empty;
                tbSelectedFocusPointInfo.ToolTip = Properties.Resources.TipSelectedFocusPoint;
                tbSelectedFocusPointInfo.Visibility = Visibility.Collapsed;
                sepSelectedFocusPointInfo.Visibility = Visibility.Collapsed;
                return;
            }

            string text = BuildSelectedFocusPointInfo(circle, includeMeasurement: true);
            tbSelectedFocusPointInfo.Text = text;
            tbSelectedFocusPointInfo.ToolTip = text;
            tbSelectedFocusPointInfo.Visibility = Visibility.Visible;
            sepSelectedFocusPointInfo.Visibility = Visibility.Visible;
        }

        private string BuildSelectedFocusPointInfo(DVCircleText circle, bool includeMeasurement)
        {
            double radiusPixels = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
            string circleName = ResolveFocusCircleName(circle);
            double azimuthDegrees = FocusPointMeasurementService.GetFullAzimuthAngle(circle.Attribute.Center, currentImageCenter);
            double polarDegrees = FocusPointMeasurementService.GetPolarRadiusAngle(circle.Attribute.Center, currentImageCenter, currentImageRadius, MaxAngle);
            double radiusDegrees = FocusPointMeasurementService.GetFocusCircleRadiusAngle(radiusPixels, currentPixelsPerDegree, currentImageRadius, MaxAngle);
            string info = string.Format(Properties.Resources.Conoscope_FocusPointInfo, circleName, azimuthDegrees, polarDegrees, radiusPixels, radiusDegrees);

            if (includeMeasurement
                && TryCalculateFocusPointAverage(circle.Attribute.Center, radiusPixels, out _, out double avgY, out _, out int sampleCount))
            {
                info += $"  N {sampleCount}  Y {avgY:F3}";
            }

            return info;
        }

        public bool TryGetFocusPointMeasurementCapture(string slotName, out MeasurementCapture capture, out string? errorMessage)
        {
            capture = default!;
            errorMessage = null;

            IReadOnlyList<DVCircleText> focusCircles = ImageView.FocusCircles;
            if (focusCircles.Count == 0)
            {
                errorMessage = Properties.Resources.MsgDrawFocusPointsFirst;
                return false;
            }

            List<MeasurementPoint> points = new(focusCircles.Count);
            foreach (DVCircleText circle in focusCircles)
            {
                if (!TryCreateFocusPointMeasurementPoint(circle, out MeasurementPoint point, out errorMessage))
                {
                    return false;
                }

                points.Add(point);
            }

            capture = new MeasurementCapture(slotName, string.IsNullOrWhiteSpace(FileName) ? "CurrentView" : Path.GetFileName(FileName), points);
            return true;
        }

        private void CalculateFocusPoints(IReadOnlyList<DVCircleText> circles)
        {
            if (!HasXyzData() || XMat == null || YMat == null || ZMat == null || currentBitmapSource == null)
            {
                MessageBox.Show(Properties.Resources.MsgFocusPointNotReady, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (circles.Count == 0)
            {
                MessageBox.Show(Properties.Resources.MsgDrawFocusPointsFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ObservableCollection<PoiResultCIExyuvData> results = new();
            List<string> failedCircles = new();

            foreach (DVCircleText circle in circles.Where(static item => item != null).Distinct())
            {
                if (!TryCreateFocusPointResult(circle, out PoiResultCIExyuvData? result, out int sampleCount, out string? errorMessage))
                {
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        failedCircles.Add(errorMessage);
                    }
                    continue;
                }

                results.Add(result);
                double msgRadiusDegrees = FocusPointMeasurementService.GetFocusCircleRadiusAngle(Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius), currentPixelsPerDegree, currentImageRadius, MaxAngle);
                circle.Attribute.Msg = $"{Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.FocusPointYUV, result.Y.ToString("F3"), result.u.ToString("F4"), result.v.ToString("F4"))}  R:{msgRadiusDegrees:F2}°  N:{sampleCount}";
            }

            if (results.Count == 0)
            {
                string message = failedCircles.Count > 0 ? string.Join(Environment.NewLine, failedCircles) : Properties.Resources.MsgNoFocusPointPixelsCalc;
                MessageBox.Show(message, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WindowCVCIE cieResultWindow = new(results)
            {
                Owner = Window.GetWindow(this)
            };
            cieResultWindow.Show();
            cieResultWindow.Activate();

            if (failedCircles.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, failedCircles), Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            UpdateFocusCircleToolbarState();
            UpdateSelectedFocusPointInfo();
        }

        private bool TryCreateFocusPointResult(DVCircleText circle, out PoiResultCIExyuvData result, out int sampleCount, out string? errorMessage)
        {
            result = new PoiResultCIExyuvData();
            sampleCount = 0;
            if (!TryCreateFocusPointMeasurement(circle, out ImageMeasurement measurement, out sampleCount, out errorMessage))
            {
                return false;
            }

            ConoscopeChromaticity chromaticity = measurement.Chromaticity;
            double dominantWave = ColorimetryHelper.CalculateDominantWavelength(measurement.Chromaticity.x, measurement.Chromaticity.y);
            if (!double.IsFinite(dominantWave) || dominantWave < 0)
            {
                dominantWave = 0;
            }

            PoiPoint poiPoint = new()
            {
                Name = ResolveFocusCircleName(circle),
                PixelX = (int)Math.Round(circle.Attribute.Center.X),
                PixelY = (int)Math.Round(circle.Attribute.Center.Y),
                PointType = PoiShape.Circle,
                Width = (int)Math.Round(circle.Attribute.Radius * 2),
                Height = (int)Math.Round(circle.Attribute.RadiusY * 2)
            };

            result.Point = poiPoint;
            result.X = measurement.X;
            result.Y = measurement.Y;
            result.Z = measurement.Z;
            result.x = measurement.Chromaticity.x;
            result.y = measurement.Chromaticity.y;
            result.u = measurement.Chromaticity.u;
            result.v = measurement.Chromaticity.v;
            result.CCT = chromaticity.Cct;
            result.Wave = dominantWave;
            return true;
        }

        private bool TryCreateFocusPointMeasurement(DVCircleText circle, out ImageMeasurement measurement, out int sampleCount, out string? errorMessage)
        {
            measurement = default!;
            errorMessage = null;
            sampleCount = 0;

            if (XMat == null || YMat == null || ZMat == null || currentBitmapSource == null)
            {
                return false;
            }

            double radius = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
            string label = $"[{Properties.Resources.FocusPointLabel}] {(string.IsNullOrWhiteSpace(FileName) ? "CurrentView" : Path.GetFileName(FileName))} - {ResolveFocusCircleName(circle)}";
            if (!FocusPointMeasurementService.TryCalculateCircleRoiAverage(
                XMat, YMat, ZMat,
                currentBitmapSource.PixelWidth, currentBitmapSource.PixelHeight,
                circle.Attribute.Center, radius,
                out double avgX, out double avgY, out double avgZ, out sampleCount))
            {
                errorMessage = CompositeFormatCache.Format(Properties.Resources.MsgFocusPointNoPixels, label);
                return false;
            }

            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(avgX, avgY, avgZ);
            measurement = new ImageMeasurement(label, avgX, avgY, avgZ, chromaticity);
            return true;
        }

        private bool TryCreateFocusPointMeasurementPoint(DVCircleText circle, out MeasurementPoint point, out string? errorMessage)
        {
            point = default!;
            if (!TryCreateFocusPointMeasurement(circle, out ImageMeasurement measurement, out _, out errorMessage))
            {
                return false;
            }

            string pointName = ResolveFocusCircleName(circle);
            double radiusPixels = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
            double azimuthDegrees = FocusPointMeasurementService.GetFullAzimuthAngle(circle.Attribute.Center, currentImageCenter);
            double polarDegrees = FocusPointMeasurementService.GetPolarRadiusAngle(circle.Attribute.Center, currentImageCenter, currentImageRadius, MaxAngle);
            double radiusDegrees = FocusPointMeasurementService.GetFocusCircleRadiusAngle(radiusPixels, currentPixelsPerDegree, currentImageRadius, MaxAngle);
            point = new MeasurementPoint(pointName, pointName, measurement, azimuthDegrees, polarDegrees, radiusDegrees);
            return true;
        }

        private bool TryCalculateFocusPointAverage(Point imageCenter, double imageRadius, out double avgX, out double avgY, out double avgZ, out int sampleCount)
        {
            avgX = 0;
            avgY = 0;
            avgZ = 0;
            sampleCount = 0;

            if (XMat == null || YMat == null || ZMat == null || currentBitmapSource == null || imageRadius <= 0)
            {
                return false;
            }

            return FocusPointMeasurementService.TryCalculateCircleRoiAverage(
                XMat, YMat, ZMat,
                currentBitmapSource.PixelWidth, currentBitmapSource.PixelHeight,
                imageCenter, imageRadius,
                out avgX, out avgY, out avgZ, out sampleCount);
        }

        private static string ResolveFocusCircleName(DVCircleText circle)
        {
            return FocusPointMeasurementService.ResolveFocusCircleName(circle.Attribute.Text, circle.Attribute.Id);
        }

        public sealed class FocusPointPolarEditModel : ViewModelBase
        {
            private readonly Point imageCenter;
            private readonly double imageRadius;
            private readonly double pixelsPerDegree;
            private readonly double maxAngle;
            private string name;
            private double azimuthDegrees;
            private double polarDegrees;
            private double distancePixels;
            private double radiusPixels;
            private double radiusDegrees;

            public FocusPointPolarEditModel(
                DVCircleText circle,
                Point imageCenter,
                double imageRadius,
                double pixelsPerDegree,
                double maxAngle)
            {
                ArgumentNullException.ThrowIfNull(circle);

                this.imageCenter = imageCenter;
                this.imageRadius = Math.Max(0, imageRadius);
                this.pixelsPerDegree = Math.Max(0, pixelsPerDegree);
                this.maxAngle = Math.Max(0, maxAngle);

                name = ResolveFocusCircleName(circle);
                Point center = circle.Attribute.Center;
                azimuthDegrees = FocusPointMeasurementService.GetFullAzimuthAngle(center, imageCenter);
                polarDegrees = FocusPointMeasurementService.GetPolarRadiusAngle(center, imageCenter, this.imageRadius, this.maxAngle);
                distancePixels = (center - imageCenter).Length;
                radiusPixels = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
                radiusDegrees = FocusPointMeasurementService.GetFocusCircleRadiusAngle(radiusPixels, this.pixelsPerDegree, this.imageRadius, this.maxAngle);
            }

            [Display(Name = "Con_FP_Name", GroupName = "Con_Category_FocusPoint", ResourceType = typeof(Properties.Resources))]
            public string Name
            {
                get => name;
                set
                {
                    string newValue = value ?? string.Empty;
                    if (name == newValue) return;
                    name = newValue;
                    OnPropertyChanged();
                }
            }

            [Display(Name = "Con_FP_Azimuth", GroupName = "Con_Category_Position", ResourceType = typeof(Properties.Resources))]
            public double AzimuthDegrees
            {
                get => azimuthDegrees;
                set
                {
                    double normalized = FocusPointMeasurementService.NormalizeFullAzimuthAngle(value);
                    if (AreClose(azimuthDegrees, normalized)) return;
                    azimuthDegrees = normalized;
                    OnPropertyChanged();
                }
            }

            [Display(Name = "Con_FP_Polar", GroupName = "Con_Category_Position", ResourceType = typeof(Properties.Resources))]
            public double PolarDegrees
            {
                get => polarDegrees;
                set
                {
                    double clamped = Math.Max(0, Math.Min(value, maxAngle));
                    if (AreClose(polarDegrees, clamped)) return;
                    polarDegrees = clamped;
                    distancePixels = PolarDegreesToPixels(clamped, pixelsPerDegree, imageRadius, maxAngle);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DistancePixels));
                }
            }

            [Display(Name = "Con_FP_Distance", GroupName = "Con_Category_Position", ResourceType = typeof(Properties.Resources))]
            public double DistancePixels
            {
                get => distancePixels;
                set
                {
                    double clamped = imageRadius > 0 ? Math.Max(0, Math.Min(value, imageRadius)) : Math.Max(0, value);
                    if (AreClose(distancePixels, clamped)) return;
                    distancePixels = clamped;
                    polarDegrees = PixelsToPolarDegrees(clamped, pixelsPerDegree, imageRadius, maxAngle);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PolarDegrees));
                }
            }

            [Display(Name = "Con_FP_RadiusPx", GroupName = "Con_Category_Size", ResourceType = typeof(Properties.Resources))]
            public double RadiusPixels
            {
                get => radiusPixels;
                set
                {
                    double clamped = Math.Max(value, ConoscopeImageHost.MinimumFocusCircleRadius);
                    if (AreClose(radiusPixels, clamped)) return;
                    radiusPixels = clamped;
                    radiusDegrees = RadiusPixelsToDegrees(clamped, pixelsPerDegree, imageRadius, maxAngle);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RadiusDegrees));
                }
            }

            [Display(Name = "Con_FP_RadiusDeg", GroupName = "Con_Category_Size", ResourceType = typeof(Properties.Resources))]
            public double RadiusDegrees
            {
                get => radiusDegrees;
                set
                {
                    double clamped = Math.Max(0, Math.Min(value, maxAngle));
                    if (AreClose(radiusDegrees, clamped)) return;
                    radiusDegrees = clamped;
                    radiusPixels = RadiusDegreesToPixels(clamped, pixelsPerDegree, imageRadius, maxAngle, ConoscopeImageHost.MinimumFocusCircleRadius);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RadiusPixels));
                }
            }

            public void ApplyTo(DVCircleText circle)
            {
                ArgumentNullException.ThrowIfNull(circle);

                circle.Attribute.Text = name;
                circle.Attribute.Center = FocusPointMeasurementService.CreatePointFromPolar(
                    azimuthDegrees,
                    distancePixels,
                    imageCenter);
                circle.Attribute.Radius = radiusPixels;
                circle.Attribute.RadiusY = radiusPixels;
            }

            private static bool AreClose(double left, double right)
            {
                return Math.Abs(left - right) < 0.000001;
            }

            private static double PolarDegreesToPixels(double angle, double pixelsPerDegree, double imageRadius, double maxAngle)
            {
                double clamped = Math.Max(0, Math.Min(angle, maxAngle));
                if (pixelsPerDegree > double.Epsilon) return clamped * pixelsPerDegree;
                if (imageRadius > 0 && maxAngle > double.Epsilon) return clamped / maxAngle * imageRadius;
                return 0;
            }

            private static double PixelsToPolarDegrees(double distance, double pixelsPerDegree, double imageRadius, double maxAngle)
            {
                distance = Math.Max(0, distance);
                if (pixelsPerDegree > double.Epsilon) return Math.Max(0, Math.Min(distance / pixelsPerDegree, maxAngle));
                if (imageRadius > 0) return Math.Max(0, Math.Min(distance / imageRadius * maxAngle, maxAngle));
                return 0;
            }

            private static double RadiusPixelsToDegrees(double radius, double pixelsPerDegree, double imageRadius, double maxAngle)
            {
                if (pixelsPerDegree > double.Epsilon) return Math.Max(0, radius / pixelsPerDegree);
                if (imageRadius > 0) return Math.Max(0, Math.Min(radius / imageRadius * maxAngle, maxAngle));
                return 0;
            }

            private static double RadiusDegreesToPixels(double angle, double pixelsPerDegree, double imageRadius, double maxAngle, double minimumRadius)
            {
                angle = Math.Max(0, angle);
                if (pixelsPerDegree > double.Epsilon) return Math.Max(minimumRadius, angle * pixelsPerDegree);
                if (imageRadius > 0 && maxAngle > double.Epsilon) return Math.Max(minimumRadius, angle / maxAngle * imageRadius);
                return minimumRadius;
            }
        }

        public void ExportAngleMode()
        {
            try
            {
                if (!TryPrepareSimpleExport(out ExportChannel channel, out string? filePath, "DiameterLine_Export_"))
                {
                    return;
                }

                ConoscopeExportService.ExportAngleModeToCsv(filePath!, channel, CreateExportContext(), ConoscopeManager.Instance.Config.ExportDecimalPlaces);
                OnExportSuccess(filePath!);
            }
            catch (Exception ex)
            {
                log.Error($"方位角模式导出失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgAzimuthExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ExportCircleMode()
        {
            try
            {
                if (!TryPrepareSimpleExport(out ExportChannel channel, out string? filePath, "RCircle_Export_", ConoscopeConfig.CurrentModel.ToString()))
                {
                    return;
                }

                ConoscopeExportService.ExportCircleModeToCsv(filePath!, channel, CreateExportContext(), ConoscopeManager.Instance.Config.ExportDecimalPlaces);
                OnExportSuccess(filePath!);
            }
            catch (Exception ex)
            {
                log.Error($"极角模式导出失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgPolarExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryPrepareSimpleExport(out ExportChannel channel, out string? filePath, string filePrefix, string? suffix = null)
        {
            channel = default;
            filePath = null;

            if (currentBitmapSource == null)
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            channel = GetSelectedCurrentCurveChannel();
            if (!EnsureExportChannelReady(channel))
            {
                return false;
            }

            string suffixPart = string.IsNullOrEmpty(suffix) ? "" : $"{suffix}_";
            string fileName = $"{filePrefix}{channel}_{suffixPart}{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            filePath = TrySelectCsvSavePath(fileName);
            return filePath != null;
        }

        private void OnExportSuccess(string filePath)
        {
            MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgExportSuccess, filePath), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
            log.Info($"导出成功: {filePath}");
        }

        private ExportChannel GetSelectedCurrentCurveChannel() => GetSelectedDisplayChannel();

        private bool EnsureExportChannelReady(ExportChannel channel)
        {
            return EnsureChannelReady(channel, Properties.Resources.TitleHint);
        }

        private bool EnsureChannelReady(ExportChannel channel, string title)
        {
            string? error = GetChannelNotReadyReason(channel);
            if (error == null)
            {
                return true;
            }

            MessageBox.Show(error, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private bool TryValidateExportChannels(List<ExportChannel> channels)
        {
            foreach (ExportChannel channel in channels)
            {
                string? error = GetChannelNotReadyReason(channel);
                if (error != null)
                {
                    MessageBox.Show(error, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private string? GetChannelNotReadyReason(ExportChannel channel)
        {
            ContrastReferenceKind contrastKind = GetRequiredContrastReferenceKind();
            OpenCvSharp.Mat? contrastRefMat = GlobalReferences.GetContrastReferenceYMat(contrastKind);
            bool contrastSizeOk = contrastRefMat == null || YMat == null || (YMat.Width == contrastRefMat.Width && YMat.Height == contrastRefMat.Height);
            OpenCvSharp.Mat? colorDifferenceUMat = GlobalReferences.ColorDifferenceReferenceUMat;
            OpenCvSharp.Mat? colorDifferenceVMat = GlobalReferences.ColorDifferenceReferenceVMat;
            bool cdSizeOk = XMat == null || colorDifferenceUMat == null || colorDifferenceVMat == null
                || (XMat.Width == colorDifferenceUMat.Width && XMat.Height == colorDifferenceUMat.Height
                    && XMat.Width == colorDifferenceVMat.Width && XMat.Height == colorDifferenceVMat.Height);

            string? error = GetExportChannelReadiness(channel,
                hasYMat: YMat != null,
                hasXyzData: HasXyzData(),
                hasContrastReference: contrastRefMat != null,
                contrastReferenceSizeMatches: contrastSizeOk,
                colorDifferenceMode: GetSelectedColorDifferenceReferenceMode(),
                hasColorDifferenceReference: GlobalReferences.HasColorDifferenceReference,
                colorDifferenceReferenceSizeMatches: cdSizeOk,
                hasValidCustomUv: TryParseCustomColorDifferenceReference(out _));

            if (error == null
                && channel == ExportChannel.ColorDifference
                && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.ImageCenter
                && TryCalculateImageCenterColorDifferenceReference() == null)
            {
                return Properties.Resources.MsgNoPixelsInCenter;
            }

            if (channel == ExportChannel.Contrast && error == Properties.Resources.MsgSaveContrastReferenceRequired)
            {
                return CompositeFormatCache.Format(error, GetContrastReferenceKindText(contrastKind));
            }

            if (channel == ExportChannel.Contrast && error == Properties.Resources.MsgContrastReferenceImageSizeMismatch)
            {
                return CompositeFormatCache.Format(error, GetContrastReferenceKindText(contrastKind));
            }

            return error;
        }

        internal static string? GetExportChannelReadiness(
            ExportChannel channel,
            bool hasYMat,
            bool hasXyzData,
            bool hasContrastReference,
            bool contrastReferenceSizeMatches,
            ColorDifferenceReferenceMode colorDifferenceMode,
            bool hasColorDifferenceReference,
            bool colorDifferenceReferenceSizeMatches,
            bool hasValidCustomUv)
        {
            if (channel == ExportChannel.Y)
            {
                return hasYMat ? null : Properties.Resources.MsgLoadImageFirst;
            }

            if (channel == ExportChannel.Contrast)
            {
                if (!hasYMat)
                {
                    return Properties.Resources.MsgLoadImageFirst;
                }

                if (!hasContrastReference)
                {
                    return Properties.Resources.MsgSaveContrastReferenceRequired;
                }

                if (!contrastReferenceSizeMatches)
                {
                    return Properties.Resources.MsgContrastReferenceImageSizeMismatch;
                }

                return null;
            }

            if (!hasXyzData)
            {
                return Properties.Resources.XYZDataNotLoaded;
            }

            if (channel == ExportChannel.ColorDifference)
            {
                if (colorDifferenceMode == ColorDifferenceReferenceMode.ReferenceImage && !hasColorDifferenceReference)
                {
                    return Properties.Resources.MsgGlobalColorDifferenceReferenceRequired;
                }

                if (colorDifferenceMode == ColorDifferenceReferenceMode.ReferenceImage && !colorDifferenceReferenceSizeMatches)
                {
                    return Properties.Resources.MsgImageSizeMismatch;
                }

                if (colorDifferenceMode == ColorDifferenceReferenceMode.Custom && !hasValidCustomUv)
                {
                    return Properties.Resources.MsgInvalidCustomUvReference;
                }
            }

            return null;
        }

        private string? TrySelectCsvSavePath(string defaultFileName)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = Properties.Resources.LabelSaveFilterCsv,
                DefaultExt = "csv",
                FileName = defaultFileName,
                RestoreDirectory = true
            };

            return saveFileDialog.ShowDialog() == true ? saveFileDialog.FileName : null;
        }

        private ConoscopeExportContext CreateExportContext()
        {
            if (YMat == null)
            {
                throw new InvalidOperationException(Properties.Resources.XYZDataNotLoaded);
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
                },
                ReadContrast = (ix, iy) =>
                {
                    ExtractXYZValues(ix, iy, out _, out double Y, out _);
                    return GetContrastValue(ix, iy, Y);
                }
            };
        }

        public void AdvancedExport()
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ConoscopeConfig config = ConoscopeManager.Instance.Config;
                AdvancedExportDialog dialog = new AdvancedExportDialog(config.AdvancedExport) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() == true)
                {
                    AdvancedExportSettings settings = dialog.Settings;
                    SaveAdvancedExportSettings(settings);

                    if (!TryValidateExportChannels(settings.Channels))
                    {
                        return;
                    }

                    PerformAdvancedExport(settings);
                }
            }
            catch (Exception ex)
            {
                log.Error($"高级导出失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgAdvancedExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void SaveAdvancedExportSettings(AdvancedExportSettings settings)
        {
            ConoscopeConfig config = ConoscopeManager.Instance.Config;
            config.AdvancedExport = settings;

            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
            }
            catch (Exception ex)
            {
                log.Warn($"保存高级导出配置失败: {ex.Message}");
            }
        }

        private void PerformAdvancedExport(AdvancedExportSettings settings)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                int filesExported = 0;

                using System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = Properties.Resources.MsgSelectExportFolder;
                folderDialog.ShowNewFolderButton = true;

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                string outputFolder = folderDialog.SelectedPath;
                ConoscopeExportContext exportContext = CreateExportContext();

                if (settings.ExportAzimuth)
                {
                    foreach (ExportChannel channel in settings.Channels)
                    {
                        string filename = $"{settings.FilePrefix}_Azimuth_{channel}_{timestamp}.csv";
                        string filePath = Path.Combine(outputFolder, filename);
                        ConoscopeExportService.ExportAzimuthWithStep(filePath, channel, exportContext, settings.AzimuthStep, settings.RadialStep, settings.DecimalPlaces);
                        filesExported++;
                        log.Info($"方位角导出成功: {filePath}");
                    }
                }

                if (settings.ExportPolar)
                {
                    foreach (ExportChannel channel in settings.Channels)
                    {
                        string filename = $"{settings.FilePrefix}_Polar_{channel}_{ConoscopeConfig.CurrentModel}_{timestamp}.csv";
                        string filePath = Path.Combine(outputFolder, filename);
                        ConoscopeExportService.ExportPolarWithStep(filePath, channel, exportContext, settings.PolarStep, settings.CircumferentialStep, settings.DecimalPlaces);
                        filesExported++;
                        log.Info($"极角导出成功: {filePath}");
                    }
                }

                if (settings.EnableCrossSection)
                {
                    ConoscopeCrossSectionExportOptions exportOptions = new ConoscopeCrossSectionExportOptions
                    {
                        StepDegrees = settings.CrossSectionType == CrossSectionType.Azimuth
                            ? settings.RadialStep
                            : settings.CircumferentialStep,
                        IncludeMetadata = true,
                        DecimalPlaces = settings.DecimalPlaces
                    };
                    string sectionType = settings.CrossSectionType == CrossSectionType.Azimuth ? "Azimuth" : "Polar";
                    foreach (ExportChannel channel in settings.Channels)
                    {
                        string filename = $"{settings.FilePrefix}_CrossSection_{sectionType}_{settings.CrossSectionAngle}deg_{channel}_{timestamp}.csv";
                        string filePath = Path.Combine(outputFolder, filename);

                        if (settings.CrossSectionType == CrossSectionType.Azimuth)
                        {
                            ConoscopeExportService.ExportAzimuthCrossSection(filePath, channel, exportContext, settings.CrossSectionAngle, exportOptions);
                        }
                        else
                        {
                            ConoscopeExportService.ExportPolarCrossSection(filePath, channel, exportContext, settings.CrossSectionAngle, exportOptions);
                        }

                        filesExported++;
                        log.Info($"截面导出成功: {filePath}");
                    }
                }

                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgExportDone, filesExported, outputFolder), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"高级导出完成: {filesExported} 个文件");
            }
            catch (Exception ex)
            {
                log.Error($"高级导出执行失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportCurrentReference_Click(object sender, RoutedEventArgs e)
        {
            if (State.CoordinateAxis.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                btnExportCurrentAzimuth_Click(sender, e);
            }
            else
            {
                btnExportCurrentPolar_Click(sender, e);
            }
        }

        private ConoscopeCrossSectionExportOptions? ShowCurrentCurveExportDialog()
        {
            ConoscopeConfig exportConfig = ConoscopeManager.Instance.Config;
            ConoscopeCrossSectionExportOptions currentOptions = new ConoscopeCrossSectionExportOptions
            {
                StepDegrees = exportConfig.CurrentCurveExportStepDegrees,
                IncludeMetadata = exportConfig.CurrentCurveExportIncludeMetadata,
                DecimalPlaces = exportConfig.ExportDecimalPlaces
            };

            CurrentCurveExportDialog dialog = new CurrentCurveExportDialog(currentOptions)
            {
                Owner = Window.GetWindow(this)
            };

            return dialog.ShowDialog() == true ? dialog.ExportOptions : null;
        }

        private void btnExportCurrentAzimuth_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPolarLine == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectAzimuthFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TryExportCurrentCrossSection(
                "Azimuth",
                selectedPolarLine.Angle,
                Properties.Resources.MsgAzimuthExportSuccess,
                (filePath, channel, context, angle, options) =>
                    ConoscopeExportService.ExportAzimuthCrossSection(filePath, channel, context, angle, options));
        }

        private void btnExportCurrentPolar_Click(object sender, RoutedEventArgs e)
        {
            if (selectedCircleLine == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectPolarFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TryExportCurrentCrossSection(
                "Polar",
                selectedCircleLine.RadiusAngle,
                Properties.Resources.MsgPolarExportSuccess,
                (filePath, channel, context, angle, options) =>
                    ConoscopeExportService.ExportPolarCrossSection(filePath, channel, context, angle, options));
        }

        private void TryExportCurrentCrossSection(
            string sectionLabel,
            double angle,
            string successMessageResource,
            Action<string, ExportChannel, ConoscopeExportContext, double, ConoscopeCrossSectionExportOptions> exportAction)
        {
            try
            {
                if (YMat == null)
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedCurrentCurveChannel();
                if (!EnsureExportChannelReady(channel))
                {
                    return;
                }

                ConoscopeCrossSectionExportOptions? exportOptions = ShowCurrentCurveExportDialog();
                if (exportOptions == null)
                {
                    return;
                }

                string? filePath = TrySelectCsvSavePath($"{sectionLabel}_{angle}deg_{channel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                if (filePath == null)
                {
                    return;
                }

                exportAction(filePath, channel, CreateExportContext(), angle, exportOptions);
                MessageBox.Show(CompositeFormatCache.Format(successMessageResource, angle), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);

                ConoscopeConfig exportConfig = ConoscopeManager.Instance.Config;
                exportConfig.CurrentCurveExportStepDegrees = exportOptions.StepDegrees;
                exportConfig.CurrentCurveExportIncludeMetadata = exportOptions.IncludeMetadata;
                exportConfig.ExportDecimalPlaces = exportOptions.DecimalPlaces;
                try
                {
                    ConfigService.Instance.Save<ConoscopeConfig>();
                }
                catch (Exception ex)
                {
                    log.Warn($"保存当前曲线导出选项失败: {ex.Message}");
                }

                log.Info($"{sectionLabel}截面导出成功: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"{sectionLabel}截面导出失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal bool HasActiveViewState => State.HasDisplayData;
        internal bool CanUseDerivedChannels => State.CanUseDerivedChannels;
        internal bool CanUseContrastChannel => State.CanUseContrastChannel;

        internal void SetDisplayChannel(ExportChannel channel)
        {
            ExportChannel previousChannel = State.DisplayChannel;
            if (previousChannel == channel)
            {
                return;
            }

            if (!EnsureChannelReady(channel, Properties.Resources.TitleHint))
            {
                State.RefreshDisplayChannelBinding();
                return;
            }

            State.DisplayChannel = channel;
            try
            {
                RefreshDisplayedImage();
                UpdateReferencePlot();
            }
            catch (Exception ex)
            {
                State.DisplayChannel = previousChannel;
                RestoreDisplayAfterRejectedStateChange();
                log.Error($"刷新显示通道失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RestoreDisplayAfterRejectedStateChange()
        {
            try
            {
                EnsureSelectedDisplayChannelAvailable();
                RefreshDisplayedImage();
                UpdateReferencePlot();
            }
            catch (Exception ex)
            {
                log.Warn($"回滚显示状态后重新渲染失败: {ex.Message}", ex);
            }
        }

        internal void SetReferenceMode(ConoscopeCoordinateReferenceMode mode)
        {
            ConoscopeCoordinateAxisParam axisParam = State.CoordinateAxis;
            if (axisParam.ReferenceMode == mode)
            {
                return;
            }

            axisParam.ReferenceMode = mode;
            if (coordinateAxisController == null)
            {
                NotifyReferenceStateChanged();
                ApplyCoordinateAxisReference();
            }
        }

        internal void SetContrastImageKind(ContrastReferenceKind kind)
        {
            ApplyContrastImageKind(kind, refreshDisplay: true);
        }

        internal void SaveColorDifferenceReference()
        {
            SaveCurrentAsGlobalColorDifferenceReference();
        }

        internal void SetReferenceValue(double value)
        {
            ConoscopeCoordinateAxisParam axisParam = State.CoordinateAxis;
            if (axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                axisParam.ReferenceAngle = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(value);
            }
            else
            {
                axisParam.ReferenceRadiusAngle = Math.Max(0, Math.Min(value, MaxAngle));
            }

            if (coordinateAxisController == null)
            {
                NotifyReferenceStateChanged();
                ApplyCoordinateAxisReference();
            }
        }

    }
}
