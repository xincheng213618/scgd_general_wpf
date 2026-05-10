using ColorVision.UI.Desktop;
using Markdig;
using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DrawingColor = System.Drawing.Color;

namespace ColorVision.Copilot
{
    public partial class CopilotMarkdownView : UserControl
    {
        public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(CopilotMarkdownView),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        private readonly DispatcherTimer _renderTimer;
        private bool _isInitialized;
        private string _pendingMarkdown = string.Empty;

        public CopilotMarkdownView()
        {
            InitializeComponent();
            Browser.DefaultBackgroundColor = DrawingColor.Transparent;

            _renderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120),
            };
            _renderTimer.Tick += RenderTimer_Tick;

            Loaded += CopilotMarkdownView_Loaded;
            Unloaded += CopilotMarkdownView_Unloaded;
            SizeChanged += CopilotMarkdownView_SizeChanged;
        }

        public string Markdown
        {
            get => (string)GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CopilotMarkdownView view)
                view.ScheduleRender();
        }

        private async void CopilotMarkdownView_Loaded(object sender, RoutedEventArgs e)
        {
            await EnsureInitializedAsync();
            ScheduleRender();
        }

        private void CopilotMarkdownView_Unloaded(object sender, RoutedEventArgs e)
        {
            _renderTimer.Stop();
        }

        private async void RenderTimer_Tick(object? sender, EventArgs e)
        {
            _renderTimer.Stop();
            await RenderAsync();
        }

        private void CopilotMarkdownView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isInitialized || Math.Abs(e.PreviousSize.Width - e.NewSize.Width) < 1)
                return;

            _ = UpdateHeightAsync();
        }

        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized)
                return;

            await WebViewService.EnsureWebViewInitializedAsync(Browser);
            if (Browser.CoreWebView2 == null)
                return;

            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            Browser.CoreWebView2.Settings.IsZoomControlEnabled = false;
            Browser.NavigationCompleted += Browser_NavigationCompleted;
            _isInitialized = true;
        }

        private void ScheduleRender()
        {
            _pendingMarkdown = Markdown ?? string.Empty;
            if (!IsLoaded)
                return;

            _renderTimer.Stop();
            _renderTimer.Start();
        }

        private async Task RenderAsync()
        {
            await EnsureInitializedAsync();
            if (!_isInitialized)
                return;

            Browser.NavigateToString(BuildHtml(_pendingMarkdown));
        }

        private async void Browser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            await UpdateHeightAsync();
        }

        private async Task UpdateHeightAsync()
        {
            if (Browser.CoreWebView2 == null)
                return;

            try
            {
                var result = await Browser.ExecuteScriptAsync("Math.max(document.body.scrollHeight, document.documentElement.scrollHeight)");
                var height = ParseScriptNumber(result);
                Height = Math.Max(20, height + 2);
            }
            catch
            {
            }
        }

        private static double ParseScriptNumber(string scriptResult)
        {
            try
            {
                using var document = JsonDocument.Parse(scriptResult);
                return document.RootElement.ValueKind switch
                {
                    JsonValueKind.Number => document.RootElement.GetDouble(),
                    JsonValueKind.String when double.TryParse(document.RootElement.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) => value,
                    _ => 20,
                };
            }
            catch
            {
                return 20;
            }
        }

        private static string BuildHtml(string markdown)
        {
            var body = string.IsNullOrWhiteSpace(markdown)
                ? "&nbsp;"
                : Markdig.Markdown.ToHtml(markdown, Pipeline);

            return $@"
<html>
<head>
    <meta charset='utf-8'>
    <meta name='color-scheme' content='light dark'>
    <style>
        html, body {{
            margin: 0;
            padding: 0;
            background: transparent;
            overflow: hidden;
            font-family: 'Segoe UI', sans-serif;
            line-height: 1.6;
        }}

        body {{
            color: #dce4ef;
            font-size: 14px;
        }}

        .markdown-body > *:first-child {{ margin-top: 0; }}
        .markdown-body > *:last-child {{ margin-bottom: 0; }}
        p, ul, ol, pre, table, blockquote {{ margin: 0 0 12px 0; }}
        h1, h2, h3, h4, h5, h6 {{ margin: 0 0 10px 0; color: #f5f8fb; }}
        a {{ color: #7bb6ff; text-decoration: none; }}
        blockquote {{
            margin-left: 0;
            padding-left: 12px;
            border-left: 3px solid #34506d;
            opacity: 0.88;
        }}
        code {{
            font-family: Consolas, 'Courier New', monospace;
            background: rgba(43, 55, 69, 0.7);
            border-radius: 6px;
            padding: 2px 5px;
        }}
        pre {{
            background: #0f141b;
            border: 1px solid #253241;
            border-radius: 10px;
            padding: 12px 14px;
            overflow-x: auto;
        }}
        pre code {{
            background: transparent;
            padding: 0;
            border-radius: 0;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
        }}
        th, td {{
            border: 1px solid #2d3c4c;
            padding: 6px 8px;
            text-align: left;
        }}
        @media (prefers-color-scheme: light) {{
            body {{ color: #1f2933; }}
            h1, h2, h3, h4, h5, h6 {{ color: #0f1720; }}
            code {{ background: rgba(232, 238, 244, 0.95); }}
            pre {{ background: #f6f8fa; border-color: #d0d7de; }}
            th, td {{ border-color: #d0d7de; }}
            blockquote {{ border-left-color: #7d8ea1; }}
        }}
    </style>
</head>
<body>
    <div class='markdown-body'>{body}</div>
</body>
</html>";
        }
    }
}