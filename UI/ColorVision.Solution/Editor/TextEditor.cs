using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Windows;

namespace ColorVision.Solution
{
    // 标记本类支持的扩展名，并设为默认
    [EditorForExtension(".dat|.ini|.txt|.cs|.json|.java|.go|.md|.py|.js|.xml|.xaml|.cpp|.c|.bat|.sql|.css|.ps1|.cvproj|.csproj|.fsproj|.vbproj", "文本编辑器", isDefault: true, resourceKey: "Sol_Editor_Text", editorId: "colorvision.text", priority: 100)]
    public class TextEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            EditorDocumentService.Open(
                filePath,
                GetType(),
                Path.GetFileName(filePath),
                () => new AvalonEditControll(filePath),
                control => control.Dispose());
        }
    }  
}
