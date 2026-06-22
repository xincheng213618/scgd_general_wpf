#pragma warning disable CS8602,CS8604
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SurfaceDefect
{
    public partial class SurfaceDefectDebugWindow : Window
    {
        private static readonly char[] ScaleSeparators = { ',', ';', ' ', '\t', '\r', '\n' };

        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly RoiRect _roi;
        private readonly List<SurfaceDefectOverlayVisual> _overlayVisuals = new();
        private SurfaceDefectNativeResult? _result;
        private string _rawJson = string.Empty;

        public SurfaceDefectDebugWindow(ImageProcessingContext imageContext, DrawEditorContext drawContext, RoiRect roi)
        {
            InitializeComponent();
            this.ApplyCaption();

            _imageContext = imageContext;
            _drawContext = drawContext;
            _roi = roi;

            RoiInfoText.Text = BuildRoiText();
            HeaderSummaryText.Text = "未检测";
            StatusText.Text = "调整参数后点击检测；结果会以临时 overlay 画在当前图上。";
        }

        private async void DetectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_imageContext.HImageCache is not HImage image)
            {
                MessageBox.Show(this, "当前没有可检测的图像。", "表面缺陷/Mura 检测", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string configJson;
            try
            {
                configJson = JsonConvert.SerializeObject(ReadConfig(), Formatting.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "参数无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DetectButton.IsEnabled = false;
            StatusText.Text = "检测中...";
            try
            {
                SurfaceDefectNativeResult result = await Task.Run(() => RunNative(image, _roi, configJson));
                ApplyResult(result);
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "表面缺陷/Mura 检测", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DetectButton.IsEnabled = true;
            }
        }

        private void ClearOverlay_Click(object sender, RoutedEventArgs e)
        {
            ClearOverlay();
        }

        private void CopyConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(JsonConvert.SerializeObject(ReadConfig(), Formatting.Indented));
                StatusText.Text = "配置 JSON 已复制。";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "参数无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyJson_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rawJson))
            {
                return;
            }

            Clipboard.SetText(_rawJson);
            StatusText.Text = "检测结果 JSON 已复制。";
        }

        private SurfaceDefectConfig ReadConfig()
        {
            return new SurfaceDefectConfig
            {
                Channel = ReadChannel(),
                Scales = ReadScales(),
                DarkThreshold = ReadDouble(DarkThresholdTextBox, "暗缺陷阈值"),
                BrightThreshold = ReadDouble(BrightThresholdTextBox, "亮缺陷阈值"),
                MinArea = ReadInt(MinAreaTextBox, "最小面积", minValue: 1),
                MaxArea = ReadInt(MaxAreaTextBox, "最大面积", minValue: 1),
                MuraMinArea = ReadInt(MuraMinAreaTextBox, "Mura 最小面积", minValue: 1),
                OpenKernel = ReadInt(OpenKernelTextBox, "Open Kernel", minValue: 0),
                CloseKernel = ReadInt(CloseKernelTextBox, "Close Kernel", minValue: 0),
                MergeDistance = ReadInt(MergeDistanceTextBox, "合并距离", minValue: 0),
                MaxDefects = ReadInt(MaxDefectsTextBox, "最大输出数", minValue: 1),
                EnableDark = EnableDarkCheckBox.IsChecked == true,
                EnableBright = EnableBrightCheckBox.IsChecked == true,
                EnableLineDetect = EnableLineCheckBox.IsChecked == true,
                LineAspectRatio = ReadDouble(LineAspectRatioTextBox, "线缺陷长宽比", minValue: 1.0),
                MinSeverity = ReadDouble(MinSeverityTextBox, "最小 severity", minValue: 0.0),
                MinorSeverity = ReadDouble(MinorSeverityTextBox, "Minor", minValue: 0.0),
                MajorSeverity = ReadDouble(MajorSeverityTextBox, "Major", minValue: 0.0),
                CriticalSeverity = ReadDouble(CriticalSeverityTextBox, "Critical", minValue: 0.0)
            };
        }

        private int ReadChannel()
        {
            if (ChannelCombo.SelectedItem is ComboBoxItem item &&
                int.TryParse(Convert.ToString(item.Tag, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out int channel))
            {
                return channel;
            }

            return -1;
        }

        private List<int> ReadScales()
        {
            string[] parts = ScalesTextBox.Text.Split(ScaleSeparators, StringSplitOptions.RemoveEmptyEntries);
            List<int> scales = new();
            foreach (string part in parts)
            {
                if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int scale) || scale <= 0)
                {
                    throw new InvalidOperationException("背景尺度必须是正整数列表，例如 31,61,121。");
                }

                scales.Add(scale % 2 == 0 ? scale + 1 : scale);
            }

            if (scales.Count == 0)
            {
                throw new InvalidOperationException("背景尺度不能为空。");
            }

            return scales.Distinct().OrderBy(v => v).ToList();
        }

        private static int ReadInt(TextBox textBox, string name, int? minValue = null)
        {
            if (!int.TryParse(textBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new InvalidOperationException($"{name} 必须是整数。");
            }

            if (minValue.HasValue && value < minValue.Value)
            {
                throw new InvalidOperationException($"{name} 不能小于 {minValue.Value}。");
            }

            return value;
        }

        private static double ReadDouble(TextBox textBox, string name, double? minValue = null)
        {
            if (!double.TryParse(textBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new InvalidOperationException($"{name} 必须是数字。");
            }

            if (minValue.HasValue && value < minValue.Value)
            {
                throw new InvalidOperationException($"{name} 不能小于 {minValue.Value.ToString(CultureInfo.InvariantCulture)}。");
            }

            return value;
        }

        private static SurfaceDefectNativeResult RunNative(HImage image, RoiRect roi, string configJson)
        {
            IntPtr resultPtr = IntPtr.Zero;
            try
            {
                int length = OpenCVMediaHelper.M_DetectSurfaceDefects(image, roi, configJson, out resultPtr);
                if (length <= 0 || resultPtr == IntPtr.Zero)
                {
                    if (resultPtr != IntPtr.Zero)
                    {
                        _ = OpenCVMediaHelper.FreeResult(resultPtr);
                    }

                    throw new InvalidOperationException($"表面缺陷/Mura 检测失败，返回码: {length}。{DescribeReturnCode(length)}");
                }

                string json = OpenCVMediaHelper.PtrToStringAnsiAndFree(resultPtr);
                resultPtr = IntPtr.Zero;
                SurfaceDefectNativeResult? result = JsonConvert.DeserializeObject<SurfaceDefectNativeResult>(json);
                if (result == null)
                {
                    throw new InvalidOperationException("表面缺陷/Mura 检测结果解析失败。");
                }

                result.RawJson = json;
                return result;
            }
            finally
            {
                if (resultPtr != IntPtr.Zero)
                {
                    _ = OpenCVMediaHelper.FreeResult(resultPtr);
                }
            }
        }

        private void ApplyResult(SurfaceDefectNativeResult result)
        {
            _result = result;
            _rawJson = FormatJson(result.RawJson);

            HeaderSummaryText.Text = BuildHeaderSummary(result);
            StatusText.Text = BuildStatusText(result);
            SummaryGrid.ItemsSource = BuildSummaryRows(result);
            DefectsGrid.ItemsSource = result.Defects;
            JsonText.Text = _rawJson;

            if (DrawOverlayCheckBox.IsChecked == true)
            {
                if (AutoClearOverlayCheckBox.IsChecked == true)
                {
                    ClearOverlay();
                }

                SurfaceDefectOverlayVisual overlay = new(result.Defects);
                overlay.ApplyLayoutScale(new DrawingVisualScaleContext(_drawContext.DrawCanvas.IsLayoutUpdated, _drawContext.DrawCanvas.Scale, _drawContext.DrawCanvas.TextFontSizeOverride));
                _drawContext.DrawCanvas.AddOverlayVisual(overlay);
                _overlayVisuals.Add(overlay);
            }
        }

        private void ClearOverlay()
        {
            foreach (SurfaceDefectOverlayVisual overlay in _overlayVisuals.ToArray())
            {
                _drawContext.DrawCanvas.RemoveOverlayVisual(overlay);
            }

            _overlayVisuals.Clear();
            StatusText.Text = _result == null ? "Overlay 已清除。" : $"{BuildStatusText(_result)}    Overlay 已清除。";
        }

        private string BuildRoiText()
        {
            if (_imageContext.HImageCache is HImage image)
            {
                RoiRect roi = NormalizeRoi(_roi, image);
                return $"ROI: X={roi.X}, Y={roi.Y}, W={roi.Width}, H={roi.Height}    Image: {image.cols} x {image.rows}";
            }

            return "ROI: 当前图像未初始化";
        }

        private static RoiRect NormalizeRoi(RoiRect roi, HImage image)
        {
            if (roi.Width > 0 && roi.Height > 0)
            {
                return roi;
            }

            return new RoiRect(0, 0, image.cols, image.rows);
        }

        private static string BuildHeaderSummary(SurfaceDefectNativeResult result)
        {
            SurfaceDefectSummary? summary = result.Summary;
            if (summary == null)
            {
                return $"缺陷: {result.Count}";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "缺陷: {0}    暗: {1}    亮: {2}    Max Severity: {3:F4}    Grade: {4}",
                summary.DefectCount,
                summary.DarkCount,
                summary.BrightCount,
                summary.MaxSeverity,
                summary.Grade);
        }

        private static string BuildStatusText(SurfaceDefectNativeResult result)
        {
            string status = result.Success ? "OK" : "NG";
            string message = string.IsNullOrWhiteSpace(result.Message) ? string.Empty : $"    {result.Message}";
            return $"状态: {status}    StatusCode: {result.StatusCode}{message}";
        }

        private static List<SurfaceDefectSummaryRow> BuildSummaryRows(SurfaceDefectNativeResult result)
        {
            List<SurfaceDefectSummaryRow> rows = new()
            {
                Row("Status", result.Success ? "OK" : "NG", result.StatusCode ?? string.Empty),
                Row("Count", result.Count.ToString(CultureInfo.InvariantCulture), "输出缺陷数量")
            };

            if (result.Summary != null)
            {
                rows.Add(Row("Dark Count", result.Summary.DarkCount.ToString(CultureInfo.InvariantCulture), "暗缺陷数量"));
                rows.Add(Row("Bright Count", result.Summary.BrightCount.ToString(CultureInfo.InvariantCulture), "亮缺陷数量"));
                rows.Add(Row("Max Severity", result.Summary.MaxSeverity.ToString("F5", CultureInfo.InvariantCulture), "最大严重度"));
                rows.Add(Row("Mean Severity", result.Summary.MeanSeverity.ToString("F5", CultureInfo.InvariantCulture), "平均严重度"));
                rows.Add(Row("Grade", result.Summary.Grade ?? string.Empty, "综合等级"));
            }

            if (result.Image != null)
            {
                rows.Add(Row("Image", $"{result.Image.Width} x {result.Image.Height}", "输入图像尺寸"));
                if (result.Image.Roi != null)
                {
                    rows.Add(Row("ROI", result.Image.Roi.Display, "native 实际计算区域"));
                }
            }

            return rows;
        }

        private static SurfaceDefectSummaryRow Row(string name, string value, string description) => new(name, value, description);

        private static string FormatJson(string json)
        {
            try
            {
                return JToken.Parse(json).ToString(Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }

        private static string DescribeReturnCode(int code) => code switch
        {
            -1 => "参数或图像无效。",
            -4 => "配置 JSON 无效。",
            -5 => "OpenCV 计算异常。",
            -6 => "native 标准异常。",
            -7 => "native 未知异常。",
            _ => "请检查图像、ROI 和参数设置。"
        };
    }

    public sealed class SurfaceDefectOverlayVisual : DrawingVisual, ILayoutScaleDrawingVisual
    {
        private readonly IReadOnlyList<SurfaceDefectItem> _defects;
        private double _scale = 1.0;

        public SurfaceDefectOverlayVisual(IReadOnlyList<SurfaceDefectItem> defects)
        {
            _defects = defects;
            Render();
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            _scale = context.IsLayoutUpdated ? context.Scale : Math.Max(context.TextFontSizeOverride / 10.0, 0.5);
            if (double.IsNaN(_scale) || double.IsInfinity(_scale) || _scale <= 0)
            {
                _scale = 1.0;
            }

            Render();
        }

        private void Render()
        {
            using DrawingContext dc = RenderOpen();
            double stroke = Math.Max(_scale, 0.5);
            double fontSize = Math.Max(11.0 * _scale, 7.0);
            double padding = Math.Max(3.0 * _scale, 2.0);
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (SurfaceDefectItem defect in _defects)
            {
                Rect rect = new(defect.X, defect.Y, Math.Max(defect.W, 1), Math.Max(defect.H, 1));
                SolidColorBrush brush = GetBrush(defect);
                Pen pen = new(brush, GetStroke(defect, stroke));
                if (string.Equals(defect.Polarity, "dark", StringComparison.OrdinalIgnoreCase))
                {
                    pen.DashStyle = DashStyles.Dash;
                }

                dc.DrawRectangle(null, pen, rect);
                DrawLabel(dc, defect, rect, brush, fontSize, padding, pixelsPerDip);
            }
        }

        private static double GetStroke(SurfaceDefectItem defect, double stroke)
        {
            return defect.Grade switch
            {
                "critical" => stroke * 2.0,
                "major" => stroke * 1.7,
                "minor" => stroke * 1.3,
                _ => stroke
            };
        }

        private static SolidColorBrush GetBrush(SurfaceDefectItem defect)
        {
            return defect.Grade switch
            {
                "critical" => Brushes.Red,
                "major" => Brushes.OrangeRed,
                "minor" => Brushes.Gold,
                _ when string.Equals(defect.Polarity, "dark", StringComparison.OrdinalIgnoreCase) => Brushes.DeepSkyBlue,
                _ => Brushes.LawnGreen
            };
        }

        private static void DrawLabel(DrawingContext dc, SurfaceDefectItem defect, Rect rect, SolidColorBrush brush, double fontSize, double padding, double pixelsPerDip)
        {
            string text = $"{defect.Id}:{defect.Type}/{defect.Grade} {defect.Severity.ToString("F2", CultureInfo.InvariantCulture)}";
            FormattedText formattedText = new(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                fontSize,
                Brushes.White,
                pixelsPerDip);

            double x = rect.Left;
            double y = Math.Max(0, rect.Top - formattedText.Height - padding * 2);
            Rect background = new(x, y, formattedText.Width + padding * 2, formattedText.Height + padding * 2);
            SolidColorBrush backgroundBrush = new(Color.FromArgb(205, brush.Color.R, brush.Color.G, brush.Color.B));
            dc.DrawRoundedRectangle(backgroundBrush, null, background, 2 * padding, 2 * padding);
            dc.DrawText(formattedText, new Point(x + padding, y + padding));
        }
    }

    public sealed class SurfaceDefectConfig
    {
        [JsonProperty("channel")]
        public int Channel { get; set; } = -1;

        [JsonProperty("scales")]
        public List<int> Scales { get; set; } = new() { 31, 61, 121 };

        [JsonProperty("darkThreshold")]
        public double DarkThreshold { get; set; } = 1.5;

        [JsonProperty("brightThreshold")]
        public double BrightThreshold { get; set; } = 1.5;

        [JsonProperty("minArea")]
        public int MinArea { get; set; } = 8;

        [JsonProperty("maxArea")]
        public int MaxArea { get; set; } = 200000;

        [JsonProperty("muraMinArea")]
        public int MuraMinArea { get; set; } = 1000;

        [JsonProperty("openKernel")]
        public int OpenKernel { get; set; } = 1;

        [JsonProperty("closeKernel")]
        public int CloseKernel { get; set; } = 3;

        [JsonProperty("mergeDistance")]
        public int MergeDistance { get; set; } = 3;

        [JsonProperty("maxDefects")]
        public int MaxDefects { get; set; } = 1000;

        [JsonProperty("enableDark")]
        public bool EnableDark { get; set; } = true;

        [JsonProperty("enableBright")]
        public bool EnableBright { get; set; } = true;

        [JsonProperty("enableLineDetect")]
        public bool EnableLineDetect { get; set; } = true;

        [JsonProperty("lineAspectRatio")]
        public double LineAspectRatio { get; set; } = 8.0;

        [JsonProperty("minSeverity")]
        public double MinSeverity { get; set; }

        [JsonProperty("minorSeverity")]
        public double MinorSeverity { get; set; } = 0.25;

        [JsonProperty("majorSeverity")]
        public double MajorSeverity { get; set; } = 1.0;

        [JsonProperty("criticalSeverity")]
        public double CriticalSeverity { get; set; } = 3.0;
    }

    public sealed class SurfaceDefectNativeResult
    {
        [JsonProperty("algorithm")]
        public string? Algorithm { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("statusCode")]
        public string? StatusCode { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("image")]
        public SurfaceDefectImageInfo? Image { get; set; }

        [JsonProperty("summary")]
        public SurfaceDefectSummary? Summary { get; set; }

        [JsonProperty("defects")]
        public List<SurfaceDefectItem> Defects { get; set; } = new();

        [JsonIgnore]
        public string RawJson { get; set; } = string.Empty;
    }

    public sealed class SurfaceDefectImageInfo
    {
        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("roi")]
        public SurfaceDefectRect? Roi { get; set; }
    }

    public sealed class SurfaceDefectSummary
    {
        [JsonProperty("defectCount")]
        public int DefectCount { get; set; }

        [JsonProperty("darkCount")]
        public int DarkCount { get; set; }

        [JsonProperty("brightCount")]
        public int BrightCount { get; set; }

        [JsonProperty("maxSeverity")]
        public double MaxSeverity { get; set; }

        [JsonProperty("meanSeverity")]
        public double MeanSeverity { get; set; }

        [JsonProperty("grade")]
        public string? Grade { get; set; }
    }

    public sealed class SurfaceDefectItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("polarity")]
        public string? Polarity { get; set; }

        [JsonProperty("grade")]
        public string? Grade { get; set; }

        [JsonProperty("scale")]
        public int Scale { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }

        [JsonProperty("w")]
        public int W { get; set; }

        [JsonProperty("h")]
        public int H { get; set; }

        [JsonProperty("centerX")]
        public double CenterX { get; set; }

        [JsonProperty("centerY")]
        public double CenterY { get; set; }

        [JsonProperty("area")]
        public int Area { get; set; }

        [JsonProperty("meanDelta")]
        public double MeanDelta { get; set; }

        [JsonProperty("minDelta")]
        public double MinDelta { get; set; }

        [JsonProperty("maxDelta")]
        public double MaxDelta { get; set; }

        [JsonProperty("maxDeltaAbs")]
        public double MaxDeltaAbs { get; set; }

        [JsonProperty("severity")]
        public double Severity { get; set; }

        [JsonProperty("aspectRatio")]
        public double AspectRatio { get; set; }

        [JsonProperty("fillRatio")]
        public double FillRatio { get; set; }

        [JsonProperty("boundingRect")]
        public SurfaceDefectRect? BoundingRect { get; set; }

        [JsonIgnore]
        public string RectDisplay => BoundingRect?.Display ?? $"X:{X} Y:{Y} W:{W} H:{H}";
    }

    public sealed class SurfaceDefectRect
    {
        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }

        [JsonProperty("w")]
        public int W { get; set; }

        [JsonProperty("h")]
        public int H { get; set; }

        [JsonIgnore]
        public string Display => $"X:{X} Y:{Y} W:{W} H:{H}";
    }

    public sealed record SurfaceDefectSummaryRow(string Name, string Value, string Description);
}
