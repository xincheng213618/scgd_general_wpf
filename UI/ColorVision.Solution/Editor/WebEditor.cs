using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.Solution.Searches;
using ColorVision.UI;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Windows;


namespace ColorVision.Solution.Editor
{

    [GenericEditor("WebView2编辑器"), FolderEditor("WebView2编辑器")]
    public class WebView2Editor : EditorBase
    {
        public override  void Open(string filePath)
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

                Application.Current.Dispatcher.Invoke(async () =>
                {
                    await WebViewService.EnsureWebViewInitializedAsync(webView2);

                    if (!Uri.IsWellFormedUriString(filePath, UriKind.Absolute))
                    {
                        filePath = "file:///" + filePath.Replace("\\", "/");
                    }
                    webView2.Source = new Uri(filePath, UriKind.Absolute);

                });



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
                layoutDocument.Closing += (s, e) =>
                {
                    webView2?.Dispose();
                };

            }
        }
    }
}
