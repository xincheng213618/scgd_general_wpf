using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Impl.FileProcessor
{
    public class FlowEditor : IEditorBase
    {
        public override string Extension => ".stn|.cvflow";
        public override Control? Open(string FilePath)
        {
            FlowEngineToolWindow flowEngineToolWindow = new FlowEngineToolWindow() { Owner = Application.Current.GetActiveWindow() };
            flowEngineToolWindow.OpenFlow(FilePath);
            flowEngineToolWindow.Show();
            return null;
        }
    }

}
