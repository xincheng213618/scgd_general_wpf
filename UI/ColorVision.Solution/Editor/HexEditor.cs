using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Windows;

namespace ColorVision.Solution
{
    // 标记本类支持的扩展名，并设为默认
    [GenericEditor("Hex文本编辑器", resourceKey: "Sol_Editor_Hex", editorId: "colorvision.hex", priority: 50)]
    public class HexEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            EditorDocumentService.Open(
                filePath,
                GetType(),
                Path.GetFileName(filePath),
                () =>
                {
                    var hexEditorView = new HexEditorView();
                    hexEditorView.HexEditorControl.FileName = filePath;
                    return hexEditorView;
                },
                hexEditorView => hexEditorView.HexEditorControl.CloseProvider(true));
        }
    }
}
