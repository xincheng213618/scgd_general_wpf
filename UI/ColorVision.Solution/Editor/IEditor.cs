using System.Windows.Controls;

namespace ColorVision.Solution.Editor
{
    public interface IEditor
    {
        Control? Open(string filePath);
    }
}