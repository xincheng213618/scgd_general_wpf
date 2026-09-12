using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.Core
{
    public sealed class GridDistortionOptions
    {
        [Category("图卡"), DisplayName("点阵行数"), Description("完整图卡的行数，必须是 3 到 15 的奇数。7×7 图卡填 7。")]
        public int ExpectedRows { get; set; } = 3;
        [Category("图卡"), DisplayName("点阵列数"), Description("完整图卡的列数，必须是 3 到 15 的奇数。")]
        public int ExpectedCols { get; set; } = 3;
        [Category("图卡"), DisplayName("亮点暗背景")]
        public bool BrightTarget { get; set; } = true;
        [Category("检测"), DisplayName("粗定位最大边长 (px)")]
        public int MaxProcessingSize { get; set; } = 1600;
        [Category("质量"), DisplayName("最小对比度"), Description("归一化对比度，范围 0 到 1。")]
        public double MinimumContrast { get; set; } = 0.02;
        [Category("质量"), DisplayName("最大点阵残差比例"), Description("相对网格间距的最大拟合残差，范围大于 0 且不超过 1。")]
        public double MaximumGridResidualFraction { get; set; } = 0.3;

        public bool TryValidate(out string error)
        {
            error = !IsValidDimension(ExpectedRows) || !IsValidDimension(ExpectedCols) ? "点阵行列数必须为 3 到 15 的奇数。"
                : MaxProcessingSize < 256 || MaxProcessingSize > 4096 ? "粗定位最大边长必须在 256 到 4096 之间。"
                : !double.IsFinite(MinimumContrast) || MinimumContrast < 0 || MinimumContrast > 1 ? "最小对比度必须为 0 到 1 的有限值。"
                : !double.IsFinite(MaximumGridResidualFraction) || MaximumGridResidualFraction <= 0 || MaximumGridResidualFraction > 1 ? "最大点阵残差比例必须为 (0, 1] 的有限值。"
                : string.Empty;
            return error.Length == 0;
        }

        public string ToJson()
        {
            if (!TryValidate(out string error)) throw new ArgumentException(error);
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        internal static bool IsValidDimension(int value) => value >= 3 && value <= 15 && value % 2 == 1;
    }

    public sealed record GridDistortionPoint
    {
        public int Id { get; init; }
        public int Row { get; init; }
        public int Col { get; init; }
        public string Name { get; init; } = string.Empty;
        public double X { get; init; }
        public double Y { get; init; }
        public double Area { get; init; }
        public double Contrast { get; init; }
    }

    public sealed record GridDistortionMetrics
    {
        public double HorizontalTvPercent { get; init; }
        public double VerticalTvPercent { get; init; }
        public double TopPercent { get; init; }
        public double BottomPercent { get; init; }
        public double LeftPercent { get; init; }
        public double RightPercent { get; init; }
        public double KeystoneHorizontalPercent { get; init; }
        public double KeystoneVerticalPercent { get; init; }
        public double TopWidth { get; init; }
        public double MiddleWidth { get; init; }
        public double BottomWidth { get; init; }
        public double LeftHeight { get; init; }
        public double CenterHeight { get; init; }
        public double RightHeight { get; init; }
    }

    public sealed record GridDistortionQuality
    {
        public double Score { get; init; }
        public double MinimumContrast { get; init; }
        public double GridResidualFraction { get; init; }
        public double ProcessingScale { get; init; } = 1;
    }

    public sealed record GridDistortionTimings
    {
        public double PreprocessMs { get; init; }
        public double CandidatesMs { get; init; }
        public double GridMs { get; init; }
        public double RefineMs { get; init; }
        public double MetricsMs { get; init; }
        public double TotalMs { get; init; }
    }

    public sealed record GridDistortionResult
    {
        public string Algorithm { get; init; } = "GridDistortion";
        public string Version { get; init; } = "2.0";
        public bool Success { get; init; }
        public string StatusCode { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public int ExpectedRows { get; init; }
        public int ExpectedCols { get; init; }
        public int CandidateCount { get; init; }
        public int SelectedCount { get; init; }
        public IReadOnlyList<GridDistortionPoint> Points { get; init; } = Array.Empty<GridDistortionPoint>();
        public IReadOnlyList<int> ReferencePointIds { get; init; } = Array.Empty<int>();
        public GridDistortionMetrics? Metrics { get; init; }
        public GridDistortionQuality Quality { get; init; } = new();
        public GridDistortionTimings Timings { get; init; } = new();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public string RawJson { get; init; } = string.Empty;
        public int NativeReturnCode { get; init; }
        public string InteropDiagnostic { get; init; } = string.Empty;

        public static GridDistortionResult CreateFailure(string statusCode, string message, int nativeReturnCode = 0, string rawJson = "") =>
            new() { StatusCode = statusCode, Message = message, InteropDiagnostic = message, NativeReturnCode = nativeReturnCode, RawJson = rawJson };
    }

    public static class GridDistortionResultParser
    {
        public static bool TryParse(string json, int imageWidth, int imageHeight, out GridDistortionResult result, out string error)
        {
            result = GridDistortionResult.CreateFailure("ResultParseFailed", "结果尚未解析。", rawJson: json ?? string.Empty);
            try
            {
                Require(imageWidth > 0 && imageHeight > 0, "Image dimensions must be positive.");
                using JsonDocument document = JsonDocument.Parse(json ?? string.Empty);
                JsonElement root = document.RootElement;
                Require(Text(root, "algorithm") == "GridDistortion" && Text(root, "version") == "2.0", "Unsupported algorithm or version.");
                bool success = root.GetProperty("success").GetBoolean();
                string status = Text(root, "statusCode");
                Require(!string.IsNullOrWhiteSpace(status), "statusCode is required.");
                int rows = Integer(root, "expectedRows"), cols = Integer(root, "expectedCols");
                Require(GridDistortionOptions.IsValidDimension(rows) && GridDistortionOptions.IsValidDimension(cols), "Invalid grid dimensions.");
                GridDistortionPoint[] points = root.GetProperty("points").EnumerateArray().Select(p => new GridDistortionPoint
                {
                    Id = Integer(p, "id"), Row = Integer(p, "row"), Col = Integer(p, "col"), Name = Text(p, "name"),
                    X = Number(p, "x"), Y = Number(p, "y"), Area = Number(p, "area"), Contrast = Number(p, "contrast")
                }).ToArray();
                int candidateCount = Integer(root, "candidateCount"), selectedCount = Integer(root, "selectedCount");
                Require(selectedCount == points.Length && selectedCount <= rows * cols && candidateCount >= selectedCount, "Inconsistent point counts.");
                Require(points.All(p => p.Id >= 0 && p.Row >= 0 && p.Row < rows && p.Col >= 0 && p.Col < cols &&
                    p.X >= 0 && p.X < imageWidth && p.Y >= 0 && p.Y < imageHeight && p.Area > 0 && p.Contrast >= 0 && p.Contrast <= 1), "Invalid point geometry or contrast.");
                Require(points.Select(p => p.Id).Distinct().Count() == points.Length && points.Select(p => (p.Row, p.Col)).Distinct().Count() == points.Length, "Duplicate point IDs or grid positions.");
                int[] references = root.GetProperty("referencePointIds").EnumerateArray().Select(p => p.GetInt32()).ToArray();
                Require(references.Distinct().Count() == references.Length && references.All(id => points.Any(p => p.Id == id)), "Invalid reference point IDs.");
                JsonElement metricsJson = root.GetProperty("metrics");
                GridDistortionMetrics? metrics = metricsJson.ValueKind == JsonValueKind.Null ? null : ReadMetrics(metricsJson);
                Require(success ? selectedCount == rows * cols && metrics != null && references.Length == 9 : metrics == null, "Success, metrics and selected points disagree.");
                if (success)
                {
                    for (int index = 0; index < 9; index++)
                    {
                        GridDistortionPoint point = points.Single(p => p.Id == references[index]);
                        Require(point.Row == (index / 3) * (rows - 1) / 2 && point.Col == (index % 3) * (cols - 1) / 2, "Reference points must use the outer and central grid rows and columns in row-major order.");
                    }
                }
                JsonElement qualityJson = root.GetProperty("quality");
                GridDistortionQuality quality = new()
                {
                    Score = Number(qualityJson, "score"), MinimumContrast = Number(qualityJson, "minimumContrast"),
                    GridResidualFraction = Number(qualityJson, "gridResidualFraction"), ProcessingScale = Number(qualityJson, "processingScale")
                };
                Require(quality.Score >= 0 && quality.Score <= 1 && quality.MinimumContrast >= 0 && quality.MinimumContrast <= 1 &&
                    quality.GridResidualFraction >= 0 && quality.ProcessingScale > 0 && quality.ProcessingScale <= 1, "Invalid quality values.");
                JsonElement timingsJson = root.GetProperty("timings");
                GridDistortionTimings timings = new()
                {
                    PreprocessMs = Number(timingsJson, "preprocessMs"), CandidatesMs = Number(timingsJson, "candidatesMs"),
                    GridMs = Number(timingsJson, "gridMs"), RefineMs = Number(timingsJson, "refineMs"),
                    MetricsMs = Number(timingsJson, "metricsMs"), TotalMs = Number(timingsJson, "totalMs")
                };
                Require(new[] { timings.PreprocessMs, timings.CandidatesMs, timings.GridMs, timings.RefineMs, timings.MetricsMs, timings.TotalMs }.All(t => t >= 0), "Timing values cannot be negative.");
                result = new GridDistortionResult
                {
                    Success = success, StatusCode = status, Message = Text(root, "message"), ExpectedRows = rows, ExpectedCols = cols,
                    CandidateCount = candidateCount, SelectedCount = selectedCount, Points = points, ReferencePointIds = references,
                    Metrics = metrics, Quality = quality, Timings = timings, RawJson = json ?? string.Empty,
                    Warnings = root.GetProperty("warnings").EnumerateArray().Select(p => p.GetString() ?? throw new JsonException("Warning must be a string.")).ToArray()
                };
                error = string.Empty;
                return true;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
            {
                error = ex.Message;
                result = GridDistortionResult.CreateFailure("ResultParseFailed", error, rawJson: json ?? string.Empty);
                return false;
            }
        }

        private static GridDistortionMetrics ReadMetrics(JsonElement element)
        {
            GridDistortionMetrics metrics = new()
            {
                HorizontalTvPercent = Number(element, "horizontalTvPercent"), VerticalTvPercent = Number(element, "verticalTvPercent"),
                TopPercent = Number(element, "topPercent"), BottomPercent = Number(element, "bottomPercent"),
                LeftPercent = Number(element, "leftPercent"), RightPercent = Number(element, "rightPercent"),
                KeystoneHorizontalPercent = Number(element, "keystoneHorizontalPercent"), KeystoneVerticalPercent = Number(element, "keystoneVerticalPercent"),
                TopWidth = Number(element, "topWidth"), MiddleWidth = Number(element, "middleWidth"), BottomWidth = Number(element, "bottomWidth"),
                LeftHeight = Number(element, "leftHeight"), CenterHeight = Number(element, "centerHeight"), RightHeight = Number(element, "rightHeight")
            };
            Require(new[] { metrics.TopWidth, metrics.MiddleWidth, metrics.BottomWidth, metrics.LeftHeight, metrics.CenterHeight, metrics.RightHeight }.All(v => v > 0), "Metric spans must be positive.");
            return metrics;
        }

        private static double Number(JsonElement element, string name)
        {
            double value = element.GetProperty(name).GetDouble();
            Require(double.IsFinite(value), $"{name} must be finite.");
            return value;
        }
        private static int Integer(JsonElement element, string name) => element.GetProperty(name).GetInt32();
        private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString() ?? throw new JsonException($"{name} must be a string.");
        private static void Require(bool condition, string message) { if (!condition) throw new JsonException(message); }
    }

    public static class GridDistortionNative
    {
        internal delegate int NativeJsonCall(out IntPtr result);

        [DllImport("opencv_helper.dll", EntryPoint = "M_CalDistortionGridV2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Detect(HImage image, RoiRect roi, [MarshalAs(UnmanagedType.LPUTF8Str)] string config, out IntPtr result);

        public static GridDistortionResult Run(HImage image, RoiRect roi, GridDistortionOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (!options.TryValidate(out string error)) return GridDistortionResult.CreateFailure("InvalidConfiguration", error);
            if (!TryValidateInput(image, roi, out error)) return GridDistortionResult.CreateFailure("InvalidInput", error);
            string config = options.ToJson();
            int expectedRows = options.ExpectedRows, expectedCols = options.ExpectedCols;
            GridDistortionResult result = Invoke((out IntPtr pointer) => Detect(image, roi, config, out pointer),
                pointer => Marshal.PtrToStringUTF8(pointer) ?? string.Empty, OpenCVMediaHelper.FreeResult, image.cols, image.rows);
            if (result.Success && (result.ExpectedRows != expectedRows || result.ExpectedCols != expectedCols))
                return GridDistortionResult.CreateFailure("ResultParseFailed", "Returned grid dimensions differ from the requested pattern.", result.NativeReturnCode, result.RawJson);
            return result;
        }

        public static bool TryValidateInput(HImage image, RoiRect roi, out string error)
        {
            bool imageValid = image.pData != IntPtr.Zero && image.cols > 0 && image.rows > 0 &&
                image.channels is 1 or 3 or 4 && image.depth is 8 or 16 or 32 or 64 &&
                image.stride >= (long)image.cols * image.channels * (image.depth / 8);
            bool fullImage = roi.X == 0 && roi.Y == 0 && roi.Width == 0 && roi.Height == 0;
            bool roiValid = fullImage || roi.X >= 0 && roi.Y >= 0 && roi.Width > 0 && roi.Height > 0 &&
                (long)roi.X + roi.Width <= image.cols && (long)roi.Y + roi.Height <= image.rows;
            error = !imageValid ? "图像缓冲区、尺寸、位深、通道或步长无效。" : !roiValid ? "ROI 必须完整位于图像内；仅 (0,0,0,0) 表示整图。" : string.Empty;
            return error.Length == 0;
        }

        internal static GridDistortionResult Invoke(NativeJsonCall call, Func<IntPtr, string> reader, Func<IntPtr, int> releaser, int width, int height)
        {
            IntPtr pointer = IntPtr.Zero;
            GridDistortionResult result;
            string releaseError = string.Empty;
            int returnCode = 0;
            try
            {
                returnCode = call(out pointer);
                if (returnCode <= 0 || pointer == IntPtr.Zero)
                    result = GridDistortionResult.CreateFailure("NativeCallFailed", $"M_CalDistortionGridV2 returned {returnCode}; result pointer {(pointer == IntPtr.Zero ? "is null" : "is non-null")}.", returnCode);
                else
                {
                    string json = reader(pointer);
                    GridDistortionResultParser.TryParse(json, width, height, out result, out _);
                    result = result with { NativeReturnCode = returnCode };
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or MarshalDirectiveException or SEHException or InvalidOperationException or ArgumentException)
            {
                string status = ex switch
                {
                    DllNotFoundException => "NativeLibraryUnavailable", EntryPointNotFoundException => "NativeEntryPointUnavailable",
                    BadImageFormatException => "NativeLibraryIncompatible", _ => "ManagedInteropFailed"
                };
                result = GridDistortionResult.CreateFailure(status, ex.Message, returnCode);
            }
            finally
            {
                if (pointer != IntPtr.Zero)
                {
                    try
                    {
                        int releaseCode = releaser(pointer);
                        if (releaseCode != 0) releaseError = $"FreeResult returned {releaseCode}.";
                    }
                    catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or MarshalDirectiveException or SEHException or InvalidOperationException)
                    { releaseError = $"FreeResult failed: {ex.Message}"; }
                }
            }
            return releaseError.Length == 0 ? result : GridDistortionResult.CreateFailure("NativeResultReleaseFailed", releaseError, returnCode, result.RawJson);
        }
    }
}
