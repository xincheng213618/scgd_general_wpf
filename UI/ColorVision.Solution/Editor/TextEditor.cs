using System.IO;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ColorVision.Solution.Editor
{
    public class TextEditor : IEditorBase
    {
        public override string Extension => ".txt|.cs|.json|.java|.go|.md|.py";

        public override string Name => "文本编辑器";
        public override Control? Open(string FilePath)
        {
            if (File.Exists(FilePath))
            {
                TextEditorControl textEditorControl = new TextEditorControl(FilePath);
                return textEditorControl;
            }
            return null;
        }
    }
}