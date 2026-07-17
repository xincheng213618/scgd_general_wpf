using ColorVision.Themes;
using ColorVision.UI.Desktop;
using System.Windows;

namespace ColorVision.UI
{
    public partial class MarkdownViewWindow : Window
    {
        private readonly string _html;

        public MarkdownViewWindow(string html)
        {
            _html = html ?? string.Empty;
            InitializeComponent();
            this.ApplyCaption();
            Loaded += MarkdownViewWindow_Loaded;
        }

        private async void MarkdownViewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MarkdownViewWindow_Loaded;
            await WebViewService.EnsureWebViewInitializedAsync(webView);
            WebViewService.RenderMarkdown(webView, _html);
        }
    }
}
