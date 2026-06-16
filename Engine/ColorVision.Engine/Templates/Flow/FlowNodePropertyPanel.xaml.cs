using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System.Linq;
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

        public STNodePropertyGrid NodePropertyGrid => STNodePropertyGrid1;
        public StackPanel SignStackPanel => SignStackPanelContainer;
        internal STNodeEditorHelper? EditorHelper { get; set; }

        private bool _isPropertyGridMode;

        private void TogglePropertyEditorMode_Click(object sender, RoutedEventArgs e)
        {
            _isPropertyGridMode = !_isPropertyGridMode;
            PropertyGridHost.Visibility = _isPropertyGridMode ? Visibility.Visible : Visibility.Collapsed;
            SignStackScrollViewer.Visibility = _isPropertyGridMode ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowDebugPropertiesToggle_Changed(object sender, RoutedEventArgs e)
        {
            FlowNodePropertyMetadataProvider.ShowDebugProperties = ShowDebugPropertiesToggle.IsChecked == true;
            if (EditorHelper != null)
            {
                EditorHelper.RefreshActiveNodePropertyPanel();
                return;
            }

            RefreshDynamicPropertyEditor();
        }

        private void RefreshDynamicPropertyEditor()
        {
            if (NodePropertyGrid.STNode == null)
            {
                return;
            }

            var propertyStackPanel = SignStackPanelContainer.Children.OfType<StackPanel>().LastOrDefault();
            if (propertyStackPanel == null)
            {
                return;
            }

            propertyStackPanel.Children.Clear();
            var resourceManager = PropertyEditorHelper.GetResourceManager(NodePropertyGrid.STNode);
            propertyStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(
                NodePropertyGrid.STNode,
                resourceManager,
                metadataProvider: FlowNodePropertyMetadataProvider.Instance));
        }
    }
}
