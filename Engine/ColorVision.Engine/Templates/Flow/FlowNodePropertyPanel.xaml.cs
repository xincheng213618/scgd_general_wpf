using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow
{
    public partial class FlowNodePropertyPanel : UserControl
    {
        public static FlowNodePropertyPanel Instance { get; private set; }

        public const string PanelId = "FlowNodePropertyPanel";

        public FlowNodePropertyPanel()
        {
            Instance = this;
            InitializeComponent();
        }

        public StackPanel SignStackPanel => SignStackPanelContainer;
        internal STNodeEditorHelper? EditorHelper { get; set; }

        private void ShowDebugPropertiesToggle_Changed(object sender, RoutedEventArgs e)
        {
            FlowNodePropertyMetadataProvider.ShowDebugProperties = ShowDebugPropertiesToggle.IsChecked == true;
            if (EditorHelper != null)
            {
                EditorHelper.RefreshActiveNodePropertyPanel();
            }
        }
    }
}
