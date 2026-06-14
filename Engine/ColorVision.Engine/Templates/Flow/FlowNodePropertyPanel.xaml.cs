using ST.Library.UI.NodeEditor;
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

        private bool _isPropertyGridMode;

        private void TogglePropertyEditorMode_Click(object sender, RoutedEventArgs e)
        {
            _isPropertyGridMode = !_isPropertyGridMode;
            PropertyGridHost.Visibility = _isPropertyGridMode ? Visibility.Visible : Visibility.Collapsed;
            SignStackScrollViewer.Visibility = _isPropertyGridMode ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
