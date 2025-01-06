using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using System;


namespace ColorVision.Engine.Impl.FileProcessor
{

    public class FileProcessoCVFlow : IFileProcessor
    {
        public string GetExtension() => "流程文件 (;*.cvflow)|*.cvflow"; // "cvflow
        public int Order => 2;

        public bool CanProcess(string filePath)
        {
            return filePath.EndsWith("cvflow", StringComparison.OrdinalIgnoreCase)|| filePath.EndsWith("stn", StringComparison.OrdinalIgnoreCase);
        }

        public void Process(string filePath)
        {
            FlowEngineToolWindow flowEngineToolWindow = new FlowEngineToolWindow();
            flowEngineToolWindow.OpenFlow(filePath);
            flowEngineToolWindow.Show();
        }
        public bool CanExport(string filePath)
        {
            return false;
        }
        public void Export(string filePath)
        {
        }
    }


}
