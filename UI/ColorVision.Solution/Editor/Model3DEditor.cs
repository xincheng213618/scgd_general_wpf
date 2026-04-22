using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.EditorTools.ThreeD;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Editor
{
    [EditorForExtension(".obj|.stl", "3D模型查看器", isDefault: true)]
    public class Model3DEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            if (!File.Exists(filePath)) return;

            string guidId = Tool.GetMD5(filePath);
            var existingDocument = WorkspaceManager.FindDocumentById(WorkspaceManager.layoutRoot, guidId.ToString());

            if (existingDocument != null)
            {
                if (existingDocument.Parent is LayoutDocumentPane layoutDocumentPane)
                {
                    layoutDocumentPane.SelectedContentIndex = layoutDocumentPane.IndexOf(existingDocument);
                }
                else if (existingDocument.Parent is LayoutFloatingWindow layoutFloatingWindow)
                {
                    var window = Window.GetWindow(layoutFloatingWindow);
                    window?.Activate();
                }
            }
            else
            {
                var control = new ModelViewer3DControl();
                control.SetInitialFile(filePath);

                LayoutDocument layoutDocument = new LayoutDocument()
                {
                    ContentId = guidId,
                    Title = Path.GetFileName(filePath)
                };
                layoutDocument.Content = control;
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
                    // Cleanup is handled by ModelViewer3DControl.Unloaded
                };
            }
        }
    }
}
