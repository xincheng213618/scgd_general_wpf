using ColorVision.Solution;
using ColorVision.Themes;
using System.Windows;

namespace ColorVision.UI
{
    /// <summary>
    /// Interaction logic for MarkdownViewWindow.xaml
    /// </summary>
    public partial class MarkdownViewWindow : Window
    {
        public MarkdownViewWindow(string html)
        {
            InitializeComponent();
            this.ApplyCaption();
            Phrase(html);
        }

        private async void Phrase( string html)
        {
            await WebViewService.EnsureWebViewInitializedAsync(webView);
            WebViewService.RenderMarkdown(webView, html);
        }
    }
}