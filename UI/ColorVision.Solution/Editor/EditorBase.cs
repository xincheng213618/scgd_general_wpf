using System.Windows.Controls;

namespace ColorVision.Solution.Editor
{
    public abstract class EditorBase : IEditor
    {
        public abstract Control? Open(string filePath);
    }
}