using ColorVision.UI;
using ColorVision.UI.PropertyEditor;
using System.IO;
using System.Windows.Controls;

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
                return new AvalonEditControll(FilePath);
            }
            return null;
        }
    }
}