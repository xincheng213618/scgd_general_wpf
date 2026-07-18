using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.P2
{
    internal delegate int P2NativeJsonCall(out IntPtr result);

    internal sealed record P2NativeResult(JObject Json, string RawJson);

    internal static class P2NativeJson
    {
        public static P2NativeResult Invoke(string operation, P2NativeJsonCall call)
        {
            IntPtr resultPtr = IntPtr.Zero;
            try
            {
                int length = call(out resultPtr);
                if (length <= 0 || resultPtr == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"{operation}失败，返回码: {length}。{DescribeReturnCode(length)}");
                }

                string rawJson = OpenCVMediaHelper.PtrToStringUtf8AndFree(resultPtr);
                resultPtr = IntPtr.Zero;
                if (JToken.Parse(rawJson) is not JObject json)
                {
                    throw new InvalidOperationException($"{operation}返回的 JSON 不是对象。");
                }

                return new P2NativeResult(json, rawJson);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"{operation}结果 JSON 解析失败。", ex);
            }
            finally
            {
                if (resultPtr != IntPtr.Zero)
                {
                    _ = OpenCVMediaHelper.FreeResult(resultPtr);
                }
            }
        }

        public static string Format(string json)
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
            -1 => "参数、图像或结果指针无效。",
            -3 => "结果内存分配失败。",
            -4 => "配置 JSON 无效。",
            -5 => "OpenCV 计算异常。",
            -6 => "Native 标准异常。",
            -7 => "Native 未知异常。",
            _ => "请检查输入图像、ROI 和配置参数。"
        };
    }

    internal sealed class P2ImageSnapshot : IDisposable
    {
        private HImage _image;

        private P2ImageSnapshot(HImage image)
        {
            _image = image;
        }

        public HImage Image => _image;

        public static P2ImageSnapshot Copy(HImage source)
        {
            if (source.pData == IntPtr.Zero || source.rows <= 0 || source.cols <= 0 || source.channels <= 0 ||
                source.depth <= 0 || source.depth % 8 != 0)
            {
                throw new InvalidOperationException("当前图像缓存无效。");
            }

            int rowBytes = checked(source.cols * source.channels * (source.depth / 8));
            int sourceStride = source.stride > 0 ? source.stride : rowBytes;
            if (sourceStride < rowBytes)
            {
                throw new InvalidOperationException("当前图像缓存步长无效。");
            }

            int totalBytes = checked(rowBytes * source.rows);
            IntPtr buffer = Marshal.AllocCoTaskMem(totalBytes);
            try
            {
                byte[] row = new byte[rowBytes];
                for (int y = 0; y < source.rows; ++y)
                {
                    Marshal.Copy(IntPtr.Add(source.pData, checked(y * sourceStride)), row, 0, rowBytes);
                    Marshal.Copy(row, 0, IntPtr.Add(buffer, checked(y * rowBytes)), rowBytes);
                }

                HImage copy = new()
                {
                    rows = source.rows,
                    cols = source.cols,
                    channels = source.channels,
                    depth = source.depth,
                    stride = rowBytes,
                    isDispose = false,
                    pData = buffer
                };
                buffer = IntPtr.Zero;
                return new P2ImageSnapshot(copy);
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(buffer);
                }
            }
        }

        public static P2ImageSnapshot FromBitmap(BitmapSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            WriteableBitmap bitmap = source as WriteableBitmap ?? new WriteableBitmap(source);
            return new P2ImageSnapshot(bitmap.ToHImage());
        }

        public void Dispose()
        {
            _image.Dispose();
            _image = default;
        }
    }

    internal static class P2BitmapLoader
    {
        private static readonly HashSet<string> SupportedFormats = new(StringComparer.Ordinal)
        {
            "Bgr32", "Bgra32", "Pbgra32", "Bgr24", "Rgb24", "Indexed8", "Rgb48", "Gray8", "Gray16", "Gray32Float"
        };

        public static BitmapSource Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("图像文件不存在。", filePath);
            }

            using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                throw new InvalidOperationException("图像文件没有可读取的帧。");
            }

            BitmapSource source = decoder.Frames[0];
            if (!SupportedFormats.Contains(source.Format.ToString()))
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0.0);
            }

            WriteableBitmap bitmap = new(source);
            bitmap.Freeze();
            return bitmap;
        }
    }

    internal static class P2RoiHelper
    {
        public static RoiRect WholeImage(HImage image) => new(0, 0, image.cols, image.rows);

        public static RoiRect Normalize(RoiRect roi, HImage image)
        {
            return roi.Width > 0 && roi.Height > 0 ? roi : WholeImage(image);
        }

        public static bool TryFromRectangle(IRectangle rectangle, HImage image, ImageViewConfig config, out RoiRect roi)
        {
            roi = default;
            double dpiScaleX = config.GetProperties<double>("DpiX") / 96.0;
            double dpiScaleY = config.GetProperties<double>("DpiY") / 96.0;
            if (!double.IsFinite(dpiScaleX) || dpiScaleX <= 0.0) dpiScaleX = 1.0;
            if (!double.IsFinite(dpiScaleY) || dpiScaleY <= 0.0) dpiScaleY = 1.0;

            int x = (int)Math.Round(rectangle.Rect.X * dpiScaleX);
            int y = (int)Math.Round(rectangle.Rect.Y * dpiScaleY);
            int width = (int)Math.Round(rectangle.Rect.Width * dpiScaleX);
            int height = (int)Math.Round(rectangle.Rect.Height * dpiScaleY);
            int left = Math.Max(0, x);
            int top = Math.Max(0, y);
            int right = Math.Min(image.cols, checked(x + width));
            int bottom = Math.Min(image.rows, checked(y + height));
            if (right <= left || bottom <= top)
            {
                return false;
            }

            roi = new RoiRect(left, top, right - left, bottom - top);
            return true;
        }

        public static BitmapSource CropCurrentBitmap(ImageProcessingContext context, RoiRect roi)
        {
            if (context.ImageShow.Source is not BitmapSource source)
            {
                throw new InvalidOperationException("当前显示图像不能作为模板。");
            }

            Int32Rect crop = new(roi.X, roi.Y, roi.Width, roi.Height);
            if (crop.X < 0 || crop.Y < 0 || crop.Width <= 0 || crop.Height <= 0 ||
                crop.X + crop.Width > source.PixelWidth || crop.Y + crop.Height > source.PixelHeight)
            {
                throw new InvalidOperationException("模板 ROI 超出当前显示图像范围。");
            }

            WriteableBitmap bitmap = new(new CroppedBitmap(source, crop));
            bitmap.Freeze();
            return bitmap;
        }

        public static string Describe(RoiRect roi) => $"X={roi.X}, Y={roi.Y}, W={roi.Width}, H={roi.Height}";
    }

    internal sealed record P2MetricRow(string Name, string Value);

    internal static class P2ResultRows
    {
        public static IReadOnlyList<P2MetricRow> Build(JObject result)
        {
            List<P2MetricRow> rows = new();
            AddPrimitiveRows(rows, result, string.Empty);
            if (result["summary"] is JObject summary)
            {
                AddPrimitiveRows(rows, summary, "summary.");
            }
            return rows;
        }

        private static void AddPrimitiveRows(List<P2MetricRow> rows, JObject source, string prefix)
        {
            foreach (JProperty property in source.Properties())
            {
                if (property.Value.Type is JTokenType.Object or JTokenType.Array or JTokenType.Null)
                {
                    continue;
                }

                rows.Add(new P2MetricRow(prefix + property.Name, Convert.ToString(((JValue)property.Value).Value, CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
    }

    internal enum P2OverlayKind
    {
        Ghost,
        TemplateMatching,
        StereoLeft
    }

    internal sealed class P2ResultOverlayVisual : DrawingVisual, ILayoutScaleDrawingVisual
    {
        private readonly JObject _result;
        private readonly P2OverlayKind _kind;
        private readonly double _pixelToDipX;
        private readonly double _pixelToDipY;
        private double _layoutScale = 1.0;

        public P2ResultOverlayVisual(JObject result, P2OverlayKind kind, ImageViewConfig config)
        {
            _result = result;
            _kind = kind;
            _pixelToDipX = SafePixelToDip(config.GetProperties<double>("DpiX"));
            _pixelToDipY = SafePixelToDip(config.GetProperties<double>("DpiY"));
            Render();
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            _layoutScale = context.IsLayoutUpdated ? context.Scale : Math.Max(context.TextFontSizeOverride / 10.0, 0.5);
            if (!double.IsFinite(_layoutScale) || _layoutScale <= 0.0) _layoutScale = 1.0;
            Render();
        }

        private static double SafePixelToDip(double dpi) => double.IsFinite(dpi) && dpi > 0.0 ? 96.0 / dpi : 1.0;

        private Point Point(JToken? token)
        {
            return new Point(token?.Value<double?>("x") * _pixelToDipX ?? 0.0, token?.Value<double?>("y") * _pixelToDipY ?? 0.0);
        }

        private Rect Rect(JToken? token)
        {
            return new Rect(
                token?.Value<double?>("x") * _pixelToDipX ?? 0.0,
                token?.Value<double?>("y") * _pixelToDipY ?? 0.0,
                Math.Max(1.0, token?.Value<double?>("width") * _pixelToDipX ?? 1.0),
                Math.Max(1.0, token?.Value<double?>("height") * _pixelToDipY ?? 1.0));
        }

        private void Render()
        {
            using DrawingContext dc = RenderOpen();
            switch (_kind)
            {
                case P2OverlayKind.Ghost:
                    DrawGhost(dc);
                    break;
                case P2OverlayKind.TemplateMatching:
                    DrawMatches(dc);
                    break;
                case P2OverlayKind.StereoLeft:
                    DrawStereo(dc);
                    break;
            }
        }

        private void DrawGhost(DrawingContext dc)
        {
            Dictionary<int, Point> sourceCenters = new();
            foreach (JObject source in _result["brightSources"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                int id = source.Value<int?>("id") ?? 0;
                Point center = Point(source["center"]);
                sourceCenters[id] = center;
                dc.DrawRectangle(null, Pen(Brushes.DeepSkyBlue, 1.2), Rect(source["boundingRect"]));
                dc.DrawEllipse(null, Pen(Brushes.DeepSkyBlue, 1.2), center, Radius(5), Radius(5));
                DrawLabel(dc, center, $"S{id}", Brushes.DeepSkyBlue);
            }

            foreach (JObject candidate in _result["candidates"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                double confidence = candidate.Value<double?>("confidence") ?? 0.0;
                Brush brush = confidence >= 0.75 ? Brushes.LawnGreen : confidence >= 0.5 ? Brushes.Gold : Brushes.OrangeRed;
                Point center = Point(candidate["center"]);
                dc.DrawRectangle(null, Pen(brush, 1.5), Rect(candidate["boundingRect"]));
                dc.DrawEllipse(null, Pen(brush, 1.5), center, Radius(5), Radius(5));
                int sourceId = candidate.Value<int?>("nearestBrightSourceId") ?? 0;
                if (sourceCenters.TryGetValue(sourceId, out Point sourceCenter))
                {
                    dc.DrawLine(Pen(brush, 0.8, DashStyles.Dash), sourceCenter, center);
                }
                DrawLabel(dc, center, $"G{candidate.Value<int?>("id") ?? 0} C={confidence:F2}", brush);
            }
        }

        private void DrawMatches(DrawingContext dc)
        {
            int index = 0;
            foreach (JObject match in _result["matches"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                ++index;
                Brush brush = index == 1 ? Brushes.LawnGreen : Brushes.Gold;
                List<Point> corners = match["corners"]?.Select(Point).ToList() ?? new List<Point>();
                for (int i = 0; i < corners.Count; ++i)
                {
                    dc.DrawLine(Pen(brush, index == 1 ? 1.8 : 1.2), corners[i], corners[(i + 1) % corners.Count]);
                }
                Point center = Point(match["center"]);
                dc.DrawEllipse(null, Pen(brush, 1.2), center, Radius(4), Radius(4));
                DrawLabel(dc, center, string.Format(
                    CultureInfo.InvariantCulture,
                    "M{0} A={1:F1}° S={2:F2} C={3:F3}",
                    index,
                    match.Value<double?>("angleDegrees") ?? 0.0,
                    match.Value<double?>("scale") ?? 1.0,
                    match.Value<double?>("score") ?? 0.0), brush);
            }
        }

        private void DrawStereo(DrawingContext dc)
        {
            foreach (JObject point in _result["points"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                bool valid = point.Value<bool?>("valid") == true;
                Brush brush = valid ? Brushes.LawnGreen : Brushes.OrangeRed;
                Point center = Point(point["leftPoint"]);
                double depth = point["pointMm"]?.Value<double?>("z") ?? 0.0;
                dc.DrawEllipse(null, Pen(brush, 1.5), center, Radius(6), Radius(6));
                dc.DrawLine(Pen(brush, 1.0), new Point(center.X - Radius(8), center.Y), new Point(center.X + Radius(8), center.Y));
                dc.DrawLine(Pen(brush, 1.0), new Point(center.X, center.Y - Radius(8)), new Point(center.X, center.Y + Radius(8)));
                DrawLabel(dc, center, $"{point.Value<string>("role")} Z={depth:F1}mm", brush);
            }
        }

        private Pen Pen(Brush brush, double thickness, DashStyle? dashStyle = null)
        {
            Pen pen = new(brush, Math.Max(0.5, thickness * _layoutScale));
            if (dashStyle != null) pen.DashStyle = dashStyle;
            return pen;
        }

        private double Radius(double value) => Math.Max(2.0, value * _layoutScale);

        private void DrawLabel(DrawingContext dc, Point anchor, string text, Brush brush)
        {
            double fontSize = Math.Max(8.0, 11.0 * _layoutScale);
            double padding = Math.Max(2.0, 3.0 * _layoutScale);
            FormattedText formatted = new(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                fontSize,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            Point origin = new(anchor.X + padding, Math.Max(0.0, anchor.Y - formatted.Height - padding * 2));
            Color color = (brush as SolidColorBrush)?.Color ?? Colors.DimGray;
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromArgb(205, color.R, color.G, color.B)),
                null,
                new Rect(origin.X, origin.Y, formatted.Width + padding * 2, formatted.Height + padding * 2),
                padding,
                padding);
            dc.DrawText(formatted, new Point(origin.X + padding, origin.Y + padding));
        }
    }
}
