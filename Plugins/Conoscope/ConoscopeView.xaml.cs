using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.FullScreen;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using Conoscope.Core;
using Conoscope.Presentation.Helpers;
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
    public partial class ConoscopeView : UserControl, IDisposable, IActiveDocumentStatusProvider
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
        private ReferencePlotDisplayMode referencePlotDisplayMode;
        private WindowCIE? cieWindow;
        private ImageFullScreenMode? imageFullScreenMode;
        private ConoscopeModelProfile? subscribedModelProfile;
        private const float MinPositiveXyzValue = 0.000001f;
        private const double Conoscope3DInitialHeightScale = 160.0;
        private ConoscopeImageZoomMode imageZoomMode = ConoscopeImageZoomMode.Fit;
        private bool applyCircleFitOnNextRefresh;
        private bool isApplyingImageZoomMode;

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
            if (XMat != null)
            {
                items.Add(new StatusBarMeta
                {
                    Id = "ConoscopeImageDimensions",
                    Name = "Image Size",
                    Description = $"{XMat.Cols} x {XMat.Rows}",
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
                    Name = "File Type",
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
                    Name = "Exposure",
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
            ImageView.FocusCircleCalculationRequested += ImageView_FocusCircleCalculationRequested;
            ImageView.FocusCirclesChanged += ImageView_FocusCirclesChanged;
            ConoscopeModuleService.Register(this);
        }

        private void ImageView_FocusCirclesChanged(object? sender, EventArgs e)
        {
            UpdateFocusCircleToolbarState();
        }

        public ConoscopeConfig ConoscopeConfig => ConoscopeManager.GetInstance().Config;
        private ConoscopeRenderingSettings RenderingConfig => ConoscopeConfig.Rendering;
        private ConoscopePreprocessSettings PreprocessConfig => ConoscopeConfig.Preprocess;
        private ConoscopeColorDifferenceSettings ColorDifferenceConfig => ConoscopeConfig.ColorDifference;

        private void Window_Initialized(object sender, EventArgs e)
        {
            RefreshReferenceLineProfileBinding();

            this.DataContext = ConoscopeManager.GetInstance();
            RefreshDisplayControlsFromConfig();
            RefreshQuickControlsFromAxisParam();
            InitializeColorDifferenceControls();
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

            imageFullScreenMode = new ImageFullScreenMode(ImageViewHost);
            ImageView.Zoombox1.ContentMatrixChanged -= Zoombox1_ContentMatrixChanged;
            ImageView.Zoombox1.ContentMatrixChanged += Zoombox1_ContentMatrixChanged;
            UpdatePseudoColorMapPreview();
            UpdateToolbarZoomRatio();
            InitializeFocusPointTools();
            UpdatePanModeState();
        }

        private void Zoombox1_ContentMatrixChanged(object? sender, EventArgs e)
        {
            if (!isApplyingImageZoomMode)
            {
                imageZoomMode = ConoscopeImageZoomMode.Custom;
            }

            UpdateToolbarZoomRatio();
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
            ImageView.FocusCirclesChanged -= ImageView_FocusCirclesChanged;
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
    }
}
