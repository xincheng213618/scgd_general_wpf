using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.P2
{
    public partial class StereoFusionDebugWindow : Window
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;
        private BitmapSource? _rightImage;
        private P2ResultOverlayVisual? _overlay;
        private string _rawResult = string.Empty;
        private bool _hasExternalCalibration;
        private bool _closed;

        public StereoFusionDebugWindow(
            ImageProcessingContext imageContext,
            DrawEditorContext drawContext,
            ImageViewConfig config)
        {
            InitializeComponent();
            this.ApplyCaption();
            _imageContext = imageContext;
            _drawContext = drawContext;
            _config = config;

            if (_imageContext.HImageCache is HImage image)
            {
                LeftInfoText.Text = $"左图：当前编辑器图像 ({image.cols} x {image.rows})";
                ConfigText.Text = CreateDefaultConfig(image.cols, image.rows, image.cols, image.rows);
            }
            LeftPreview.Source = _imageContext.ImageShow.Source;
            StatusText.Text = "请选择右图并加载真实标定参数。当前 JSON 中的焦距和基线仅为界面调试示例，不能用于测量。";
            Closed += (_, _) =>
            {
                _closed = true;
                ClearOverlay();
            };
        }

        private void SelectRightImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "选择双目右图",
                Filter = "图像文件|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.webp|所有文件|*.*"
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                _rightImage = P2BitmapLoader.Load(dialog.FileName);
                ClearOverlay();
                RightPreview.Source = _rightImage;
                RightInfoText.Text = $"右图：{Path.GetFileName(dialog.FileName)} ({_rightImage.PixelWidth} x {_rightImage.PixelHeight})";
                if (!_hasExternalCalibration && _imageContext.HImageCache is HImage left)
                {
                    ConfigText.Text = CreateDefaultConfig(left.cols, left.rows, _rightImage.PixelWidth, _rightImage.PixelHeight);
                }
                StatusText.Text = "右图已加载；请确认标定矩阵、畸变、旋转和平移均与这对图像一致。";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "加载右图失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCalibration_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "加载双目标定 JSON",
                Filter = "JSON 文件|*.json|所有文件|*.*"
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                if (JToken.Parse(File.ReadAllText(dialog.FileName)) is not JObject loaded)
                {
                    throw new InvalidOperationException("标定 JSON 必须是对象。");
                }

                if (loaded["calibration"] is JObject)
                {
                    ConfigText.Text = loaded.ToString(Formatting.Indented);
                }
                else
                {
                    JObject root = JObject.Parse(ConfigText.Text);
                    root["calibration"] = loaded;
                    ConfigText.Text = root.ToString(Formatting.Indented);
                }
                _hasExternalCalibration = true;
                StatusText.Text = $"已加载标定配置：{Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "加载标定失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rightImage == null)
            {
                MessageBox.Show(this, "请先选择右图。", "双目标定融合", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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
            StatusText.Text = "双目检测与三角化计算中...";
            try
            {
                BitmapSource rightAtStart = _rightImage;
                P2NativeResult result = await RunAsync(rightAtStart, ConfigText.Text);
                if (_closed || !ReferenceEquals(_rightImage, rightAtStart)) return;
                ApplyResult(result);
            }
            catch (Exception ex)
            {
                if (_closed) return;
                StatusText.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "双目标定融合", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!_closed) RunButton.IsEnabled = true;
            }
        }

        private async Task<P2NativeResult> RunAsync(BitmapSource rightImage, string config)
        {
            if (_imageContext.HImageCache is not HImage left)
            {
                throw new InvalidOperationException("当前左图已经关闭或切换。");
            }

            IntPtr sourcePointer = left.pData;
            using P2ImageSnapshot leftSnapshot = P2ImageSnapshot.Copy(left);
            using P2ImageSnapshot rightSnapshot = P2ImageSnapshot.FromBitmap(rightImage);
            RoiRect leftRoi = P2RoiHelper.WholeImage(leftSnapshot.Image);
            RoiRect rightRoi = P2RoiHelper.WholeImage(rightSnapshot.Image);
            P2NativeResult result = await Task.Run(() => P2NativeJson.Invoke(
                "双目标定融合",
                (out IntPtr result) => OpenCVMediaHelper.M_CalStereoBinocularFusion(
                    leftSnapshot.Image,
                    rightSnapshot.Image,
                    leftRoi,
                    rightRoi,
                    config,
                    out result)));
            if (_imageContext.HImageCache is not HImage latest || latest.pData != sourcePointer)
            {
                throw new InvalidOperationException("计算期间左图发生了切换，结果已丢弃。");
            }
            return result;
        }

        private void ApplyResult(P2NativeResult result)
        {
            _rawResult = P2NativeJson.Format(result.RawJson);
            ResultText.Text = _rawResult;
            PointsGrid.ItemsSource = BuildPointRows(result.Json);
            SummaryText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "有效点: {0}/5    深度: {1:F2} mm    重投影: {2:F3} px    Confidence: {3:F3}",
                result.Json.Value<int?>("validPointCount") ?? 0,
                result.Json.Value<double?>("meanDepthMm") ?? 0.0,
                result.Json.Value<double?>("meanReprojectionErrorPixels") ?? 0.0,
                result.Json.Value<double?>("confidence") ?? 0.0);

            string warnings = result.Json["warnings"] is JArray array && array.Count > 0
                ? $"    Warning: {string.Join("; ", array.Values<string>())}"
                : string.Empty;
            StatusText.Text = $"StatusCode: {result.Json.Value<string>("statusCode")}    {result.Json.Value<string>("message")}{warnings}";

            if (_imageContext.HImageCache is HImage left && _rightImage != null)
            {
                LeftPointOverlay.SetResult(result.Json, true, left.cols, left.rows);
                RightPointOverlay.SetResult(result.Json, false, _rightImage.PixelWidth, _rightImage.PixelHeight);
            }

            ClearOverlay();
            _overlay = new P2ResultOverlayVisual(result.Json, P2OverlayKind.StereoLeft, _config);
            _overlay.ApplyLayoutScale(new DrawingVisualScaleContext(
                _drawContext.DrawCanvas.IsLayoutUpdated,
                _drawContext.DrawCanvas.Scale,
                _drawContext.DrawCanvas.TextFontSizeOverride));
            _drawContext.DrawCanvas.AddOverlayVisual(_overlay);
        }

        private static List<StereoPointRow> BuildPointRows(JObject result)
        {
            List<StereoPointRow> rows = new();
            foreach (JObject item in result["points"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                JObject? left = item["leftPoint"] as JObject;
                JObject? right = item["rightPoint"] as JObject;
                JObject? point = item["pointMm"] as JObject;
                double leftError = item.Value<double?>("leftReprojectionErrorPixels") ?? 0.0;
                double rightError = item.Value<double?>("rightReprojectionErrorPixels") ?? 0.0;
                rows.Add(new StereoPointRow(
                    item.Value<string>("role") ?? string.Empty,
                    Coordinate(left),
                    Coordinate(right),
                    point?.Value<double?>("x") ?? 0.0,
                    point?.Value<double?>("y") ?? 0.0,
                    point?.Value<double?>("z") ?? 0.0,
                    item.Value<double?>("parallaxPixels") ?? 0.0,
                    Math.Max(leftError, rightError),
                    item.Value<double?>("confidence") ?? 0.0,
                    item.Value<string>("status") ?? string.Empty));
            }
            return rows;
        }

        private static string Coordinate(JObject? point)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:F1}, {1:F1}",
                point?.Value<double?>("x") ?? 0.0,
                point?.Value<double?>("y") ?? 0.0);
        }

        private static string CreateDefaultConfig(int leftWidth, int leftHeight, int rightWidth, int rightHeight)
        {
            JObject detection = new()
            {
                ["threshold"] = -1.0,
                ["blurKernel"] = 5,
                ["morphKernel"] = 3,
                ["minArea"] = 20,
                ["maxArea"] = 0,
                ["maxCandidates"] = 128
            };
            JObject root = new()
            {
                ["leftDetection"] = detection.DeepClone(),
                ["rightDetection"] = detection.DeepClone(),
                ["minimumParallaxPixels"] = 0.25,
                ["maximumReprojectionErrorPixels"] = 2.0,
                ["requirePositiveDepth"] = true,
                ["calibration"] = new JObject
                {
                    ["leftCameraMatrix"] = new JArray(1000.0, 0.0, leftWidth * 0.5, 0.0, 1000.0, leftHeight * 0.5, 0.0, 0.0, 1.0),
                    ["rightCameraMatrix"] = new JArray(1000.0, 0.0, rightWidth * 0.5, 0.0, 1000.0, rightHeight * 0.5, 0.0, 0.0, 1.0),
                    ["leftDistCoeffs"] = new JArray(0.0, 0.0, 0.0, 0.0, 0.0),
                    ["rightDistCoeffs"] = new JArray(0.0, 0.0, 0.0, 0.0, 0.0),
                    ["rotation"] = new JArray(1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0),
                    ["translation"] = new JArray(-60.0, 0.0, 0.0)
                }
            };
            return root.ToString(Formatting.Indented);
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
            LeftPointOverlay.Clear();
            RightPointOverlay.Clear();
            if (_overlay == null) return;
            _drawContext.DrawCanvas.RemoveOverlayVisual(_overlay);
            _overlay = null;
        }
    }

    public sealed record StereoPointRow(
        string Role,
        string Left,
        string Right,
        double X,
        double Y,
        double Z,
        double Parallax,
        double Reprojection,
        double Confidence,
        string Status);

    public sealed class StereoPointPreviewOverlay : FrameworkElement
    {
        private JObject? _result;
        private bool _left;
        private int _pixelWidth;
        private int _pixelHeight;

        internal void SetResult(JObject result, bool left, int pixelWidth, int pixelHeight)
        {
            _result = result;
            _left = left;
            _pixelWidth = pixelWidth;
            _pixelHeight = pixelHeight;
            InvalidateVisual();
        }

        internal void Clear()
        {
            _result = null;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (_result == null || _pixelWidth <= 0 || _pixelHeight <= 0 || ActualWidth <= 0.0 || ActualHeight <= 0.0)
            {
                return;
            }

            double scale = Math.Min(ActualWidth / _pixelWidth, ActualHeight / _pixelHeight);
            double offsetX = (ActualWidth - _pixelWidth * scale) * 0.5;
            double offsetY = (ActualHeight - _pixelHeight * scale) * 0.5;
            foreach (JObject item in _result["points"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                JObject? point = item[_left ? "leftPoint" : "rightPoint"] as JObject;
                if (point == null) continue;

                Point center = new(
                    offsetX + (point.Value<double?>("x") ?? 0.0) * scale,
                    offsetY + (point.Value<double?>("y") ?? 0.0) * scale);
                Brush brush = item.Value<bool?>("valid") == true ? Brushes.LawnGreen : Brushes.OrangeRed;
                Pen pen = new(brush, 1.5);
                drawingContext.DrawEllipse(null, pen, center, 5.0, 5.0);
                drawingContext.DrawLine(pen, new Point(center.X - 7.0, center.Y), new Point(center.X + 7.0, center.Y));
                drawingContext.DrawLine(pen, new Point(center.X, center.Y - 7.0), new Point(center.X, center.Y + 7.0));

                FormattedText label = new(
                    item.Value<string>("role") ?? string.Empty,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11.0,
                    brush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                drawingContext.DrawText(label, new Point(center.X + 7.0, center.Y - label.Height - 2.0));
            }
        }
    }
}
