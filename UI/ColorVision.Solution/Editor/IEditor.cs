using System.Windows.Controls;

namespace ColorVision.Solution
{
    public interface IEditor
    {
        string Name { get; }
        Control? Open(string filePath);
    }
}