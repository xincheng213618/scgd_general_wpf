using ColorVision.UI;
using ColorVision.Core;
using Conoscope;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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

            [Display(Name = "Con_Cfg_DisplayChannel", GroupName = "Con_Category_Display", ResourceType = typeof(Properties.Resources))]
            public ExportChannel DisplayChannel
            {
                get => rendering.DisplayChannel;
                set => rendering.DisplayChannel = value;
            }

            [Display(Name = "Con_Cfg_PseudoColor", GroupName = "Con_Category_Display", ResourceType = typeof(Properties.Resources))]
            public ColormapTypes PseudoColorMap
            {
                get => rendering.PseudoColorMap;
                set => rendering.PseudoColorMap = value;
            }

            [Display(Name = "Con_Cfg_UsePseudoColor", GroupName = "Con_Category_Display", ResourceType = typeof(Properties.Resources))]
            public bool UsePseudoColor
            {
                get => rendering.UsePseudoColor;
                set => rendering.UsePseudoColor = value;
            }

            [Display(Name = "Con_Cfg_SampleInterval", GroupName = "Con_Category_Export", Description = "当前曲线 CSV 导出的默认采样间隔。", ResourceType = typeof(Properties.Resources))]
            public double CurrentCurveExportStepDegrees
            {
                get => export.CurrentCurveStepDegrees;
                set => export.CurrentCurveStepDegrees = value;
            }

            [Display(Name = "Con_Cfg_ExportMetadata", GroupName = "Con_Category_Export", Description = "是否在当前曲线 CSV 顶部写入标题和元数据。", ResourceType = typeof(Properties.Resources))]
            public bool CurrentCurveExportIncludeMetadata
            {
                get => export.IncludeMetadata;
                set => export.IncludeMetadata = value;
            }

            [Display(Name = "Con_Cfg_Decimals", GroupName = "Con_Category_Export", Description = "CSV 导出时数据值默认保留的小数位数。", ResourceType = typeof(Properties.Resources))]
            public int ExportDecimalPlaces
            {
                get => export.DecimalPlaces;
                set => export.DecimalPlaces = value;
            }
        }
    }
}
