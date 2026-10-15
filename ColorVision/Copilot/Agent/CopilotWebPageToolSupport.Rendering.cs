using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotWebPageRenderingRequiredException : InvalidOperationException
    {
        public CopilotWebPageRenderingRequiredException()
            : base("Could not extract readable web page body text. The page may require script rendering.") { }
    }

    public static partial class CopilotWebPageToolSupport
    {
        internal static async Task<CopilotFetchedWebPageContent> ExtractWithBrowserFallbackAsync(
            Uri uri, string mediaType, string content,
            Func<Uri, CancellationToken, Task<CopilotFetchedWebPageContent>>? renderPage,
            CancellationToken cancellationToken)
        {
            CopilotFetchedWebPageContent? extracted = null;
            try { extracted = ExtractDownloadedContent(uri, mediaType, content); }
            catch (CopilotWebPageRenderingRequiredException) when (renderPage != null) { }

            var needsRendering = extracted == null || extracted.Value.IsSparseExtraction
                || (!IsStructuredWebContentType(mediaType) && extracted.Value.Content.Length < 200
                    && content.Contains("<script", StringComparison.OrdinalIgnoreCase));
            if (renderPage == null || !needsRendering) return extracted!.Value;

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var rendered = await renderPage(uri, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(rendered.Content))
                    throw new InvalidOperationException("浏览器渲染后仍没有可读正文；页面可能需要登录或交互。");
                return rendered with { BrowserRendered = true };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                var detail = CopilotUserFacingErrorFormatter.Sanitize(ex.Message);
                if (extracted is { } staticPage)
                    return staticPage with { IsSparseExtraction = true, RenderingNotice = $"Browser rendering failed; only static text is available: {detail}" };
                throw new InvalidOperationException($"网页已下载，但静态正文为空，浏览器渲染也未完成：{detail}", ex);
            }
        }
    }
}
