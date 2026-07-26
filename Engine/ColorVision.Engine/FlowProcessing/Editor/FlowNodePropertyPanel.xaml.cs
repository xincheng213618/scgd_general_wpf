using System.Windows.Controls;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    public partial class FlowNodePropertyPanel : UserControl
    {
        public FlowNodePropertyPanel()
        {
            InitializeComponent();
        }

        public StackPanel SignStackPanel => SignStackPanelContainer;
    }
}
