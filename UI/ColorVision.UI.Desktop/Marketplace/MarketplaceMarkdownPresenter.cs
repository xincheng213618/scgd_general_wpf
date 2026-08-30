using ColorVision.Themes;
using log4net;
using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;

namespace ColorVision.UI.Desktop.Marketplace
{
    public static class MarketplaceMarkdownPresenter
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Weakly held per-view state uses only WaitAsync; the semaphore never allocates a wait handle.")]
        private sealed class RenderState
        {
            public readonly SemaphoreSlim RenderLock = new(1, 1);
            public long RequestVersion;
            public long NavigationVersion;
            public bool IsInitialized;
            public bool HasShell;
            public string? LastRenderKey;
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceMarkdownPresenter));
        private static readonly ConditionalWeakTable<WebView2, RenderState> RenderStates = new();
        private const string MarkdownShell = "<style>html, body, .markdown-body { background: transparent !important; }</style><div id='colorvision-marketplace-markdown'></div>";

        public static async Task RenderAsync(WebView2 webView, string? markdown, string emptyMessage, CancellationToken cancellationToken = default)
        {
            if (webView == null)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            string normalizedMarkdown = markdown ?? string.Empty;
            string renderKey = $"{emptyMessage}\n{normalizedMarkdown}";
            RenderState state = RenderStates.GetOrCreateValue(webView);
            long requestVersion = Interlocked.Increment(ref state.RequestVersion);

            await state.RenderLock.WaitAsync(cancellationToken);
            try
            {
                if (!IsCurrentRequest())
                    return;

                if (!state.IsInitialized)
                {
                    SetBackgroundColor(webView);
                    await WebViewService.EnsureWebViewInitializedAsync(webView);
                    if (webView.CoreWebView2 == null)
                        return;

                    state.IsInitialized = true;
                    webView.CoreWebView2.NavigationStarting += (_, _) =>
                    {
                        state.NavigationVersion++;
                        state.HasShell = false;
                        state.LastRenderKey = null;
                    };
                    void OnThemeChanged(Theme _)
                    {
                        try
                        {
                            SetBackgroundColor(webView);
                        }
                        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
                        {
                            ThemeManager.Current.CurrentUIThemeChanged -= OnThemeChanged;
                        }
                    }
                    ThemeManager.Current.CurrentUIThemeChanged += OnThemeChanged;
                    RoutedEventHandler? onUnloaded = null;
                    onUnloaded = (_, _) =>
                    {
                        ThemeManager.Current.CurrentUIThemeChanged -= OnThemeChanged;
                        webView.Unloaded -= onUnloaded;
                    };
                    webView.Unloaded += onUnloaded;
                }

                if (!IsCurrentRequest())
                    return;
                if (state.HasShell && string.Equals(state.LastRenderKey, renderKey, StringComparison.Ordinal))
                    return;

                if (!state.HasShell)
                {
                    await LoadShellAsync(webView, cancellationToken);
                    if (!IsCurrentRequest())
                        return;
                }

                string html = string.IsNullOrWhiteSpace(normalizedMarkdown)
                    ? $"<div style='padding:24px 0;color:#6b7280;font-style:italic;'>{System.Net.WebUtility.HtmlEncode(emptyMessage)}</div>"
                    : Markdown.ToHtml(normalizedMarkdown);

                long navigationVersion = state.NavigationVersion;
                // A canceled script may still finish in the browser; do not leave its predecessor cached.
                state.LastRenderKey = null;
                string result = await webView.ExecuteScriptAsync($$"""
                    (() => {
                        const content = document.getElementById('colorvision-marketplace-markdown');
                        if (!content) return false;
                        content.innerHTML = {{JsonSerializer.Serialize(html)}};
                        window.scrollTo(0, 0);
                        return true;
                    })();
                    """);
                if (navigationVersion != state.NavigationVersion)
                    return;
                state.HasShell = result == "true";
                if (IsCurrentRequest() && state.HasShell)
                {
                    state.LastRenderKey = renderKey;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Debug($"Marketplace markdown render failed: {ex.Message}");
            }
            finally
            {
                state.RenderLock.Release();
            }

            bool IsCurrentRequest()
            {
                cancellationToken.ThrowIfCancellationRequested();
                return requestVersion == state.RequestVersion;
            }
        }

        private static async Task LoadShellAsync(WebView2 webView, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ulong? navigationId = null;
            void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e) => navigationId = e.NavigationId;
            void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (navigationId != e.NavigationId)
                    return;
                if (e.IsSuccess)
                    completion.TrySetResult();
                else
                    completion.TrySetException(new InvalidOperationException($"Marketplace document navigation failed: {e.WebErrorStatus}"));
            }

            CoreWebView2 core = webView.CoreWebView2;
            core.NavigationStarting += OnNavigationStarting;
            core.NavigationCompleted += OnNavigationCompleted;
            try
            {
                WebViewService.RenderMarkdown(webView, MarkdownShell);
                await completion.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                core.NavigationStarting -= OnNavigationStarting;
                core.NavigationCompleted -= OnNavigationCompleted;
            }
        }

        private static void SetBackgroundColor(WebView2 webView)
        {
            var color = webView.TryFindResource("UpdateDialogSurfaceColor") is System.Windows.Media.Color surface
                ? surface
                : ThemeManager.Current.CurrentUITheme == Theme.Dark
                    ? System.Windows.Media.Color.FromRgb(28, 28, 28)
                    : System.Windows.Media.Color.FromRgb(251, 251, 251);
            webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, color.R, color.G, color.B);
        }
    }
}
