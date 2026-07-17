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
    }
}
