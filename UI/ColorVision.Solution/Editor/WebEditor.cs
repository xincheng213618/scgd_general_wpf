using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Searches;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Windows;


namespace ColorVision.Solution.Editor
{
    [GenericEditor("浏览器编辑器")]
    public class WebView2Editor : EditorBase
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

                var directory = new DirectoryInfo(filePath);

                WebView2 webView2 = new WebView2();


                LayoutDocument layoutDocument = new LayoutDocument() { ContentId = GuidId, Title = Path.GetFileName(filePath) };

                layoutDocument.Content = webView2;
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
            }
        }
    }
}
