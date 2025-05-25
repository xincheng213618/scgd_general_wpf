using ColorVision.Themes;
using log4net;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;

namespace ColorVision.UI
{
    public class WebViewService
    {
        private static CoreWebView2Environment _sharedEnv;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static Task<CoreWebView2Environment> _initTask;
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

                if (_initTask != null)
                    return await _initTask;

                _initTask = InternalCreateEnvAsync();
                _sharedEnv = await _initTask;
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
            var env = await GetOrCreateEnvironmentAsync();
            try
            {
                await webView.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(typeof(WebViewService)).Error("WebView2 初始化失败", ex);
            }
        }

        // 渲染 Markdown 到 WebView2
        public static void RenderMarkdown(WebView2 webView, string html)
        {
            string cssPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "css", "github-markdown.css");
            string cssContent = File.ReadAllText(cssPath);
            webView.CoreWebView2.Profile.PreferredColorScheme =ThemeManager.Current.CurrentUITheme == Theme.Dark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;

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
