using ColorVision.Solution.Editor;
using ColorVision.UI.PropertyEditor;
using System.IO;
using System.Windows.Controls;

namespace ColorVision.Solution
{
    // 标记本类支持的扩展名，并设为默认
    [EditorForExtension(".txt|.cs|.json|.java|.go|.md|.py|.dat", isDefault: true)]
    public class TextEditor : EditorBase
    {
        public override string Name => "文本编辑器";

        public override Control? Open(string filePath)
        {
            if (File.Exists(filePath))
            {
                return new AvalonEditControll(filePath);
            }
            return null;
        }
    }
}