using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.Solution.Searches;
using System.IO;
using System.Windows;


namespace ColorVision.Solution.Editor
{
    // 声明支持的图片扩展名，设置为默认编辑器
    [EditorForExtension(".jpg|.png|.jpeg|.tif|.bmp|.tiff|.cvraw|.cvcie", "图片编辑器", isDefault: true)]
    public class ImageEditor : EditorBase
    {
        public override void Open(string filePath)
        {

            string GuidId = Tool.GetMD5(filePath);
            var existingDocument = SolutionViewExtensions.FindDocumentById(SolutionViewExtensions.layoutRoot, GuidId.ToString());

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
                ImageView imageView = new ImageView();
                imageView.OpenImage(filePath);

                LayoutDocument layoutDocument = new LayoutDocument() { ContentId = GuidId, Title = Path.GetFileName(filePath) };
                layoutDocument.Content = imageView;
                SolutionViewExtensions.LayoutDocumentPane.Children.Add(layoutDocument);
                SolutionViewExtensions.LayoutDocumentPane.SelectedContentIndex = SolutionViewExtensions.LayoutDocumentPane.IndexOf(layoutDocument);
                layoutDocument.IsActiveChanged += (s, e) =>
                {
                    if (layoutDocument.IsActive)
                    {
                        SolutionViewExtensions.OnContentIdSelected(filePath);
                    }
                };
                bool isclear = true;
                layoutDocument.Closing += (s, e) =>
                {
                    if (isclear)
                    {
                        isclear = false;
                        imageView.ImageViewModel.ClearImageCommand.Execute(null);
                        e.Cancel = true; // Prevent the document from closing immediately
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(300);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                imageView.Dispose();
                                layoutDocument?.Close();
                            });
                        });
                    }
                    else
                    {
                        e.Cancel = false; // Prevent the document from closing immediately
                    }
                };
            }
        }
    }

}
