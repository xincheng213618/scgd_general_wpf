using ColorVision.Themes;
using log4net;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.UI.Desktop
{
    public class WebViewService
    {
        private static CoreWebView2Environment _sharedEnv;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly ILog _log = LogManager.GetLogger(typeof(WebViewService));

        // 线程安全异步初始化
        public static async Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync()
        {
            if (_sharedEnv != null)
                return _sharedEnv;

            await _semaphore.WaitAsync();
            try
            {
                if (_sharedEnv != null)
                    return _sharedEnv;

                _sharedEnv = await InternalCreateEnvAsync();
                return _sharedEnv;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static async Task<CoreWebView2Environment> InternalCreateEnvAsync()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userDataFolder = Path.Combine(appData, "ColorVision");

            if (!Directory.Exists(userDataFolder))
                Directory.CreateDirectory(userDataFolder);

            return await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        }

        public static async Task EnsureWebViewInitializedAsync(WebView2 webView)
        {
            if (webView == null) return;

            var env = await GetOrCreateEnvironmentAsync();
            try
            {
                await webView.EnsureCoreWebView2Async(env);

                // 设置初始主题
                SetWebViewColorScheme(webView);

                // 内部事件处理器
                void OnThemeChanged(Theme sender)
                {
                    try
                    {
                        if (webView?.CoreWebView2 != null)
                        {
                            SetWebViewColorScheme(webView);
                        }
                    }
                    catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException || ex is NullReferenceException)
                    {
                        // 如果控件已被释放但在 Unloaded 之前触发了事件，安全地取消订阅
                        ThemeManager.Current.CurrentUIThemeChanged -= OnThemeChanged;
                    }
                }

                // 订阅主题变化事件
                ThemeManager.Current.CurrentUIThemeChanged += OnThemeChanged;

                // 【修复核心】监听 WebView 的卸载事件，主动取消订阅以防止内存泄漏
                RoutedEventHandler onUnloaded = null;
                onUnloaded = (s, e) =>
                {
                    ThemeManager.Current.CurrentUIThemeChanged -= OnThemeChanged;
                    webView.Unloaded -= onUnloaded; // 避免重复卸载
                };
                webView.Unloaded += onUnloaded;
            }
            catch (Exception ex)
            {
                _log.Error("WebView2 初始化失败", ex);
            }
        }

        // 抽取设置主题方法
        private static void SetWebViewColorScheme(WebView2 webView)
        {
            if (webView?.CoreWebView2?.Profile == null) return;

            webView.CoreWebView2.Profile.PreferredColorScheme =
                ThemeManager.Current.CurrentUITheme == Theme.Dark
                    ? CoreWebView2PreferredColorScheme.Dark
                    : CoreWebView2PreferredColorScheme.Light;
        }

        // 渲染 Markdown 到 WebView2
        public static void RenderMarkdown(WebView2 webView, string html)
        {
            // 防止在未初始化完成时调用报错
            if (webView?.CoreWebView2 == null)
            {
                _log.Warn("WebView2 尚未初始化完成，无法渲染 Markdown。");
                return;
            }

            SetWebViewColorScheme(webView);

            string cssPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "css", "github-markdown.css");
            string cssContent = File.Exists(cssPath) ? File.ReadAllText(cssPath) : string.Empty;

            string htmlContent = $@"
<html>
<head>
    <meta charset='utf-8'>
    <meta name=""color-scheme"" content=""light dark"">
    <style>
        {cssContent}
        body {{
            box-sizing: border-box;
            min-width: 200px;
            max-width: 980px;
            margin: 0 auto;
            padding: 45px;
        }}
        @media (prefers-color-scheme: dark) {{
            body {{
                background-color: #0d1117;
            }}
        }}
    </style>
</head>
<body>
    <div class='markdown-body'>
        {html}
    </div>
</body>
</html>";

            webView.NavigateToString(htmlContent);
        }
    }
}