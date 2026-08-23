using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Core
{
    public enum LuminousAreaDetectionMode
    {
        [Description("鲁棒自动（推荐）")]
        RobustV2 = 0,

        [Description("经典兼容")]
        Legacy = 1
    }

    public readonly record struct LuminousAreaPoint(double X, double Y);

    public sealed class LuminousAreaSideQuality
    {
        internal LuminousAreaSideQuality(string side, double? score, IReadOnlyDictionary<string, double>? metrics = null)
        {
            Side = side;
            Score = score;
            Metrics = metrics ?? EmptyMetrics;
        }

        private static IReadOnlyDictionary<string, double> EmptyMetrics { get; } =
            new ReadOnlyDictionary<string, double>(new Dictionary<string, double>());

        public string Side { get; }

        public double? Score { get; }

        public IReadOnlyDictionary<string, double> Metrics { get; }
    }

    public sealed class LuminousAreaDetectionResult
    {
        internal LuminousAreaDetectionResult(
            bool success,
            string algorithm,
            IReadOnlyList<LuminousAreaPoint>? corners,
            double? confidence,
            IReadOnlyList<LuminousAreaSideQuality>? sideQuality,
            string failureReason,
            IReadOnlyList<string>? warnings,
            string rawJson = "",
            int nativeReturnCode = 0,
            string diagnostic = "")
        {
            Success = success;
            Algorithm = algorithm;
            Corners = corners ?? Array.Empty<LuminousAreaPoint>();
            Confidence = confidence;
            SideQuality = sideQuality ?? Array.Empty<LuminousAreaSideQuality>();
            FailureReason = failureReason;
            Warnings = warnings ?? Array.Empty<string>();
            RawJson = rawJson;
            NativeReturnCode = nativeReturnCode;
            Diagnostic = diagnostic;
        }

        public bool Success { get; }

        public string Algorithm { get; }

        /// <summary>
        /// Coordinates in strict LT, RT, RB, LB order. Results returned by
        /// <see cref="LuminousAreaNative"/> are normalized to full-image coordinates.
        /// </summary>
        public IReadOnlyList<LuminousAreaPoint> Corners { get; }

        public double? Confidence { get; }

        public IReadOnlyList<LuminousAreaSideQuality> SideQuality { get; }

        public string FailureReason { get; }

        public IReadOnlyList<string> Warnings { get; }

        public string RawJson { get; }

        public int NativeReturnCode { get; }

        public string Diagnostic { get; }

        public bool HasValidCorners => Success && LuminousAreaResultParser.TryValidateOrderedCorners(Corners, out _);

        public static LuminousAreaDetectionResult CreateFailure(string algorithm, string failureReason, int nativeReturnCode = 0, string diagnostic = "", string rawJson = "") =>
            new(false, algorithm, null, null, null, failureReason, null, rawJson, nativeReturnCode, diagnostic);

        internal LuminousAreaDetectionResult WithNativeContext(string rawJson, int nativeReturnCode) =>
            new(Success, Algorithm, Corners, Confidence, SideQuality, FailureReason, Warnings, rawJson, nativeReturnCode, Diagnostic);

        internal LuminousAreaDetectionResult AsFailure(string failureReason, string diagnostic = "") =>
            new(false, Algorithm, Corners, Confidence, SideQuality, failureReason, Warnings, RawJson, NativeReturnCode, diagnostic);

        internal LuminousAreaDetectionResult Offset(double offsetX, double offsetY)
        {
            bool hasFiniteCorners = Corners.Count == 4
                && Corners.All(point => double.IsFinite(point.X) && double.IsFinite(point.Y));
            if (!hasFiniteCorners || (offsetX == 0 && offsetY == 0))
            {
                return this;
            }

            LuminousAreaPoint[] corners = Corners
                .Select(point => new LuminousAreaPoint(point.X + offsetX, point.Y + offsetY))
                .ToArray();
            return new(Success, Algorithm, corners, Confidence, SideQuality, FailureReason, Warnings, RawJson, NativeReturnCode, Diagnostic);
        }
    }

    public static class LuminousAreaResultParser
    {
        private static readonly string[] SideNames = { "Top", "Right", "Bottom", "Left" };

        public static bool TryParseV2(string json, out LuminousAreaDetectionResult result, out string error)
        {
            result = LuminousAreaDetectionResult.CreateFailure("RobustV2", "ResultParseFailed", diagnostic: "Result JSON was not parsed.", rawJson: json ?? string.Empty);
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "结果 JSON 为空。";
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    error = "结果 JSON 不是对象。";
                    return false;
                }

                if (!TryGetProperty(root, "Success", out JsonElement successElement) ||
                    successElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    error = "结果缺少布尔类型的 Success。";
                    return false;
                }

                bool success = successElement.GetBoolean();
                string algorithm = ReadString(root, "Algorithm") ?? string.Empty;
                if (!string.Equals(algorithm, "RobustV2", StringComparison.Ordinal))
                {
                    error = "结果缺少 Algorithm=RobustV2。";
                    return false;
                }
                string failureReason = ReadString(root, "FailureReason") ?? string.Empty;
                double? confidence = ReadFiniteNumber(root, "Confidence");
                if (!confidence.HasValue || confidence.Value < 0 || confidence.Value > 1)
                {
                    error = "结果缺少 0 到 1 范围内的 Confidence。";
                    return false;
                }
                IReadOnlyList<string> warnings = ReadWarnings(root);
                IReadOnlyList<LuminousAreaSideQuality> sideQuality = ReadSideQuality(root);
                IReadOnlyList<LuminousAreaPoint> corners = Array.Empty<LuminousAreaPoint>();

                if (TryGetProperty(root, "Corners", out JsonElement cornersElement))
                {
                    if (!TryReadCorners(cornersElement, out corners, out error))
                    {
                        return false;
                    }
                }

                if (success && corners.Count != 4)
                {
                    error = "成功结果必须包含四个角点。";
                    return false;
                }
                if (success && !TryValidateOrderedCorners(corners, out string geometryError))
                {
                    error = $"成功结果四角点无效：{geometryError}";
                    return false;
                }

                if (!success && string.IsNullOrWhiteSpace(failureReason))
                {
                    failureReason = "UnknownFailure";
                }

                result = new LuminousAreaDetectionResult(
                    success,
                    algorithm,
                    corners,
                    confidence,
                    sideQuality,
                    failureReason,
                    warnings,
                    json);
                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryValidateOrderedCorners(IReadOnlyList<LuminousAreaPoint>? corners, out string error)
        {
            error = string.Empty;
            if (corners == null || corners.Count != 4)
            {
                error = "必须包含 LT、RT、RB、LB 四个角点。";
                return false;
            }
            if (corners.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            {
                error = "角点包含非有限坐标。";
                return false;
            }

            double signedAreaTwice = 0;
            for (int index = 0; index < corners.Count; index++)
            {
                LuminousAreaPoint current = corners[index];
                LuminousAreaPoint next = corners[(index + 1) % corners.Count];
                signedAreaTwice += current.X * next.Y - next.X * current.Y;
            }
            if (!double.IsFinite(signedAreaTwice) || Math.Abs(signedAreaTwice) < 1)
            {
                error = "四角点不能构成非退化四边形。";
                return false;
            }
            if (signedAreaTwice < 0)
            {
                error = "角点方向错误；必须按 LT、RT、RB、LB 顺序排列。";
                return false;
            }

            int expectedLeftTop = Enumerable.Range(0, corners.Count)
                .OrderBy(index => corners[index].X + corners[index].Y)
                .ThenBy(index => corners[index].Y)
                .First();
            if (expectedLeftTop != 0)
            {
                error = "首点不是 LT；必须按 LT、RT、RB、LB 顺序排列。";
                return false;
            }

            double winding = 0;
            for (int index = 0; index < corners.Count; index++)
            {
                LuminousAreaPoint a = corners[index];
                LuminousAreaPoint b = corners[(index + 1) % corners.Count];
                LuminousAreaPoint c = corners[(index + 2) % corners.Count];
                double cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (!double.IsFinite(cross) || Math.Abs(cross) < 1e-4)
                {
                    error = "四角点包含重合或共线边。";
                    return false;
                }
                if (winding == 0)
                {
                    winding = Math.Sign(cross);
                }
                else if (Math.Sign(cross) != winding)
                {
                    error = "角点不是按 LT、RT、RB、LB 排列的凸四边形。";
                    return false;
                }
            }
            return true;
        }

        internal static bool TryParseLegacy(string json, bool useRotatedRect, out LuminousAreaDetectionResult result, out string error)
        {
            result = LuminousAreaDetectionResult.CreateFailure("Legacy", "ResultParseFailed", diagnostic: "Legacy result JSON was not parsed.", rawJson: json ?? string.Empty);
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "旧算法结果 JSON 为空。";
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    error = "旧算法结果 JSON 不是对象。";
                    return false;
                }

                IReadOnlyList<LuminousAreaPoint> corners;
                if (useRotatedRect)
                {
                    if (!TryGetProperty(root, "Corners", out JsonElement cornersElement) ||
                        !TryReadCorners(cornersElement, out corners, out error))
                    {
                        if (string.IsNullOrEmpty(error))
                        {
                            error = "旧算法结果缺少 Corners。";
                        }
                        return false;
                    }

                    corners = OrderCorners(corners);
                }
                else
                {
                    if (!TryReadFiniteNumber(root, "X", out double x) ||
                        !TryReadFiniteNumber(root, "Y", out double y) ||
                        !TryReadFiniteNumber(root, "Width", out double width) ||
                        !TryReadFiniteNumber(root, "Height", out double height) ||
                        width <= 0 || height <= 0)
                    {
                        error = "旧算法结果中的矩形无效。";
                        return false;
                    }

                    corners = new[]
                    {
                        new LuminousAreaPoint(x, y),
                        new LuminousAreaPoint(x + width, y),
                        new LuminousAreaPoint(x + width, y + height),
                        new LuminousAreaPoint(x, y + height)
                    };
                }

                if (corners.Count != 4)
                {
                    error = "旧算法结果必须包含四个角点。";
                    return false;
                }

                result = new LuminousAreaDetectionResult(true, "Legacy", corners, null, null, string.Empty, null, json);
                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryReadCorners(JsonElement element, out IReadOnlyList<LuminousAreaPoint> corners, out string error)
        {
            List<LuminousAreaPoint> values = new();
            error = string.Empty;
            corners = values;

            if (element.ValueKind != JsonValueKind.Array)
            {
                error = "Corners 不是数组。";
                return false;
            }

            foreach (JsonElement corner in element.EnumerateArray())
            {
                double x;
                double y;
                if (corner.ValueKind == JsonValueKind.Object)
                {
                    if (!TryReadFiniteNumber(corner, "X", out x) || !TryReadFiniteNumber(corner, "Y", out y))
                    {
                        error = "Corners 包含无效坐标。";
                        return false;
                    }
                }
                else if (corner.ValueKind == JsonValueKind.Array)
                {
                    JsonElement[] coordinates = corner.EnumerateArray().Take(2).ToArray();
                    if (coordinates.Length < 2 || !TryReadFiniteNumber(coordinates[0], out x) || !TryReadFiniteNumber(coordinates[1], out y))
                    {
                        error = "Corners 包含无效坐标。";
                        return false;
                    }
                }
                else
                {
                    error = "Corners 包含不支持的点格式。";
                    return false;
                }

                values.Add(new LuminousAreaPoint(x, y));
            }

            corners = values;
            return true;
        }

        private static IReadOnlyList<LuminousAreaPoint> OrderCorners(IReadOnlyList<LuminousAreaPoint> corners)
        {
            if (corners.Count != 4)
            {
                return corners;
            }

            double centerX = corners.Average(point => point.X);
            double centerY = corners.Average(point => point.Y);
            List<LuminousAreaPoint> ordered = corners
                .OrderBy(point => Math.Atan2(point.Y - centerY, point.X - centerX))
                .ToList();
            int leftTopIndex = Enumerable.Range(0, ordered.Count)
                .OrderBy(index => ordered[index].X + ordered[index].Y)
                .ThenBy(index => ordered[index].Y)
                .First();
            return ordered.Skip(leftTopIndex).Concat(ordered.Take(leftTopIndex)).ToArray();
        }

        private static string[] ReadWarnings(JsonElement root)
        {
            if (!TryGetProperty(root, "Warnings", out JsonElement warningsElement) || warningsElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return warningsElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        private static IReadOnlyList<LuminousAreaSideQuality> ReadSideQuality(JsonElement root)
        {
            if (!TryGetProperty(root, "SideQuality", out JsonElement qualityElement))
            {
                return Array.Empty<LuminousAreaSideQuality>();
            }

            if (qualityElement.ValueKind == JsonValueKind.Array)
            {
                List<LuminousAreaSideQuality> sides = new();
                int index = 0;
                foreach (JsonElement item in qualityElement.EnumerateArray())
                {
                    if (index >= SideNames.Length)
                    {
                        break;
                    }

                    sides.Add(ReadSideQualityValue(SideNames[index], item));
                    index++;
                }
                return sides;
            }

            if (qualityElement.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<LuminousAreaSideQuality>();
            }

            Dictionary<string, JsonElement> properties = qualityElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);
            if (SideNames.Any(side => properties.ContainsKey(side)))
            {
                return SideNames
                    .Where(properties.ContainsKey)
                    .Select(side => ReadSideQualityValue(side, properties[side]))
                    .ToArray();
            }

            if (properties.Values.Any(value => value.ValueKind == JsonValueKind.Array))
            {
                List<Dictionary<string, double>> metricsBySide = Enumerable.Range(0, 4)
                    .Select(_ => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase))
                    .ToList();
                foreach ((string metricName, JsonElement metricValues) in properties)
                {
                    if (metricValues.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    int index = 0;
                    foreach (JsonElement value in metricValues.EnumerateArray())
                    {
                        if (index >= metricsBySide.Count)
                        {
                            break;
                        }
                        if (TryReadFiniteNumber(value, out double number))
                        {
                            metricsBySide[index][metricName] = number;
                        }
                        index++;
                    }
                }

                return metricsBySide.Select((metrics, index) => CreateSideQuality(SideNames[index], metrics)).ToArray();
            }

            return properties.Select(property => ReadSideQualityValue(property.Key, property.Value)).ToArray();
        }

        private static LuminousAreaSideQuality ReadSideQualityValue(string side, JsonElement value)
        {
            if (TryReadFiniteNumber(value, out double score))
            {
                return new LuminousAreaSideQuality(side, score);
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                return new LuminousAreaSideQuality(side, null);
            }

            string? reportedName = ReadString(value, "Name");
            if (!string.IsNullOrWhiteSpace(reportedName))
            {
                side = reportedName;
            }

            Dictionary<string, double> metrics = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (TryReadFiniteNumber(property.Value, out double metric))
                {
                    metrics[property.Name] = metric;
                }
            }
            return CreateSideQuality(side, metrics);
        }

        private static LuminousAreaSideQuality CreateSideQuality(string side, Dictionary<string, double> metrics)
        {
            double? score = null;
            foreach (string scoreName in new[] { "Score", "Confidence", "Quality" })
            {
                if (metrics.TryGetValue(scoreName, out double value))
                {
                    score = value;
                    break;
                }
            }
            if (!score.HasValue && metrics.Count == 1)
            {
                score = metrics.Values.First();
            }

            return new LuminousAreaSideQuality(
                side,
                score,
                new ReadOnlyDictionary<string, double>(metrics));
        }

        private static string? ReadString(JsonElement root, string name) =>
            TryGetProperty(root, name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static double? ReadFiniteNumber(JsonElement root, string name) =>
            TryReadFiniteNumber(root, name, out double value) ? value : null;

        private static bool TryReadFiniteNumber(JsonElement root, string name, out double value)
        {
            value = 0;
            return TryGetProperty(root, name, out JsonElement element) && TryReadFiniteNumber(element, out value);
        }

        private static bool TryReadFiniteNumber(JsonElement element, out double value)
        {
            value = 0;
            return element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value) && double.IsFinite(value);
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.TryGetProperty(name, out value))
            {
                return true;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }

    public static class LuminousAreaNative
    {
        public static LuminousAreaDetectionResult DetectV2(HImage image, RoiRect roi, double minConfidence)
        {
            if (!double.IsFinite(minConfidence) || minConfidence < 0 || minConfidence > 1)
            {
                return LuminousAreaDetectionResult.CreateFailure("RobustV2", "InvalidConfiguration", diagnostic: "MinConfidence must be within [0, 1].");
            }

            string configJson = JsonSerializer.Serialize(new { MinConfidence = minConfidence });
            LuminousAreaDetectionResult result = Invoke(
                "RobustV2",
                (out IntPtr resultPtr) => OpenCVMediaHelper.M_FindLuminousAreaV2(image, roi, configJson, out resultPtr),
                LuminousAreaResultParser.TryParseV2);
            result = ApplyRoiOffset(image, roi, result);
            return EnforceMinimumConfidence(result, minConfidence);
        }

        public static LuminousAreaDetectionResult DetectLegacy(HImage image, RoiRect roi, int threshold, bool useRotatedRect)
        {
            string configJson = JsonSerializer.Serialize(new { Threshold = threshold, UseRotatedRect = useRotatedRect });
            LuminousAreaDetectionResult result = Invoke(
                "Legacy",
                (out IntPtr resultPtr) => OpenCVMediaHelper.M_FindLuminousArea(image, roi, configJson, out resultPtr),
                (string json, out LuminousAreaDetectionResult parsed, out string error) =>
                    LuminousAreaResultParser.TryParseLegacy(json, useRotatedRect, out parsed, out error));
            return ApplyRoiOffset(image, roi, result);
        }

        private delegate int NativeJsonCall(out IntPtr result);

        private delegate bool ResultParser(string json, out LuminousAreaDetectionResult result, out string error);

        private static LuminousAreaDetectionResult Invoke(string algorithm, NativeJsonCall call, ResultParser parser)
        {
            IntPtr resultPtr = IntPtr.Zero;
            try
            {
                int returnCode = call(out resultPtr);
                if (returnCode <= 0 || resultPtr == IntPtr.Zero)
                {
                    return LuminousAreaDetectionResult.CreateFailure(
                        algorithm,
                        "NativeCallFailed",
                        returnCode,
                        resultPtr == IntPtr.Zero ? "Native result pointer is null." : $"Native return code: {returnCode}.");
                }

                IntPtr ownedResult = resultPtr;
                resultPtr = IntPtr.Zero;
                string json = OpenCVMediaHelper.PtrToStringUtf8AndFree(ownedResult);
                if (!parser(json, out LuminousAreaDetectionResult parsed, out string parseError))
                {
                    return LuminousAreaDetectionResult.CreateFailure(algorithm, "ResultParseFailed", returnCode, parseError, json);
                }

                return parsed.WithNativeContext(json, returnCode);
            }
            catch (DllNotFoundException ex)
            {
                return LuminousAreaDetectionResult.CreateFailure(algorithm, "NativeLibraryUnavailable", diagnostic: ex.Message);
            }
            catch (EntryPointNotFoundException ex)
            {
                return LuminousAreaDetectionResult.CreateFailure(algorithm, "NativeEntryPointUnavailable", diagnostic: ex.Message);
            }
            catch (BadImageFormatException ex)
            {
                return LuminousAreaDetectionResult.CreateFailure(algorithm, "NativeLibraryIncompatible", diagnostic: ex.Message);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
            {
                return LuminousAreaDetectionResult.CreateFailure(algorithm, "ManagedInteropFailed", diagnostic: ex.Message);
            }
            finally
            {
                if (resultPtr != IntPtr.Zero)
                {
                    _ = OpenCVMediaHelper.FreeResult(resultPtr);
                }
            }
        }

        private static LuminousAreaDetectionResult ApplyRoiOffset(HImage image, RoiRect roi, LuminousAreaDetectionResult result)
        {
            long right = (long)roi.X + roi.Width;
            long bottom = (long)roi.Y + roi.Height;
            bool validRoi = roi.X >= 0 && roi.Y >= 0 && roi.Width > 0 && roi.Height > 0 && right <= image.cols && bottom <= image.rows;
            return validRoi ? result.Offset(roi.X, roi.Y) : result;
        }

        internal static LuminousAreaDetectionResult EnforceMinimumConfidence(
            LuminousAreaDetectionResult result,
            double minConfidence)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (!result.Success)
            {
                return result;
            }
            if (!result.Confidence.HasValue || !double.IsFinite(result.Confidence.Value)
                || result.Confidence.Value < 0 || result.Confidence.Value > 1)
            {
                return result.AsFailure("ResultParseFailed", "Successful V2 result has an invalid Confidence.");
            }
            return result.Confidence.Value < minConfidence
                ? result.AsFailure("LowConfidence", $"Managed confidence gate: {result.Confidence.Value:F6} < {minConfidence:F6}.")
                : result;
        }
    }
}
