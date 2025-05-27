using ColorVision.Solution.Editor;
using ColorVision.UI.PropertyEditor;
using System.IO;
using System.Windows.Controls;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.MethodExtention;

namespace ColorVision.Solution
{
    // 标记本类支持的扩展名，并设为默认
    [EditorForExtension(".txt|.cs|.json|.java|.go|.md|.py|.dat|.js|.xml|.xaml|.cpp|.c|.bat|.sql|.css|.ps1", isDefault: true)]
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
    // 标记本类支持的扩展名，并设为默认
    [EditorForExtension(".txt|.cs|.json|.java|.go|.md|.py|.dat|.js|.xml|.xaml|.cpp|.c|.bat|.sql|.css|.ps1", isDefault: true)]
    public class HexEditor : EditorBase
    {
        public override string Name => "文本编辑器";

        public override Control? Open(string filePath)
        {
            if (File.Exists(filePath))
            {
                var HexEditor = new WpfHexaEditor.HexEditor();
                HexEditor.PreloadByteInEditorMode = PreloadByteInEditor.MaxVisibleLineExtended;
                HexEditor.FileName = filePath;
                return HexEditor;
            }
            return null;
        }
    }
}