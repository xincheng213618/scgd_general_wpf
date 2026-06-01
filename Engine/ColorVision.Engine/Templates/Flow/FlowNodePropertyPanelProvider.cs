using ColorVision.Solution.Workspace;
using ColorVision.UI;

namespace ColorVision.Engine.Templates.Flow
{
    public class FlowNodePropertyPanelProvider : IDockPanelProvider
    {
        public int Order => 200;

        public void RegisterPanels()
        {
            var layoutManager = WorkspaceManager.LayoutManager;
            if (layoutManager == null) return;

            var panel = new FlowNodePropertyPanel();
            layoutManager.RegisterPanel(FlowNodePropertyPanel.PanelId, panel, Properties.Resources.NodeProperty, PanelPosition.Right);
        }
    }
}
