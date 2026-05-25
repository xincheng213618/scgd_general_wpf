using ColorVision.UI;
using ColorVision.Core;
using Conoscope;
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
            PreprocessSettingsHost.Content = new ConoscopePreprocessSettingsControl(config, persistChanges: false);
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
            private readonly ConoscopeExportSettings export;

            public ConoscopeGlobalSettings(ConoscopeConfig config)
            {
                rendering = config.Rendering;
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

            [Category("显示"), DisplayName("使用伪彩色")]
            public bool UsePseudoColor
            {
                get => rendering.UsePseudoColor;
                set => rendering.UsePseudoColor = value;
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

            [Category("导出"), DisplayName("导出数据小数位数"), Description("CSV 导出时数据值默认保留的小数位数。")]
            public int ExportDecimalPlaces
            {
                get => export.DecimalPlaces;
                set => export.DecimalPlaces = value;
            }
        }
    }
}
