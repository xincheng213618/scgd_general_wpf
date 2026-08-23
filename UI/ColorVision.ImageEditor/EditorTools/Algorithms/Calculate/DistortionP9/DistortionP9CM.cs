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
            DistortionP9AnalysisRunner.Run(
                _imageContext,
                new RoiRect(),
                _drawContext);
        }
    }

    internal static class DistortionP9AnalysisRunner
    {
        public static void Run(ImageProcessingContext imageContext, RoiRect requestedRoi, DrawEditorContext drawContext)
        {
            ImageFrameLease? lease = imageContext.AcquireImageFrame();
            if (lease == null)
            {
                return;
            }

            HImage image = lease.Image;
            if (!TryNormalizeRoi(requestedRoi, image, out RoiRect roi))
            {
                lease.Dispose();
                return;
            }

            long revision = lease.Revision;
            _ = Task.Run(() =>
            {
                IntPtr resultPtr = IntPtr.Zero;
                try
                {
                    int length;
                    using (lease)
                    {
                        length = OpenCVMediaHelper.M_CalDistortionP9(lease.Image, roi, CreateDefaultConfigJson(), out resultPtr);
                    }

                    if (length <= 0 || resultPtr == IntPtr.Zero)
                    {
                        if (resultPtr != IntPtr.Zero)
                        {
                            _ = OpenCVMediaHelper.FreeResult(resultPtr);
                            resultPtr = IntPtr.Zero;
                        }

                        imageContext.Dispatcher.BeginInvoke(() =>
                        {
                            if (!imageContext.IsCurrentImageRevision(revision)) return;

                            MessageBox.Show(
                                $"9点畸变计算失败，返回码: {length}\n{DescribeReturnCode(length)}",
                                "9点畸变",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                        return;
                    }

                    IntPtr ownedResult = resultPtr;
                    resultPtr = IntPtr.Zero;
                    string json = OpenCVMediaHelper.PtrToStringAnsiAndFree(ownedResult);
                    DistortionP9NativeResult? result = JsonConvert.DeserializeObject<DistortionP9NativeResult>(json);
                    if (result == null)
                    {
                        imageContext.Dispatcher.BeginInvoke(() =>
                        {
                            if (!imageContext.IsCurrentImageRevision(revision)) return;

                            MessageBox.Show("9点畸变结果解析失败。", "9点畸变", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        return;
                    }

                    result.RawJson = json;
                    imageContext.Dispatcher.BeginInvoke(() =>
                    {
                        if (!imageContext.IsCurrentImageRevision(revision)) return;

                        ShowResult(result, drawContext);
                    });
                }
                catch (Exception ex)
                {
                    lease.Dispose();
                    if (resultPtr != IntPtr.Zero)
                    {
                        _ = OpenCVMediaHelper.FreeResult(resultPtr);
                    }

                    imageContext.Dispatcher.BeginInvoke(() =>
                    {
                        if (!imageContext.IsCurrentImageRevision(revision)) return;

                        MessageBox.Show($"9点畸变计算异常: {ex.Message}", "9点畸变", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        private static bool TryNormalizeRoi(RoiRect requestedRoi, HImage image, out RoiRect roi)
        {
            roi = new RoiRect();
            if (image.cols <= 0 || image.rows <= 0)
                return false;

            if (requestedRoi.Width <= 0 || requestedRoi.Height <= 0)
            {
                roi = new RoiRect(0, 0, image.cols, image.rows);
                return true;
            }

            long left = Math.Max(0L, requestedRoi.X);
            long top = Math.Max(0L, requestedRoi.Y);
            long right = Math.Min((long)image.cols, (long)requestedRoi.X + requestedRoi.Width);
            long bottom = Math.Min((long)image.rows, (long)requestedRoi.Y + requestedRoi.Height);
            if (right <= left || bottom <= top)
                return false;

            roi = new RoiRect((int)left, (int)top, (int)(right - left), (int)(bottom - top));
            return true;
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
            double scale = double.IsFinite(zoom) && zoom > 0 ? 1.0 / zoom : 1.0;
            if (!double.IsFinite(scale) || !double.IsFinite(20.0 * scale))
                scale = 1.0;

            double stroke = Math.Max(scale, 0.5);
            double radius = Math.Max(20.0 * scale, 4.0);
            Pen linePen = new(Brushes.DeepSkyBlue, stroke);
            Pen circlePen = new(Brushes.OrangeRed, stroke * 1.5);
            Pen candidatePen = new(Brushes.Gold, stroke * 1.2);

            foreach (DistortionP9Point point in result.CandidatePoints)
            {
                if (!HasFiniteCoordinates(point) || IsSelectedPoint(point, result.Points))
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
                if (HasFiniteCoordinates(point))
                    AddCircle(drawContext, point, radius, circlePen, Brushes.OrangeRed);
            }
        }

        private static bool HasFiniteCoordinates(DistortionP9Point? point)
        {
            return point != null && double.IsFinite(point.X) && double.IsFinite(point.Y);
        }

        private static bool IsSelectedPoint(DistortionP9Point candidate, IReadOnlyCollection<DistortionP9Point> selectedPoints)
        {
            const double tolerance = 1.0;
            return selectedPoints.Any(point => HasFiniteCoordinates(point) &&
                Math.Abs(point.X - candidate.X) <= tolerance &&
                Math.Abs(point.Y - candidate.Y) <= tolerance);
        }

        private static void AddCircle(DrawEditorContext drawContext, DistortionP9Point point, double radius, Pen pen, Brush textBrush)
        {
            if (!HasFiniteCoordinates(point))
                return;

            DVCircleText circle = new(new CircleTextProperties
            {
                Center = new Point(point.X, point.Y),
                Radius = radius,
                Brush = Brushes.Transparent,
                Pen = pen,
                Text = point.Name ?? point.Id.ToString(),
                Foreground = textBrush,
            });
            circle.TextAttribute.FontSize = 20;
            circle.Render();
            drawContext.DrawCanvas.AddVisualCommand(circle);
        }

        private static DistortionP9Point?[,] BuildGrid(IEnumerable<DistortionP9Point> points)
        {
            DistortionP9Point?[,] grid = new DistortionP9Point?[3, 3];
            foreach (DistortionP9Point point in points)
            {
                if (HasFiniteCoordinates(point) && point.Row >= 0 && point.Row < 3 && point.Col >= 0 && point.Col < 3)
                {
                    grid[point.Row, point.Col] = point;
                }
            }

            return grid;
        }

        private static void AddLine(DrawEditorContext drawContext, Pen pen, params DistortionP9Point?[] points)
        {
            DistortionP9Point[] validPoints = points.Where(HasFiniteCoordinates).Cast<DistortionP9Point>().ToArray();
            if (validPoints.Length < 2)
            {
                return;
            }

            DVLine line = new(new LineProperties
            {
                Pen = pen.CloneCurrentValue(),
                Points = validPoints.Select(point => new Point(point.X, point.Y)).ToList(),
            });
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
            if (obj is not IRectangle rectangle)
            {
                return menuItems;
            }

            using ImageFrameLease? lease = _imageContext.AcquireImageFrame();
            if (lease == null) return menuItems;
            HImage hImage = lease.Image;

            if (!TryBuildRoi(rectangle, hImage, out RoiRect roi))
            {
                return menuItems;
            }

            MenuItem item = new() { Header = "9点畸变分析" };
            item.Click += (_, _) => DistortionP9AnalysisRunner.Run(_imageContext, roi, _drawContext);
            menuItems.Add(item);
            return menuItems;
        }

        private bool TryBuildRoi(IRectangle rectangle, HImage image, out RoiRect roi)
        {
            roi = new RoiRect();

            double dpiX = _config.GetProperties<double>("DpiX");
            double dpiY = _config.GetProperties<double>("DpiY");
            if (!double.IsFinite(dpiX) || dpiX <= 0 || !double.IsFinite(dpiY) || dpiY <= 0
                || image.cols <= 0 || image.rows <= 0)
            {
                return false;
            }

            double dpiScaleX = dpiX / 96.0;
            double dpiScaleY = dpiY / 96.0;
            Rect rect = rectangle.Rect;
            if (!TryRoundToInt(rect.X * dpiScaleX, out int x)
                || !TryRoundToInt(rect.Y * dpiScaleY, out int y)
                || !TryRoundToInt(rect.Width * dpiScaleX, out int w)
                || !TryRoundToInt(rect.Height * dpiScaleY, out int h)
                || w <= 0
                || h <= 0)
            {
                return false;
            }

            long roiX = Math.Max(0L, x);
            long roiY = Math.Max(0L, y);
            long roiX2 = Math.Min((long)image.cols, (long)x + w);
            long roiY2 = Math.Min((long)image.rows, (long)y + h);
            long roiW = roiX2 - roiX;
            long roiH = roiY2 - roiY;

            if (roiW <= 0 || roiH <= 0)
            {
                return false;
            }

            roi = new RoiRect((int)roiX, (int)roiY, (int)roiW, (int)roiH);
            return true;
        }

        private static bool TryRoundToInt(double value, out int result)
        {
            double rounded = Math.Round(value);
            if (!double.IsFinite(rounded) || rounded < int.MinValue || rounded > int.MaxValue)
            {
                result = 0;
                return false;
            }

            result = (int)rounded;
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
