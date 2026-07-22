using log4net;
using Markdig;
using Microsoft.Web.WebView2.Wpf;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Desktop.Marketplace
{
    public static class MarketplaceMarkdownPresenter
    {
        private sealed class RenderState
        {
            public string LastRenderKey { get; set; } = string.Empty;
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceMarkdownPresenter));
        private static readonly ConditionalWeakTable<WebView2, RenderState> RenderStates = new();

        public static async Task RenderAsync(WebView2 webView, string? markdown, string emptyMessage, CancellationToken cancellationToken = default)
        {
            if (webView == null)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            string normalizedMarkdown = markdown ?? string.Empty;
            string renderKey = $"{emptyMessage}\n{normalizedMarkdown}";
            RenderState state = RenderStates.GetOrCreateValue(webView);
            if (string.Equals(state.LastRenderKey, renderKey, StringComparison.Ordinal))
                return;

            try
            {
                await WebViewService.EnsureWebViewInitializedAsync(webView);
                cancellationToken.ThrowIfCancellationRequested();
                if (webView.CoreWebView2 == null)
                    return;

                string html = string.IsNullOrWhiteSpace(normalizedMarkdown)
                    ? $"<div style='padding:24px 0;color:#6b7280;font-style:italic;'>{System.Net.WebUtility.HtmlEncode(emptyMessage)}</div>"
                    : Markdown.ToHtml(normalizedMarkdown);

                cancellationToken.ThrowIfCancellationRequested();
                WebViewService.RenderMarkdown(webView, html);
                state.LastRenderKey = renderKey;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Debug($"Marketplace markdown render failed: {ex.Message}");
            }
        }
    }
}
