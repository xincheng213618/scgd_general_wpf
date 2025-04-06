using ColorVision.UI;
using System.IO;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ColorVision.Solution.Editor
{
    public class TextEditor : IEditorBase
    {
        public override string Extension => ".txt|.cs|.json|.java|.go|.md|.py|.dat";

        public override string Name => "文本编辑器";
        public override Control? Open(string FilePath)
        {
            if (File.Exists(FilePath))
            {
                AvalonEditWindow window1 = new AvalonEditWindow(FilePath);
                window1.Show();
                return new Control();
            }
            return null;
        }
    }
}