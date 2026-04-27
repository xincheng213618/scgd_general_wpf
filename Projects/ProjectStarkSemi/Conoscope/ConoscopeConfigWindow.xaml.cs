using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ProjectStarkSemi.Conoscope
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
        }
    }
}