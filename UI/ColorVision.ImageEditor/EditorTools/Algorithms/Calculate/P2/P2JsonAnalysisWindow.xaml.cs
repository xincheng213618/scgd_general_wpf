using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.P2
{
    public partial class P2JsonAnalysisWindow : Window
    {
        private readonly Func<string, Task<P2NativeResult>> _execute;
        private readonly Func<JObject, string> _summary;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;
        private readonly P2OverlayKind _overlayKind;
        private P2ResultOverlayVisual? _overlay;
        private string _rawResult = string.Empty;
        private bool _closed;

        internal P2JsonAnalysisWindow(
            string title,
            string inputDescription,
            string defaultConfig,
            Func<string, Task<P2NativeResult>> execute,
            Func<JObject, string> summary,
            DrawEditorContext drawContext,
            ImageViewConfig config,
            P2OverlayKind overlayKind)
        {
            InitializeComponent();
            this.ApplyCaption();
            Title = title;
            InputDescriptionText.Text = inputDescription;
            ConfigText.Text = P2NativeJson.Format(defaultConfig);
            StatusText.Text = "调整 JSON 参数后点击运行；Overlay 仅用于当前调试窗口。";
            _execute = execute;
            _summary = summary;
            _drawContext = drawContext;
            _config = config;
            _overlayKind = overlayKind;
            Closed += (_, _) =>
            {
                _closed = true;
                ClearOverlay();
            };
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (JToken.Parse(ConfigText.Text) is not JObject)
                {
                    throw new InvalidOperationException("配置 JSON 必须是对象。");
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                MessageBox.Show(this, ex.Message, "配置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RunButton.IsEnabled = false;
            StatusText.Text = "计算中...";
            try
            {
                P2NativeResult result = await _execute(ConfigText.Text);
                if (_closed) return;
                _rawResult = P2NativeJson.Format(result.RawJson);
                ResultText.Text = _rawResult;
                MetricsGrid.ItemsSource = P2ResultRows.Build(result.Json);
                SummaryText.Text = _summary(result.Json);
                StatusText.Text = BuildStatus(result.Json);
                if (DrawOverlayCheckBox.IsChecked == true)
                {
                    if (AutoClearCheckBox.IsChecked == true) ClearOverlay();
                    _overlay = new P2ResultOverlayVisual(result.Json, _overlayKind, _config);
                    _overlay.ApplyLayoutScale(new DrawingVisualScaleContext(
                        _drawContext.DrawCanvas.IsLayoutUpdated,
                        _drawContext.DrawCanvas.Scale,
                        _drawContext.DrawCanvas.TextFontSizeOverride));
                    _drawContext.DrawCanvas.AddOverlayVisual(_overlay);
                }
            }
            catch (Exception ex)
            {
                if (_closed) return;
                StatusText.Text = ex.Message;
                MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!_closed) RunButton.IsEnabled = true;
            }
        }

        private void ClearOverlay_Click(object sender, RoutedEventArgs e)
        {
            ClearOverlay();
            StatusText.Text = "Overlay 已清除。";
        }

        private void CopyConfig_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(P2NativeJson.Format(ConfigText.Text));
            StatusText.Text = "配置 JSON 已复制。";
        }

        private void CopyResult_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rawResult)) return;
            Clipboard.SetText(_rawResult);
            StatusText.Text = "结果 JSON 已复制。";
        }

        private void ClearOverlay()
        {
            if (_overlay == null) return;
            _drawContext.DrawCanvas.RemoveOverlayVisual(_overlay);
            _overlay = null;
        }

        private static string BuildStatus(JObject result)
        {
            string state = result.Value<bool?>("success") == true ? "OK" : "未通过";
            string code = result.Value<string>("statusCode") ?? string.Empty;
            string message = result.Value<string>("message") ?? string.Empty;
            string warnings = result["warnings"] is JArray array && array.Count > 0
                ? $"    Warning: {string.Join("; ", array.Values<string>())}"
                : string.Empty;
            return $"状态: {state}    StatusCode: {code}    {message}{warnings}";
        }
    }
}
