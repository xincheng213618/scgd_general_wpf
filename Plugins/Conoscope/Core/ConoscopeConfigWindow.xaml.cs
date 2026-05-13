using ColorVision.UI;
using ColorVision.Core;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope.Core
{
    public partial class ConoscopeConfigWindow : Window
    {
        private readonly ConoscopeConfig config;
        private readonly ConoscopeGlobalSettings globalSettings;

        public ConoscopeConfigWindow(ConoscopeConfig config)
        {
            InitializeComponent();
            this.config = config;
            globalSettings = new ConoscopeGlobalSettings(config);

            cbCurrentModel.ItemsSource = Enum.GetValues<ConoscopeModelType>();
            cbCurrentModel.SelectedItem = config.CurrentModel;
            GlobalSettingsHost.Content = PropertyEditorHelper.GenPropertyEditorControl(globalSettings);
            RefreshModelProfileEditor();
        }

        private void cbCurrentModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbCurrentModel.SelectedItem is ConoscopeModelType modelType)
            {
                config.CurrentModel = modelType;
                RefreshModelProfileEditor();
            }
        }

        private void RefreshModelProfileEditor()
        {
            ModelProfileHost.Content = PropertyEditorHelper.GenPropertyEditorControl(config.CurrentModelProfile);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private sealed class ConoscopeGlobalSettings
        {
            private readonly ConoscopeRenderingSettings rendering;
            private readonly ConoscopePreprocessSettings preprocess;
            private readonly ConoscopeExportSettings export;

            public ConoscopeGlobalSettings(ConoscopeConfig config)
            {
                rendering = config.Rendering;
                preprocess = config.Preprocess;
                export = config.Export;
            }

            [Category("显示"), DisplayName("显示通道")]
            public ExportChannel DisplayChannel
            {
                get => rendering.DisplayChannel;
                set => rendering.DisplayChannel = value;
            }

            [Category("显示"), DisplayName("伪彩色映射")]
            public ColormapTypes PseudoColorMap
            {
                get => rendering.PseudoColorMap;
                set => rendering.PseudoColorMap = value;
            }

            [Category("滤波"), DisplayName("打开时应用滤波")]
            public bool ApplyFilterOnOpen
            {
                get => preprocess.ApplyFilterOnOpen;
                set => preprocess.ApplyFilterOnOpen = value;
            }

            [Category("滤波"), DisplayName("滤波类型")]
            public ImageFilterType FilterType
            {
                get => preprocess.FilterType;
                set => preprocess.FilterType = value;
            }

            [Category("滤波"), DisplayName("核大小")]
            public int FilterKernelSize
            {
                get => preprocess.FilterKernelSize;
                set => preprocess.FilterKernelSize = value;
            }

            [Category("滤波"), DisplayName("高斯 Sigma")]
            public double FilterSigma
            {
                get => preprocess.FilterSigma;
                set => preprocess.FilterSigma = value;
            }

            [Category("滤波"), DisplayName("双边 d")]
            public int FilterD
            {
                get => preprocess.FilterD;
                set => preprocess.FilterD = value;
            }

            [Category("滤波"), DisplayName("双边 SigmaColor")]
            public double FilterSigmaColor
            {
                get => preprocess.FilterSigmaColor;
                set => preprocess.FilterSigmaColor = value;
            }

            [Category("滤波"), DisplayName("双边 SigmaSpace")]
            public double FilterSigmaSpace
            {
                get => preprocess.FilterSigmaSpace;
                set => preprocess.FilterSigmaSpace = value;
            }

            [Category("灰尘滤除"), DisplayName("启用灰尘滤除")]
            public bool DustRemovalEnabled
            {
                get => preprocess.DustRemovalEnabled;
                set => preprocess.DustRemovalEnabled = value;
            }

            [Category("灰尘滤除"), DisplayName("灰尘类型")]
            public DustRemovalMode DustRemovalMode
            {
                get => preprocess.DustRemovalMode;
                set => preprocess.DustRemovalMode = value;
            }

            [Category("灰尘滤除"), DisplayName("检测阈值(%)")]
            public double DustThresholdPercent
            {
                get => preprocess.DustThresholdPercent;
                set => preprocess.DustThresholdPercent = value;
            }

            [Category("灰尘滤除"), DisplayName("最小面积(px)")]
            public int DustMinArea
            {
                get => preprocess.DustMinArea;
                set => preprocess.DustMinArea = value;
            }

            [Category("灰尘滤除"), DisplayName("最大面积(px)")]
            public int DustMaxArea
            {
                get => preprocess.DustMaxArea;
                set => preprocess.DustMaxArea = value;
            }

            [Category("灰尘滤除"), DisplayName("修复半径(px)")]
            public int DustRepairRadius
            {
                get => preprocess.DustRepairRadius;
                set => preprocess.DustRepairRadius = value;
            }

            [Category("导出"), DisplayName("当前曲线采样间隔(度)"), Description("当前曲线 CSV 导出的默认采样间隔。")]
            public double CurrentCurveExportStepDegrees
            {
                get => export.CurrentCurveStepDegrees;
                set => export.CurrentCurveStepDegrees = value;
            }

            [Category("导出"), DisplayName("当前曲线导出元数据"), Description("是否在当前曲线 CSV 顶部写入标题和元数据。")]
            public bool CurrentCurveExportIncludeMetadata
            {
                get => export.IncludeMetadata;
                set => export.IncludeMetadata = value;
            }
        }
    }
}
