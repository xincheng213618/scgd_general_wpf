using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Workspace;
using ColorVision.UI.Desktop;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Windows;


namespace ColorVision.Solution.Editor
{

    [GenericEditor("WebView2编辑器", resourceKey: "Sol_Editor_WebView2", editorId: "colorvision.web", isVisibleInOpenWith: false), FolderEditor("WebView2编辑器", resourceKey: "Sol_Editor_WebView2", editorId: "colorvision.web.folder")]
    public class WebView2Editor : EditorBase
    {
        public override void Open(string filePath)
        {
            string resourcePath = filePath;
            EditorDocumentService.Open(
                resourcePath,
                GetType(),
                Path.GetFileName(resourcePath),
                () =>
                {
                    var webView = new WebView2();
                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        await WebViewService.EnsureWebViewInitializedAsync(webView);
                        string source = Uri.IsWellFormedUriString(resourcePath, UriKind.RelativeOrAbsolute)
                            ? resourcePath
                            : "file:///" + resourcePath.Replace("\\", "/");
                        webView.Source = new Uri(source, UriKind.RelativeOrAbsolute);
                    });
                    return webView;
                },
                webView => webView.Dispose());
        }
    }
}
