using System.Windows.Controls;

namespace ColorVision.Solution.Editor
{
    public interface IEditor
    {
        string Name { get; }
        Control? Open(string filePath);
    }
}