using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Solution.Editor
{
    [FolderEditor("图片编辑器")]
    public class ProjectEditor : EditorBase
    {
        public override void Open(string filePath)
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

                var directory = new DirectoryInfo(filePath);

                StackPanel stackPanel = new StackPanel();
                foreach (var item in directory.GetFiles())
                {
                    TextBox textBox = new TextBox
                    {
                        Text = item.Name,
                        Margin = new Thickness(5),
                        Tag = item.FullName,
                        IsReadOnly = true
                    };

                    stackPanel.Children.Add(textBox);   

                }
                UserControl userControl = new UserControl
                {
                    Content = stackPanel
                };

                LayoutDocument layoutDocument = new LayoutDocument() { ContentId = GuidId, Title = Path.GetFileName(filePath) };

                layoutDocument.Content = userControl;
                WorkspaceManager.LayoutDocumentPane.Children.Add(layoutDocument);
                WorkspaceManager.LayoutDocumentPane.SelectedContentIndex = WorkspaceManager.LayoutDocumentPane.IndexOf(layoutDocument);
                layoutDocument.IsActiveChanged += (s, e) =>
                {
                    if (layoutDocument.IsActive)
                    {
                        WorkspaceManager.OnContentIdSelected(filePath);
                    }
                };
            }
        }
    }
}
