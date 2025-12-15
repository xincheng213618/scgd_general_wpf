using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Windows;

namespace ColorVision.Solution
{
    // 标记本类支持的扩展名，并设为默认
    [GenericEditor("Hex文本编辑器")]
    public class HexEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            if (File.Exists(filePath))
            {
                string GuidId = Tool.GetMD5(filePath);
                var existingDocument = WorkspaceManager.FindDocumentById(WorkspaceManager.layoutRoot, GuidId.ToString());

                if (existingDocument != null)
                {
                    if (existingDocument.Parent is LayoutDocumentPane layoutDocumentPane)
                    {
                        layoutDocumentPane.SelectedContentIndex = layoutDocumentPane.IndexOf(existingDocument); ;
                    }
                    else if (existingDocument.Parent is LayoutFloatingWindow layoutFloatingWindow)
                    {
                        var window = Window.GetWindow(layoutFloatingWindow);
                        if (window != null)
                        {
                            window.Activate();
                        }
                    }
                }
                else
                {
                    HexEditorView hexEditorView = new HexEditorView();
                    hexEditorView.HexEditor.FileName = filePath;


                    LayoutDocument layoutDocument = new LayoutDocument() { ContentId = GuidId, Title = Path.GetFileName(filePath) };
                    layoutDocument.Content = hexEditorView;
                    WorkspaceManager.LayoutDocumentPane.Children.Add(layoutDocument);
                    WorkspaceManager.LayoutDocumentPane.SelectedContentIndex = WorkspaceManager.LayoutDocumentPane.IndexOf(layoutDocument);
                    layoutDocument.IsActiveChanged += (s, e) =>
                    {
                        if (layoutDocument.IsActive)
                        {
                            WorkspaceManager.OnContentIdSelected(filePath);
                        }
                    };
                    layoutDocument.Closing += (s, e) =>
                    {
                        hexEditorView.HexEditor.CloseProvider(true);
                        hexEditorView.HexEditor.Dispose();
                    };
                }
            }
        }
    }
}