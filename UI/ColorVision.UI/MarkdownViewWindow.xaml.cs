using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

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
            Phrase(html);
        }

        private async void Phrase( string html)
        {
            await WebViewService.EnsureWebViewInitializedAsync(webView);
            WebViewService.RenderMarkdown(webView, html);
        }
    }
}