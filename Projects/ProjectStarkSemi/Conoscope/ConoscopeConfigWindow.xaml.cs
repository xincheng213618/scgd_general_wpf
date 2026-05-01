using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ProjectStarkSemi.Core
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
            private readonly ConoscopeConfig config;

            public ConoscopeGlobalSettings(ConoscopeConfig config)
            {
                this.config = config;
            }

            [Category("显示"), DisplayName("显示通道")]
            public ExportChannel DisplayChannel
            {
                get => config.DisplayChannel;
                set => config.DisplayChannel = value;
            }

            [Category("滤波"), DisplayName("打开时应用滤波")]
            public bool ApplyFilterOnOpen
            {
                get => config.ApplyFilterOnOpen;
                set => config.ApplyFilterOnOpen = value;
            }

            [Category("滤波"), DisplayName("滤波类型")]
            public ImageFilterType FilterType
            {
                get => config.FilterType;
                set => config.FilterType = value;
            }

            [Category("滤波"), DisplayName("核大小")]
            public int FilterKernelSize
            {
                get => config.FilterKernelSize;
                set => config.FilterKernelSize = value;
            }

            [Category("滤波"), DisplayName("高斯 Sigma")]
            public double FilterSigma
            {
                get => config.FilterSigma;
                set => config.FilterSigma = value;
            }

            [Category("滤波"), DisplayName("双边 d")]
            public int FilterD
            {
                get => config.FilterD;
                set => config.FilterD = value;
            }

            [Category("滤波"), DisplayName("双边 SigmaColor")]
            public double FilterSigmaColor
            {
                get => config.FilterSigmaColor;
                set => config.FilterSigmaColor = value;
            }

            [Category("滤波"), DisplayName("双边 SigmaSpace")]
            public double FilterSigmaSpace
            {
                get => config.FilterSigmaSpace;
                set => config.FilterSigmaSpace = value;
            }

            [Category("灰尘滤除"), DisplayName("启用灰尘滤除")]
            public bool DustRemovalEnabled
            {
                get => config.DustRemovalEnabled;
                set => config.DustRemovalEnabled = value;
            }

            [Category("灰尘滤除"), DisplayName("灰尘类型")]
            public DustRemovalMode DustRemovalMode
            {
                get => config.DustRemovalMode;
                set => config.DustRemovalMode = value;
            }

            [Category("灰尘滤除"), DisplayName("检测阈值(%)")]
            public double DustThresholdPercent
            {
                get => config.DustThresholdPercent;
                set => config.DustThresholdPercent = value;
            }

            [Category("灰尘滤除"), DisplayName("最小面积(px)")]
            public int DustMinArea
            {
                get => config.DustMinArea;
                set => config.DustMinArea = value;
            }

            [Category("灰尘滤除"), DisplayName("最大面积(px)")]
            public int DustMaxArea
            {
                get => config.DustMaxArea;
                set => config.DustMaxArea = value;
            }

            [Category("灰尘滤除"), DisplayName("修复半径(px)")]
            public int DustRepairRadius
            {
                get => config.DustRepairRadius;
                set => config.DustRepairRadius = value;
            }
        }
    }
}