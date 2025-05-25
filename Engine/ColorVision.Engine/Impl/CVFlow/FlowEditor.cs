using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Impl.FileProcessor
{
    using System.Windows.Controls;

    // 声明支持的扩展名，并设为默认
    [EditorForExtension(".stn|.cvflow", isDefault: true)]
    public class FlowEditor : EditorBase
    {
        public override string Name => "流程编辑器";

        public override Control? Open(string filePath)
        {
            FlowEngineToolWindow flowEngineToolWindow = new FlowEngineToolWindow
            {
                Owner = System.Windows.Application.Current.GetActiveWindow()
            };
            flowEngineToolWindow.OpenFlow(filePath);
            flowEngineToolWindow.Show();
            return null;
        }
    }

}
