#pragma warning disable CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.DistortionP9
{
    public sealed class DistortionP9EditorTool
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;

        public DistortionP9EditorTool(ImageProcessingContext imageContext, DrawEditorContext drawContext)
        {
            _imageContext = imageContext;
            _drawContext = drawContext;
        }

        public void Execute()
        {
            if (_imageContext.HImageCache is not HImage hImage)
            {
                return;
            }

            DistortionP9AnalysisRunner.Run(
                hImage,
                new RoiRect(0, 0, hImage.cols, hImage.rows),
                _drawContext);
        }
    }

    internal static class DistortionP9AnalysisRunner
    {
        public static void Run(HImage image, RoiRect roi, DrawEditorContext drawContext)
        {
            Task.Run(() =>
            {
                IntPtr resultPtr = IntPtr.Zero;
                try
                {
                    int length = OpenCVMediaHelper.M_CalDistortionP9(image, roi, CreateDefaultConfigJson(), out resultPtr);
                    if (length <= 0 || resultPtr == IntPtr.Zero)
                    {
                        if (resultPtr != IntPtr.Zero)
                        {
                            _ = OpenCVMediaHelper.FreeResult(resultPtr);
                        }

                        Application.Current.Dispatcher.BeginInvoke(() =>
                            MessageBox.Show(
                                $"9点畸变计算失败，返回码: {length}\n{DescribeReturnCode(length)}",
                                "9点畸变",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error));
                        return;
                    }

                    string json = OpenCVMediaHelper.PtrToStringAnsiAndFree(resultPtr);
                    resultPtr = IntPtr.Zero;
                    DistortionP9NativeResult? result = JsonConvert.DeserializeObject<DistortionP9NativeResult>(json);
                    if (result == null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(() =>
                            MessageBox.Show("9点畸变结果解析失败。", "9点畸变", MessageBoxButton.OK, MessageBoxImage.Error));
                        return;
                    }

                    result.RawJson = json;
                    Application.Current.Dispatcher.BeginInvoke(() => ShowResult(result, drawContext));
                }
                catch (Exception ex)
                {
                    if (resultPtr != IntPtr.Zero)
                    {
                        _ = OpenCVMediaHelper.FreeResult(resultPtr);
                    }

                    Application.Current.Dispatcher.BeginInvoke(() =>
                        MessageBox.Show($"9点畸变计算异常: {ex.Message}", "9点畸变", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private static string CreateDefaultConfigJson()
        {
            var config = new
            {
                expectedRows = 3,
                expectedCols = 3,
                threshold = -1,
                brightTarget = true,
                minRectSize = 40,
                maxRectSize = 400,
                erodeKernel = 3,
                erodeIterations = 0,
                tvCalcWay = 0,
                sortWithPca = true
            };

            return JsonConvert.SerializeObject(config);
        }

        private static string DescribeReturnCode(int code) => code switch
        {
            -1 => "参数或图像无效。",
            -2 => "未能找到有效的 3x3 点阵。",
            -4 => "配置 JSON 无效。",
            -5 => "OpenCV 计算异常。",
            -6 => "native 标准异常。",
            -7 => "native 未知异常。",
            _ => "请检查图像、阈值和点尺寸参数。"
        };

        private static void ShowResult(DistortionP9NativeResult result, DrawEditorContext drawContext)
        {
            DrawResultOverlay(result, drawContext);

            DistortionP9ResultWindow window = new(result, result.RawJson)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            window.Show();
        }

        private static void DrawResultOverlay(DistortionP9NativeResult result, DrawEditorContext drawContext)
        {
            double zoom = drawContext.Zoombox.ContentMatrix.M11;
            if (zoom <= 0)
            {
                zoom = 1.0;
            }

            double stroke = Math.Max(1.0 / zoom, 0.5);
            double radius = Math.Max(20.0 / zoom, 4.0);
            Pen linePen = new(Brushes.DeepSkyBlue, stroke);
            Pen circlePen = new(Brushes.OrangeRed, stroke * 1.5);
            Pen candidatePen = new(Brushes.Gold, stroke * 1.2);

            foreach (DistortionP9Point point in result.CandidatePoints)
            {
                if (IsSelectedPoint(point, result.Points))
                {
                    continue;
                }

                AddCircle(drawContext, point, radius * 0.75, candidatePen, Brushes.Gold);
            }

            if (result.Success)
            {
                DistortionP9Point?[,] grid = BuildGrid(result.Points);
                for (int row = 0; row < 3; ++row)
                {
                    AddLine(drawContext, linePen, grid[row, 0], grid[row, 1], grid[row, 2]);
                }

                for (int col = 0; col < 3; ++col)
                {
                    AddLine(drawContext, linePen, grid[0, col], grid[1, col], grid[2, col]);
                }
            }

            foreach (DistortionP9Point point in result.Points.OrderBy(p => p.Id))
            {
                AddCircle(drawContext, point, radius, circlePen, Brushes.OrangeRed);
            }
        }

        private static bool IsSelectedPoint(DistortionP9Point candidate, IReadOnlyCollection<DistortionP9Point> selectedPoints)
        {
            const double tolerance = 1.0;
            return selectedPoints.Any(point =>
                Math.Abs(point.X - candidate.X) <= tolerance &&
                Math.Abs(point.Y - candidate.Y) <= tolerance);
        }

        private static void AddCircle(DrawEditorContext drawContext, DistortionP9Point point, double radius, Pen pen, Brush textBrush)
        {
            DVCircleText circle = new();
            circle.Attribute.Center = new Point(point.X, point.Y);
            circle.Attribute.Radius = radius;
            circle.Attribute.Brush = Brushes.Transparent;
            circle.Attribute.Pen = pen;
            circle.Attribute.Text = point.Name ?? point.Id.ToString();
            circle.Attribute.Foreground = textBrush;
            circle.Render();
            drawContext.DrawCanvas.AddVisualCommand(circle);
        }

        private static DistortionP9Point?[,] BuildGrid(IEnumerable<DistortionP9Point> points)
        {
            DistortionP9Point?[,] grid = new DistortionP9Point?[3, 3];
            foreach (DistortionP9Point point in points)
            {
                if (point.Row >= 0 && point.Row < 3 && point.Col >= 0 && point.Col < 3)
                {
                    grid[point.Row, point.Col] = point;
                }
            }

            return grid;
        }

        private static void AddLine(DrawEditorContext drawContext, Pen pen, params DistortionP9Point?[] points)
        {
            DistortionP9Point[] validPoints = points.Where(p => p != null).Cast<DistortionP9Point>().ToArray();
            if (validPoints.Length < 2)
            {
                return;
            }

            DVLine line = new();
            line.Attribute.Pen = pen;
            foreach (DistortionP9Point point in validPoints)
            {
                line.Attribute.Points.Add(new Point(point.X, point.Y));
            }
            line.Render();
            drawContext.DrawCanvas.AddVisualCommand(line);
        }
    }

    public sealed class DistortionP9IDVContextMenu : IDVContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;

        public DistortionP9IDVContextMenu(ImageProcessingContext imageContext, DrawEditorContext drawContext, ImageViewConfig config)
        {
            _imageContext = imageContext;
            _drawContext = drawContext;
            _config = config;
        }

        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle rectangle || _imageContext.HImageCache is not HImage hImage)
            {
                return menuItems;
            }

            if (!TryBuildRoi(rectangle, hImage, out RoiRect roi))
            {
                return menuItems;
            }

            MenuItem item = new() { Header = "9点畸变分析" };
            item.Click += (_, _) =>
            {
                if (_imageContext.HImageCache is HImage image)
                {
                    DistortionP9AnalysisRunner.Run(image, roi, _drawContext);
                }
            };
            menuItems.Add(item);
            return menuItems;
        }

        private bool TryBuildRoi(IRectangle rectangle, HImage image, out RoiRect roi)
        {
            roi = new RoiRect();

            double dpiScaleX = _config.GetProperties<double>("DpiX") / 96.0;
            double dpiScaleY = _config.GetProperties<double>("DpiY") / 96.0;
            int x = (int)Math.Round(rectangle.Rect.X * dpiScaleX);
            int y = (int)Math.Round(rectangle.Rect.Y * dpiScaleY);
            int w = (int)Math.Round(rectangle.Rect.Width * dpiScaleX);
            int h = (int)Math.Round(rectangle.Rect.Height * dpiScaleY);

            if (w <= 0 || h <= 0)
            {
                return false;
            }

            int roiX = Math.Max(0, x);
            int roiY = Math.Max(0, y);
            int roiX2 = Math.Min(image.cols, x + w);
            int roiY2 = Math.Min(image.rows, y + h);
            int roiW = roiX2 - roiX;
            int roiH = roiY2 - roiY;

            if (roiW <= 0 || roiH <= 0)
            {
                return false;
            }

            roi = new RoiRect(roiX, roiY, roiW, roiH);
            return true;
        }
    }

    public sealed record class CMDistortionP9(ImageProcessingContext ImageContext, DrawEditorContext DrawContext) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RelayCommand command = new(o =>
            {
                DistortionP9EditorTool tool = new(ImageContext, DrawContext);
                tool.Execute();
            });

            return new List<MenuItemMetadata>
            {
                new()
                {
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "DistortionP9",
                    Order = 3,
                    Header = "9点畸变分析",
                    Command = command
                }
            };
        }
    }

    public sealed class DistortionP9NativeResult
    {
        [JsonProperty("algorithm")]
        public string? Algorithm { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("statusCode")]
        public string? StatusCode { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("expectedCount")]
        public int ExpectedCount { get; set; }

        [JsonProperty("selectedCount")]
        public int SelectedCount { get; set; }

        [JsonProperty("candidateCount")]
        public int CandidateCount { get; set; }

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new();

        [JsonProperty("diagnostics")]
        public DistortionP9Diagnostics? Diagnostics { get; set; }

        [JsonProperty("metrics")]
        public DistortionP9Metrics? Metrics { get; set; }

        [JsonProperty("points")]
        public List<DistortionP9Point> Points { get; set; } = new();

        [JsonProperty("candidatePoints")]
        public List<DistortionP9Point> CandidatePoints { get; set; } = new();

        [JsonIgnore]
        public string RawJson { get; set; } = string.Empty;
    }

    public sealed class DistortionP9Diagnostics
    {
        [JsonProperty("expectedPointCount")]
        public int ExpectedPointCount { get; set; }

        [JsonProperty("candidateCount")]
        public int CandidateCount { get; set; }

        [JsonProperty("missingCount")]
        public int MissingCount { get; set; }

        [JsonProperty("extraCount")]
        public int ExtraCount { get; set; }

        [JsonProperty("roiUsed")]
        public bool RoiUsed { get; set; }

        [JsonProperty("canCalculateMetrics")]
        public bool CanCalculateMetrics { get; set; }
    }

    public sealed class DistortionP9Metrics
    {
        [JsonProperty("horizontalTvPercent")]
        public double HorizontalTvPercent { get; set; }

        [JsonProperty("verticalTvPercent")]
        public double VerticalTvPercent { get; set; }

        [JsonProperty("topPercent")]
        public double TopPercent { get; set; }

        [JsonProperty("bottomPercent")]
        public double BottomPercent { get; set; }

        [JsonProperty("leftPercent")]
        public double LeftPercent { get; set; }

        [JsonProperty("rightPercent")]
        public double RightPercent { get; set; }

        [JsonProperty("keystoneHorizontalPercent")]
        public double KeystoneHorizontalPercent { get; set; }

        [JsonProperty("keystoneVerticalPercent")]
        public double KeystoneVerticalPercent { get; set; }

        [JsonProperty("topWidth")]
        public double TopWidth { get; set; }

        [JsonProperty("middleWidth")]
        public double MiddleWidth { get; set; }

        [JsonProperty("bottomWidth")]
        public double BottomWidth { get; set; }

        [JsonProperty("leftHeight")]
        public double LeftHeight { get; set; }

        [JsonProperty("centerHeight")]
        public double CenterHeight { get; set; }

        [JsonProperty("rightHeight")]
        public double RightHeight { get; set; }
    }

    public sealed class DistortionP9Point
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("row")]
        public int Row { get; set; }

        [JsonProperty("col")]
        public int Col { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("area")]
        public int Area { get; set; }

        [JsonProperty("boundingRect")]
        public DistortionP9Rect? BoundingRect { get; set; }

        [JsonIgnore]
        public string BoundingRectDisplay
            => BoundingRect == null
                ? string.Empty
                : $"X:{BoundingRect.X} Y:{BoundingRect.Y} W:{BoundingRect.W} H:{BoundingRect.H}";
    }

    public sealed class DistortionP9Rect
    {
        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }

        [JsonProperty("w")]
        public int W { get; set; }

        [JsonProperty("h")]
        public int H { get; set; }
    }
}
