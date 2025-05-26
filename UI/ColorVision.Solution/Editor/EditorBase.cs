using System.Windows.Controls;

namespace ColorVision.Solution.Editor
{
    public abstract class EditorBase : IEditor
    {
        public virtual string Name { get; set; }
        public abstract Control? Open(string filePath);
    }
}