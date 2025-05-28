using ColorVision.Engine.Templates.Flow;
using System.Windows;
using ColorVision.Solution.Editor;


namespace ColorVision.Engine.Templates.Flow
{
    // 声明支持的扩展名，并设为默认
    [EditorForExtension(".stn|.cvflow", "流程编辑器", isDefault: true)]
    public class FlowEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            FlowEngineToolWindow flowEngineToolWindow = new FlowEngineToolWindow
            {
                Owner = System.Windows.Application.Current.GetActiveWindow()
            };
            flowEngineToolWindow.OpenFlow(filePath);
            flowEngineToolWindow.Show();
        }
    }

}
