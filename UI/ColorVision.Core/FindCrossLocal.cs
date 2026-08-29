using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Core
{
    public readonly record struct FindCrossLocalPoint(
        [property: JsonPropertyName("x")] double X,
        [property: JsonPropertyName("y")] double Y);

    public readonly record struct FindCrossLocalTilt(
        [property: JsonPropertyName("tilt_x")] double X,
        [property: JsonPropertyName("tilt_y")] double Y);

    public readonly record struct FindCrossLocalRectangle(double X, double Y, double Width, double Height);

    public sealed class FindCrossLocalDistortionOptions
    {
        [JsonPropertyName("Enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("K1")]
        public double K1 { get; set; }

        [JsonPropertyName("K2")]
        public double K2 { get; set; }

        [JsonPropertyName("P1")]
        public double P1 { get; set; }

        [JsonPropertyName("P2")]
        public double P2 { get; set; }

        [JsonPropertyName("K3")]
        public double K3 { get; set; }

        [JsonPropertyName("Fx")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? FxPixels { get; set; }

        [JsonPropertyName("Fy")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? FyPixels { get; set; }

        [JsonPropertyName("Cx")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? PrincipalPointX { get; set; }

        [JsonPropertyName("Cy")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? PrincipalPointY { get; set; }
    }

    public sealed class FindCrossLocalOpticsOptions
    {
        [JsonPropertyName("stdCenter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FindCrossLocalPoint? StandardCenter { get; set; }

        [JsonPropertyName("focusLength")]
        public double FocusLengthMillimeters { get; set; } = 25.4;

        [JsonPropertyName("sensorPixSize")]
        public double SensorPixelSizeMicrometers { get; set; } = 3.76;

        [JsonPropertyName("distortion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FindCrossLocalDistortionOptions? Distortion { get; set; }
    }

    /// <summary>
    /// Production configuration for local FindCross. Algorithm thresholds and strategy
    /// are deliberately internal so a recipe exposes only product geometry and calibrated
    /// optics. Legacy and diagnostic JSON payloads remain accepted by
    /// <see cref="FindCrossLocal.RunJson"/> without expanding this public surface.
    /// </summary>
    public sealed class FindCrossLocalOptions
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        [JsonPropertyName("opticsParams")]
        public FindCrossLocalOpticsOptions Optics { get; set; } = new();

        [JsonPropertyName("ExpectedAngleDegrees")]
        public double ExpectedAngleDegrees { get; set; }

        [JsonPropertyName("AngleToleranceDegrees")]
        public double AngleToleranceDegrees { get; set; } = 10;

        [JsonPropertyName("Name")]
        public string Name { get; set; } = "Point_1";

        [JsonPropertyName("CalibrationOffset")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FindCrossLocalPoint? CalibrationOffset { get; set; }

        public string ToJson()
        {
            if (!TryValidate(out string error))
            {
                throw new ArgumentException(error, nameof(FindCrossLocalOptions));
            }

            Dictionary<string, object?> payload = new(StringComparer.Ordinal)
            {
                ["opticsParams"] = Optics,
                ["ExpectedAngleDegrees"] = ExpectedAngleDegrees,
                ["AngleToleranceDegrees"] = AngleToleranceDegrees,
                ["Name"] = Name
            };
            if (CalibrationOffset.HasValue) payload["CalibrationOffset"] = CalibrationOffset.Value;
            return JsonSerializer.Serialize(payload, SerializerOptions);
        }

        public bool TryValidate(out string error)
        {
            if (Optics?.Distortion is { Enabled: true } enabledDistortion &&
                !HasCompleteFiniteIntrinsics(enabledDistortion))
            {
                error = "Enabled distortion correction requires complete calibrated Fx/Fy/Cx/Cy; Fx and Fy must be positive.";
                return false;
            }
            if (Optics == null ||
                (Optics.StandardCenter.HasValue && !IsFinite(Optics.StandardCenter.Value)) ||
                !IsFinitePositive(Optics.FocusLengthMillimeters) || !IsFinitePositive(Optics.SensorPixelSizeMicrometers) ||
                (Optics.Distortion != null && !IsFinite(Optics.Distortion)))
            {
                error = "opticsParams contains a non-finite center/distortion coefficient or non-positive optical dimension.";
                return false;
            }
            if (CalibrationOffset.HasValue && !IsFinite(CalibrationOffset.Value))
            {
                error = "CalibrationOffset coordinates must be finite.";
                return false;
            }
            if (!IsWithin(ExpectedAngleDegrees, -180, 180))
            {
                error = "ExpectedAngleDegrees must be within [-180, 180].";
                return false;
            }
            if (!IsWithin(AngleToleranceDegrees, 0, 45) || AngleToleranceDegrees == 0)
            {
                error = "AngleToleranceDegrees must be within (0, 45].";
                return false;
            }
            if (string.IsNullOrWhiteSpace(Name))
            {
                error = "Name cannot be blank.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinitePositive(double value) => double.IsFinite(value) && value > 0;

        private static bool IsFinite(FindCrossLocalPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

        private static bool IsFinite(FindCrossLocalDistortionOptions distortion)
        {
            if (!double.IsFinite(distortion.K1) || !double.IsFinite(distortion.K2) ||
                !double.IsFinite(distortion.P1) || !double.IsFinite(distortion.P2) || !double.IsFinite(distortion.K3))
            {
                return false;
            }

            bool anyIntrinsic = distortion.FxPixels.HasValue || distortion.FyPixels.HasValue ||
                distortion.PrincipalPointX.HasValue || distortion.PrincipalPointY.HasValue;
            if (!anyIntrinsic) return !distortion.Enabled;
            return HasCompleteFiniteIntrinsics(distortion);
        }

        private static bool HasCompleteFiniteIntrinsics(FindCrossLocalDistortionOptions distortion) =>
            distortion.FxPixels is > 0 && double.IsFinite(distortion.FxPixels.Value) &&
                distortion.FyPixels is > 0 && double.IsFinite(distortion.FyPixels.Value) &&
                distortion.PrincipalPointX.HasValue && double.IsFinite(distortion.PrincipalPointX.Value) &&
                distortion.PrincipalPointY.HasValue && double.IsFinite(distortion.PrincipalPointY.Value);

        private static bool IsWithin(double value, double minimum, double maximum) =>
            double.IsFinite(value) && value >= minimum && value <= maximum;

    }

    public sealed class FindCrossLocalItem
    {
        internal FindCrossLocalItem(
            string name,
            double x,
            double y,
            double width,
            double height,
            FindCrossLocalPoint center,
            double rotationAngle,
            FindCrossLocalTilt tilt)
        {
            Name = name;
            X = x;
            Y = y;
            W = width;
            H = height;
            Center = center;
            RotationAngle = rotationAngle;
            Tilt = tilt;
        }

        public string Name { get; }

        public double X { get; }

        public double Y { get; }

        public double W { get; }

        public double H { get; }

        public double Width => W;

        public double Height => H;

        public FindCrossLocalPoint Center { get; }

        public double RotationAngle { get; }

        public FindCrossLocalTilt Tilt { get; }

        public double TiltX => Tilt.X;

        public double TiltY => Tilt.Y;

        public bool ContainsCenter => Center.X >= X && Center.X < X + W && Center.Y >= Y && Center.Y < Y + H;
    }

    public sealed class FindCrossLocalSideQuality
    {
        internal FindCrossLocalSideQuality(
            string name,
            double? coverage,
            double? inlierRatio,
            double? contrastP10,
            double? fitRms,
            double? maxGap,
            double? confidence,
            int? sampleCount,
            int? inlierCount)
        {
            Name = name;
            Coverage = coverage;
            InlierRatio = inlierRatio;
            ContrastP10 = contrastP10;
            FitRms = fitRms;
            MaxGap = maxGap;
            Confidence = confidence;
            SampleCount = sampleCount;
            InlierCount = inlierCount;
        }

        public string Name { get; }

        public double? Coverage { get; }

        public double? InlierRatio { get; }

        public double? ContrastP10 { get; }

        public double? FitRms { get; }

        public double? MaxGap { get; }

        public double? Confidence { get; }

        public int? SampleCount { get; }

        public int? InlierCount { get; }
    }

    public sealed class FindCrossLocalEffectiveOptics
    {
        internal FindCrossLocalEffectiveOptics(
            FindCrossLocalPoint standardCenter,
            double focusLengthMillimeters,
            double sensorPixelSizeMicrometers,
            string standardCenterSource)
        {
            StandardCenter = standardCenter;
            FocusLengthMillimeters = focusLengthMillimeters;
            SensorPixelSizeMicrometers = sensorPixelSizeMicrometers;
            StandardCenterSource = standardCenterSource;
        }

        public FindCrossLocalPoint StandardCenter { get; }

        public double FocusLengthMillimeters { get; }

        public double SensorPixelSizeMicrometers { get; }

        public string StandardCenterSource { get; }
    }

    public sealed class FindCrossLocalDiagnostics
    {
        internal FindCrossLocalDiagnostics(
            bool success,
            string algorithm,
            string centerMethod,
            string rotationMethod,
            FindCrossLocalPoint? centerSubpixel,
            double? confidence,
            IReadOnlyList<FindCrossLocalPoint>? corners,
            IReadOnlyList<FindCrossLocalSideQuality>? sideQuality,
            double? topEdgeAngle,
            double? allEdgesAngle,
            FindCrossLocalPoint? rawGeometricCenter,
            FindCrossLocalPoint? appliedOffset,
            FindCrossLocalRectangle? effectiveRoi,
            IReadOnlyList<string>? ignoredParameters,
            FindCrossLocalEffectiveOptics? effectiveOptics,
            string? patternPolarity,
            IReadOnlyList<FindCrossLocalPoint>? armEndpoints,
            IReadOnlyList<FindCrossLocalPoint>? rawArmEndpoints,
            double? orthogonalityError,
            double? patternContrast,
            bool? distortionApplied,
            string failureReason,
            IReadOnlyList<string>? warnings)
        {
            Success = success;
            Algorithm = algorithm;
            CenterMethod = centerMethod;
            RotationMethod = rotationMethod;
            CenterSubpixel = centerSubpixel;
            Confidence = confidence;
            Corners = corners ?? Array.Empty<FindCrossLocalPoint>();
            SideQuality = sideQuality ?? Array.Empty<FindCrossLocalSideQuality>();
            TopEdgeAngle = topEdgeAngle;
            AllEdgesAngle = allEdgesAngle;
            RawGeometricCenter = rawGeometricCenter;
            AppliedOffset = appliedOffset;
            EffectiveRoi = effectiveRoi;
            IgnoredParameters = ignoredParameters ?? Array.Empty<string>();
            EffectiveOptics = effectiveOptics;
            PatternPolarity = patternPolarity;
            ArmEndpoints = armEndpoints ?? Array.Empty<FindCrossLocalPoint>();
            RawArmEndpoints = rawArmEndpoints ?? Array.Empty<FindCrossLocalPoint>();
            OrthogonalityError = orthogonalityError;
            PatternContrast = patternContrast;
            DistortionApplied = distortionApplied;
            FailureReason = failureReason;
            Warnings = warnings ?? Array.Empty<string>();
        }

        public bool Success { get; }

        public string Algorithm { get; }

        public string CenterMethod { get; }

        public string RotationMethod { get; }

        public FindCrossLocalPoint? CenterSubpixel { get; }

        public FindCrossLocalPoint? SubpixelCenter => CenterSubpixel;

        public double? Confidence { get; }

        public IReadOnlyList<FindCrossLocalPoint> Corners { get; }

        public IReadOnlyList<FindCrossLocalSideQuality> SideQuality { get; }

        public double? TopEdgeAngle { get; }

        public double? AllEdgesAngle { get; }

        public FindCrossLocalPoint? RawGeometricCenter { get; }

        public FindCrossLocalPoint? AppliedOffset { get; }

        public FindCrossLocalRectangle? EffectiveRoi { get; }

        public IReadOnlyList<string> IgnoredParameters { get; }

        public FindCrossLocalEffectiveOptics? EffectiveOptics { get; }

        public string? PatternPolarity { get; }

        public IReadOnlyList<FindCrossLocalPoint> ArmEndpoints { get; }

        public IReadOnlyList<FindCrossLocalPoint> RawArmEndpoints { get; }

        public double? OrthogonalityError { get; }

        public double? PatternContrast { get; }

        public bool? DistortionApplied { get; }

        public string FailureReason { get; }

        public IReadOnlyList<string> Warnings { get; }

        internal static FindCrossLocalDiagnostics CreateFailure(string reason, string algorithm = "M_FindCrossLocal") =>
            new(false, algorithm, string.Empty, string.Empty, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, reason, null);

        internal FindCrossLocalDiagnostics AsFailure(string reason) =>
            new(false, Algorithm, CenterMethod, RotationMethod, CenterSubpixel, Confidence, Corners, SideQuality,
                TopEdgeAngle, AllEdgesAngle, RawGeometricCenter, AppliedOffset, EffectiveRoi, IgnoredParameters,
                EffectiveOptics, PatternPolarity, ArmEndpoints, RawArmEndpoints, OrthogonalityError,
                PatternContrast, DistortionApplied, reason, Warnings);
    }

    public sealed class FindCrossLocalResult
    {
        internal FindCrossLocalResult(
            bool success,
            IReadOnlyList<FindCrossLocalItem>? items,
            FindCrossLocalDiagnostics diagnostics,
            string failureReason,
            string rawJson = "",
            int nativeReturnCode = 0,
            string interopDiagnostic = "")
        {
            Success = success;
            Items = items ?? Array.Empty<FindCrossLocalItem>();
            Diagnostics = diagnostics;
            FailureReason = failureReason;
            RawJson = rawJson;
            NativeReturnCode = nativeReturnCode;
            InteropDiagnostic = interopDiagnostic;
        }

        public bool Success { get; }

        public IReadOnlyList<FindCrossLocalItem> Items { get; }

        public FindCrossLocalDiagnostics Diagnostics { get; }

        public string Algorithm => Diagnostics.Algorithm;

        public string FailureReason { get; }

        public string RawJson { get; }

        public int NativeReturnCode { get; }

        public string InteropDiagnostic { get; }

        public bool HasSingleItem => Success && Items.Count == 1;

        public static FindCrossLocalResult CreateFailure(
            string failureReason,
            int nativeReturnCode = 0,
            string interopDiagnostic = "",
            string rawJson = "") =>
            new(false, null, FindCrossLocalDiagnostics.CreateFailure(failureReason), failureReason, rawJson, nativeReturnCode, interopDiagnostic);

        internal FindCrossLocalResult WithNativeContext(string rawJson, int nativeReturnCode) =>
            new(Success, Items, Diagnostics, FailureReason, rawJson, nativeReturnCode, InteropDiagnostic);

        internal FindCrossLocalResult AsInteropFailure(string reason, string detail) =>
            new(false, null, Diagnostics.AsFailure(reason), reason, RawJson, NativeReturnCode, detail);
    }

    public static class FindCrossLocalResultParser
    {
        public static bool TryParse(string? json, out FindCrossLocalResult result, out string error)
        {
            result = FindCrossLocalResult.CreateFailure("ResultParseFailed", rawJson: json ?? string.Empty);
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Result JSON is empty.";
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    error = "Result JSON must be an object.";
                    return false;
                }

                if (!TryGetProperty(root, "result", out JsonElement itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
                {
                    error = "Result JSON must contain a result array.";
                    return false;
                }

                List<FindCrossLocalItem> items = new();
                foreach (JsonElement itemElement in itemsElement.EnumerateArray())
                {
                    if (!TryReadItem(itemElement, out FindCrossLocalItem? item, out error))
                    {
                        return false;
                    }
                    items.Add(item!);
                }

                JsonElement diagnosticsElement = default;
                bool hasDiagnostics = TryGetProperty(root, "diagnostics", out diagnosticsElement);
                if (hasDiagnostics && diagnosticsElement.ValueKind != JsonValueKind.Object)
                {
                    error = "diagnostics must be an object when present.";
                    return false;
                }

                if (!TryReadOptionalBoolean(root, "Success", out bool? rootSuccess, out error))
                {
                    return false;
                }
                bool? diagnosticStatus = null;
                if (hasDiagnostics && !TryReadOptionalBoolean(diagnosticsElement, "Success", out diagnosticStatus, out error))
                {
                    return false;
                }
                if (rootSuccess.HasValue && diagnosticStatus.HasValue && rootSuccess.Value != diagnosticStatus.Value)
                {
                    error = "Root and diagnostics Success values disagree.";
                    return false;
                }

                bool success = rootSuccess ?? diagnosticStatus ?? items.Count > 0;
                if (success && items.Count == 0)
                {
                    error = "A successful result must contain at least one item.";
                    return false;
                }
                if (!success && items.Count > 0)
                {
                    error = "A failed result cannot contain legacy result items.";
                    return false;
                }

                string algorithm = ReadOptionalString(root, "Algorithm") ??
                    (hasDiagnostics ? ReadOptionalString(diagnosticsElement, "Algorithm") : null) ??
                    "LegacyFindCross";
                string failureReason = ReadOptionalString(root, "FailureReason") ??
                    (hasDiagnostics ? ReadOptionalString(diagnosticsElement, "FailureReason") : null) ??
                    string.Empty;
                if (!success && string.IsNullOrWhiteSpace(failureReason))
                {
                    failureReason = "UnknownFailure";
                }

                IReadOnlyList<string> warnings = ReadWarnings(root)
                    .Concat(hasDiagnostics ? ReadWarnings(diagnosticsElement) : Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (!TryReadDiagnostics(
                    hasDiagnostics ? diagnosticsElement : default,
                    hasDiagnostics,
                    success,
                    algorithm,
                    failureReason,
                    warnings,
                    out FindCrossLocalDiagnostics diagnostics,
                    out error))
                {
                    return false;
                }

                result = new FindCrossLocalResult(success, items, diagnostics, failureReason, json);
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
            catch (OverflowException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryReadItem(JsonElement element, out FindCrossLocalItem? item, out string error)
        {
            item = null;
            error = string.Empty;
            if (element.ValueKind != JsonValueKind.Object)
            {
                error = "Each result item must be an object.";
                return false;
            }

            string? name = ReadOptionalString(element, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "A result item is missing a non-empty name.";
                return false;
            }
            if (!TryReadRequiredFinite(element, "x", out double x, out error) ||
                !TryReadRequiredFinite(element, "y", out double y, out error) ||
                !TryReadRequiredFinite(element, "w", out double width, out error) ||
                !TryReadRequiredFinite(element, "h", out double height, out error) ||
                !TryReadRequiredFinite(element, "rotationAngle", out double rotationAngle, out error))
            {
                return false;
            }
            if (x < 0 || y < 0 || width <= 0 || height <= 0)
            {
                error = "Result item bounds must have non-negative origin and positive dimensions.";
                return false;
            }
            if (!TryGetProperty(element, "center", out JsonElement centerElement) ||
                !TryReadPoint(centerElement, out FindCrossLocalPoint center, out error))
            {
                error = string.IsNullOrEmpty(error) ? "Result item is missing center." : $"Invalid center: {error}";
                return false;
            }
            if (!TryGetProperty(element, "tilt", out JsonElement tiltElement) || tiltElement.ValueKind != JsonValueKind.Object ||
                !TryReadRequiredFinite(tiltElement, "tilt_x", out double tiltX, out error) ||
                !TryReadRequiredFinite(tiltElement, "tilt_y", out double tiltY, out error))
            {
                error = string.IsNullOrEmpty(error) ? "Result item is missing tilt." : $"Invalid tilt: {error}";
                return false;
            }

            item = new FindCrossLocalItem(name, x, y, width, height, center, rotationAngle, new FindCrossLocalTilt(tiltX, tiltY));
            if (!item.ContainsCenter)
            {
                error = "Result item center lies outside its bounding rectangle.";
                item = null;
                return false;
            }
            return true;
        }

        private static bool TryReadDiagnostics(
            JsonElement element,
            bool present,
            bool success,
            string algorithm,
            string failureReason,
            IReadOnlyList<string> warnings,
            out FindCrossLocalDiagnostics diagnostics,
            out string error)
        {
            diagnostics = new FindCrossLocalDiagnostics(
                success, algorithm, string.Empty, string.Empty, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, failureReason, warnings);
            error = string.Empty;
            if (!present)
            {
                return true;
            }

            string centerMethod = ReadOptionalString(element, "CenterMethod") ?? string.Empty;
            string rotationMethod = ReadOptionalString(element, "RotationMethod") ?? string.Empty;
            if (!TryReadOptionalFinite(element, "Confidence", out double? confidence, out error))
            {
                return false;
            }
            if (confidence.HasValue && (confidence.Value < 0 || confidence.Value > 1))
            {
                error = "diagnostics Confidence must be within [0, 1].";
                return false;
            }

            FindCrossLocalPoint? centerSubpixel = null;
            if (TryGetProperty(element, "CenterSubpixel", out JsonElement centerElement) ||
                TryGetProperty(element, "SubpixelCenter", out centerElement))
            {
                if (centerElement.ValueKind != JsonValueKind.Null)
                {
                    if (!TryReadPoint(centerElement, out FindCrossLocalPoint point, out error, allowNegative: true))
                    {
                        error = $"Invalid diagnostics subpixel center: {error}";
                        return false;
                    }
                    centerSubpixel = point;
                }
            }

            List<FindCrossLocalPoint> corners = new();
            if (TryGetProperty(element, "Corners", out JsonElement cornersElement))
            {
                if (cornersElement.ValueKind != JsonValueKind.Array)
                {
                    error = "diagnostics Corners must be an array.";
                    return false;
                }
                foreach (JsonElement cornerElement in cornersElement.EnumerateArray())
                {
                    if (!TryReadPoint(cornerElement, out FindCrossLocalPoint corner, out error, allowNegative: true))
                    {
                        error = $"Invalid diagnostics corner: {error}";
                        return false;
                    }
                    corners.Add(corner);
                }
            }

            List<FindCrossLocalSideQuality> sideQuality = new();
            if (TryGetProperty(element, "SideQuality", out JsonElement qualityElement))
            {
                if (qualityElement.ValueKind != JsonValueKind.Array)
                {
                    error = "diagnostics SideQuality must be an array.";
                    return false;
                }
                foreach (JsonElement sideElement in qualityElement.EnumerateArray())
                {
                    if (!TryReadSideQuality(sideElement, out FindCrossLocalSideQuality? side, out error))
                    {
                        return false;
                    }
                    sideQuality.Add(side!);
                }
            }

            if (!TryReadOptionalFinite(element, "TopEdgeAngle", out double? topEdgeAngle, out error) ||
                !TryReadOptionalFinite(element, "AllEdgesAngle", out double? allEdgesAngle, out error))
            {
                return false;
            }
            if (TryGetProperty(element, "RotationCandidates", out JsonElement candidatesElement))
            {
                if (candidatesElement.ValueKind != JsonValueKind.Null)
                {
                    if (candidatesElement.ValueKind != JsonValueKind.Object ||
                        !TryReadOptionalFinite(candidatesElement, "TopEdge", out double? candidateTop, out error) ||
                        !TryReadOptionalFinite(candidatesElement, "AllEdges", out double? candidateAll, out error))
                    {
                        error = string.IsNullOrEmpty(error) ? "diagnostics RotationCandidates must be an object." : error;
                        return false;
                    }
                    topEdgeAngle ??= candidateTop;
                    allEdgesAngle ??= candidateAll;
                }
            }

            if (!TryReadOptionalPoint(element, "RawGeometricCenter", true, out FindCrossLocalPoint? rawGeometricCenter, out error))
            {
                error = $"Invalid diagnostics RawGeometricCenter: {error}";
                return false;
            }
            if (!TryReadOptionalPoint(element, "AppliedOffset", true, out FindCrossLocalPoint? appliedOffset, out error))
            {
                error = $"Invalid diagnostics AppliedOffset: {error}";
                return false;
            }

            FindCrossLocalRectangle? effectiveRoi = null;
            if (TryGetProperty(element, "EffectiveRoi", out JsonElement roiElement) && roiElement.ValueKind != JsonValueKind.Null)
            {
                if (roiElement.ValueKind != JsonValueKind.Object ||
                    !TryReadRequiredFinite(roiElement, "x", out double roiX, out error) ||
                    !TryReadRequiredFinite(roiElement, "y", out double roiY, out error) ||
                    !TryReadRequiredFinite(roiElement, "w", out double roiWidth, out error) ||
                    !TryReadRequiredFinite(roiElement, "h", out double roiHeight, out error))
                {
                    error = string.IsNullOrEmpty(error) ? "diagnostics EffectiveRoi must be an object." : error;
                    return false;
                }
                if (roiX < 0 || roiY < 0 || roiWidth <= 0 || roiHeight <= 0)
                {
                    error = "diagnostics EffectiveRoi must have a non-negative origin and positive dimensions.";
                    return false;
                }
                effectiveRoi = new FindCrossLocalRectangle(roiX, roiY, roiWidth, roiHeight);
            }

            IReadOnlyList<string> ignoredParameters = Array.Empty<string>();
            if (TryGetProperty(element, "IgnoredParameters", out JsonElement ignoredElement))
            {
                if (ignoredElement.ValueKind != JsonValueKind.Array ||
                    ignoredElement.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String))
                {
                    error = "diagnostics IgnoredParameters must be an array of strings.";
                    return false;
                }
                ignoredParameters = ignoredElement.EnumerateArray()
                    .Select(value => value.GetString() ?? string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            FindCrossLocalEffectiveOptics? effectiveOptics = null;
            if (TryGetProperty(element, "EffectiveOptics", out JsonElement opticsElement) &&
                opticsElement.ValueKind != JsonValueKind.Null)
            {
                if (opticsElement.ValueKind != JsonValueKind.Object ||
                    !TryGetProperty(opticsElement, "StandardCenter", out JsonElement standardCenterElement) ||
                    !TryReadPoint(standardCenterElement, out FindCrossLocalPoint standardCenter, out error, allowNegative: true) ||
                    !TryReadRequiredFinite(opticsElement, "FocusLengthMm", out double focusLength, out error) ||
                    !TryReadRequiredFinite(opticsElement, "SensorPixelSizeUm", out double sensorPixelSize, out error))
                {
                    error = string.IsNullOrEmpty(error) ? "diagnostics EffectiveOptics is incomplete." : error;
                    return false;
                }
                string standardCenterSource = ReadOptionalString(opticsElement, "StandardCenterSource") ?? string.Empty;
                if (focusLength <= 0 || sensorPixelSize <= 0 || string.IsNullOrWhiteSpace(standardCenterSource))
                {
                    error = "diagnostics EffectiveOptics requires positive dimensions and a StandardCenterSource.";
                    return false;
                }
                effectiveOptics = new FindCrossLocalEffectiveOptics(
                    standardCenter, focusLength, sensorPixelSize, standardCenterSource);
            }

            if (!TryReadOptionalString(element, "PatternPolarity", out string? patternPolarity, out error) ||
                !TryReadOptionalPointArray(element, "ArmEndpoints", out IReadOnlyList<FindCrossLocalPoint> armEndpoints, out error) ||
                !TryReadOptionalPointArray(element, "RawArmEndpoints", out IReadOnlyList<FindCrossLocalPoint> rawArmEndpoints, out error) ||
                !TryReadOptionalFinite(element, "OrthogonalityError", out double? orthogonalityError, out error) ||
                !TryReadOptionalFinite(element, "PatternContrast", out double? patternContrast, out error) ||
                !TryReadOptionalBoolean(element, "DistortionApplied", out bool? distortionApplied, out error))
            {
                return false;
            }

            diagnostics = new FindCrossLocalDiagnostics(
                success,
                algorithm,
                centerMethod,
                rotationMethod,
                centerSubpixel,
                confidence,
                corners,
                sideQuality,
                topEdgeAngle,
                allEdgesAngle,
                rawGeometricCenter,
                appliedOffset,
                effectiveRoi,
                ignoredParameters,
                effectiveOptics,
                patternPolarity,
                armEndpoints,
                rawArmEndpoints,
                orthogonalityError,
                patternContrast,
                distortionApplied,
                failureReason,
                warnings);
            return true;
        }

        private static bool TryReadOptionalPointArray(
            JsonElement element,
            string name,
            out IReadOnlyList<FindCrossLocalPoint> points,
            out string error)
        {
            points = Array.Empty<FindCrossLocalPoint>();
            error = string.Empty;
            if (!TryGetProperty(element, name, out JsonElement pointsElement) || pointsElement.ValueKind == JsonValueKind.Null)
            {
                return true;
            }
            if (pointsElement.ValueKind != JsonValueKind.Array)
            {
                error = $"diagnostics {name} must be an array of points.";
                return false;
            }

            List<FindCrossLocalPoint> parsed = new();
            foreach (JsonElement pointElement in pointsElement.EnumerateArray())
            {
                if (!TryReadPoint(pointElement, out FindCrossLocalPoint point, out error, allowNegative: true))
                {
                    error = $"Invalid diagnostics {name} point: {error}";
                    return false;
                }
                parsed.Add(point);
            }
            points = parsed;
            return true;
        }

        private static bool TryReadOptionalPoint(
            JsonElement element,
            string name,
            bool allowNegative,
            out FindCrossLocalPoint? point,
            out string error)
        {
            point = null;
            error = string.Empty;
            if (!TryGetProperty(element, name, out JsonElement pointElement) || pointElement.ValueKind == JsonValueKind.Null)
            {
                return true;
            }
            if (!TryReadPoint(pointElement, out FindCrossLocalPoint parsed, out error, allowNegative))
            {
                return false;
            }
            point = parsed;
            return true;
        }

        private static bool TryReadSideQuality(
            JsonElement element,
            out FindCrossLocalSideQuality? quality,
            out string error)
        {
            quality = null;
            error = string.Empty;
            if (element.ValueKind != JsonValueKind.Object)
            {
                error = "Each SideQuality entry must be an object.";
                return false;
            }

            string name = ReadOptionalString(element, "Name") ?? string.Empty;
            if (!TryReadOptionalFinite(element, "Coverage", out double? coverage, out error) ||
                !TryReadOptionalFinite(element, "InlierRatio", out double? inlierRatio, out error) ||
                !TryReadOptionalFinite(element, "ContrastP10", out double? contrastP10, out error) ||
                !TryReadOptionalFinite(element, "FitRms", out double? fitRms, out error) ||
                !TryReadOptionalFinite(element, "MaxGap", out double? maxGap, out error) ||
                !TryReadOptionalFinite(element, "Confidence", out double? confidence, out error) ||
                !TryReadOptionalInteger(element, "SampleCount", out int? sampleCount, out error) ||
                !TryReadOptionalInteger(element, "InlierCount", out int? inlierCount, out error))
            {
                return false;
            }
            if ((coverage.HasValue && (coverage.Value < 0 || coverage.Value > 1)) ||
                (inlierRatio.HasValue && (inlierRatio.Value < 0 || inlierRatio.Value > 1)) ||
                (confidence.HasValue && (confidence.Value < 0 || confidence.Value > 1)) ||
                (fitRms.HasValue && fitRms.Value < 0) || (maxGap.HasValue && maxGap.Value < 0) ||
                (sampleCount.HasValue && sampleCount.Value < 0) || (inlierCount.HasValue && inlierCount.Value < 0))
            {
                error = "SideQuality contains an out-of-range metric.";
                return false;
            }

            quality = new FindCrossLocalSideQuality(
                name, coverage, inlierRatio, contrastP10, fitRms, maxGap, confidence, sampleCount, inlierCount);
            return true;
        }

        private static bool TryReadPoint(
            JsonElement element,
            out FindCrossLocalPoint point,
            out string error,
            bool allowNegative = false)
        {
            point = default;
            error = string.Empty;
            if (element.ValueKind != JsonValueKind.Object ||
                !TryReadRequiredFinite(element, "x", out double x, out error) ||
                !TryReadRequiredFinite(element, "y", out double y, out error))
            {
                error = string.IsNullOrEmpty(error) ? "Point must be an object with numeric x and y." : error;
                return false;
            }
            if (!allowNegative && (x < 0 || y < 0))
            {
                error = "Point coordinates cannot be negative.";
                return false;
            }
            point = new FindCrossLocalPoint(x, y);
            return true;
        }

        private static bool TryReadRequiredFinite(JsonElement element, string name, out double value, out string error)
        {
            value = default;
            error = string.Empty;
            if (!TryGetProperty(element, name, out JsonElement valueElement) ||
                valueElement.ValueKind != JsonValueKind.Number ||
                !valueElement.TryGetDouble(out value) ||
                !double.IsFinite(value))
            {
                error = $"Property {name} must be a finite number.";
                return false;
            }
            return true;
        }

        private static bool TryReadOptionalFinite(JsonElement element, string name, out double? value, out string error)
        {
            value = null;
            error = string.Empty;
            if (!TryGetProperty(element, name, out JsonElement valueElement) || valueElement.ValueKind == JsonValueKind.Null)
            {
                return true;
            }
            if (valueElement.ValueKind != JsonValueKind.Number || !valueElement.TryGetDouble(out double number) || !double.IsFinite(number))
            {
                error = $"Property {name} must be a finite number when present.";
                return false;
            }
            value = number;
            return true;
        }

        private static bool TryReadOptionalInteger(JsonElement element, string name, out int? value, out string error)
        {
            value = null;
            error = string.Empty;
            if (!TryGetProperty(element, name, out JsonElement valueElement) || valueElement.ValueKind == JsonValueKind.Null)
            {
                return true;
            }
            if (valueElement.ValueKind != JsonValueKind.Number || !valueElement.TryGetInt32(out int number))
            {
                error = $"Property {name} must be a 32-bit integer when present.";
                return false;
            }
            value = number;
            return true;
        }

        private static bool TryReadOptionalBoolean(JsonElement element, string name, out bool? value, out string error)
        {
            value = null;
            error = string.Empty;
            if (!TryGetProperty(element, name, out JsonElement valueElement) || valueElement.ValueKind == JsonValueKind.Null)
            {
                return true;
            }
            if (valueElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = $"Property {name} must be boolean when present.";
                return false;
            }
            value = valueElement.GetBoolean();
            return true;
        }

        private static bool TryReadOptionalString(JsonElement element, string name, out string? value, out string error)
        {
            value = null;
            error = string.Empty;
            if (!TryGetProperty(element, name, out JsonElement valueElement) || valueElement.ValueKind == JsonValueKind.Null)
            {
                return true;
            }
            if (valueElement.ValueKind != JsonValueKind.String)
            {
                error = $"Property {name} must be a string when present.";
                return false;
            }
            value = valueElement.GetString();
            return true;
        }

        private static string? ReadOptionalString(JsonElement element, string name)
        {
            return TryGetProperty(element, name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static string[] ReadWarnings(JsonElement element)
        {
            if (!TryGetProperty(element, "Warnings", out JsonElement warnings) || warnings.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }
            return warnings.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }
    }

    public static class FindCrossLocal
    {
        internal delegate int NativeJsonCall(out IntPtr result);

        internal delegate string NativeStringReader(IntPtr result);

        internal delegate int NativeResultReleaser(IntPtr result);

        internal delegate string NativeLastErrorReader();

        public static FindCrossLocalResult Run(HImage image, RoiRect roi, FindCrossLocalOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (!options.TryValidate(out string validationError))
            {
                return FindCrossLocalResult.CreateFailure("InvalidConfiguration", interopDiagnostic: validationError);
            }

            try
            {
                return RunJson(image, roi, options.ToJson());
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
            {
                return FindCrossLocalResult.CreateFailure("InvalidConfiguration", interopDiagnostic: ex.Message);
            }
        }

        public static FindCrossLocalResult RunJson(HImage image, RoiRect roi, string configJson)
        {
            if (!TryValidateConfigurationJson(configJson, out string error))
            {
                return FindCrossLocalResult.CreateFailure("InvalidConfigurationJson", interopDiagnostic: error);
            }

            return Invoke(
                (out IntPtr result) => OpenCVMediaHelper.M_FindCrossLocal(image, roi, configJson, out result),
                result => Marshal.PtrToStringUTF8(result) ?? string.Empty,
                OpenCVMediaHelper.FreeResult,
                ReadNativeLastError);
        }

        public static FindCrossLocalTilt CalculateTilt(
            FindCrossLocalPoint center,
            FindCrossLocalOpticsOptions optics)
        {
            ArgumentNullException.ThrowIfNull(optics);
            if (!optics.StandardCenter.HasValue)
            {
                throw new ArgumentException(
                    "A standard center is required for a standalone tilt calculation; image-center auto resolution is available only during Run.",
                    nameof(optics));
            }
            FindCrossLocalPoint standardCenter = optics.StandardCenter.Value;
            if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) ||
                !double.IsFinite(standardCenter.X) || !double.IsFinite(standardCenter.Y) ||
                !double.IsFinite(optics.FocusLengthMillimeters) || optics.FocusLengthMillimeters <= 0 ||
                !double.IsFinite(optics.SensorPixelSizeMicrometers) || optics.SensorPixelSizeMicrometers <= 0)
            {
                throw new ArgumentException("Center and optical dimensions must be finite, and dimensions must be positive.", nameof(optics));
            }

            double micrometersToMillimeters = 0.001;
            double xOnSensor = (center.X - standardCenter.X) * optics.SensorPixelSizeMicrometers * micrometersToMillimeters;
            double yOnSensor = (center.Y - standardCenter.Y) * optics.SensorPixelSizeMicrometers * micrometersToMillimeters;
            double radiansToDegrees = 180 / Math.PI;
            return new FindCrossLocalTilt(
                Math.Atan2(xOnSensor, optics.FocusLengthMillimeters) * radiansToDegrees,
                -Math.Atan2(yOnSensor, optics.FocusLengthMillimeters) * radiansToDegrees);
        }

        internal static FindCrossLocalResult InvokeForTest(
            NativeJsonCall call,
            NativeStringReader reader,
            NativeResultReleaser releaser) => Invoke(call, reader, releaser);

        internal static FindCrossLocalResult InvokeForTest(
            NativeJsonCall call,
            NativeStringReader reader,
            NativeResultReleaser releaser,
            NativeLastErrorReader lastErrorReader) => Invoke(call, reader, releaser, lastErrorReader);

        private static FindCrossLocalResult Invoke(
            NativeJsonCall call,
            NativeStringReader reader,
            NativeResultReleaser releaser,
            NativeLastErrorReader? lastErrorReader = null)
        {
            IntPtr resultPointer = IntPtr.Zero;
            FindCrossLocalResult result;
            Exception? releaseException = null;
            int releaseReturnCode = 0;
            try
            {
                int returnCode = call(out resultPointer);
                if (returnCode <= 0 || resultPointer == IntPtr.Zero)
                {
                    string diagnostic = resultPointer == IntPtr.Zero
                        ? $"M_FindCrossLocal returned {returnCode} with a null result pointer."
                        : $"M_FindCrossLocal returned failure code {returnCode}.";
                    string nativeError = TryReadNativeLastError(lastErrorReader);
                    if (!string.IsNullOrWhiteSpace(nativeError))
                    {
                        diagnostic += $" Native error: {nativeError}";
                    }
                    result = FindCrossLocalResult.CreateFailure(
                        GetNativeFailureReason(returnCode),
                        returnCode,
                        diagnostic);
                }
                else
                {
                    string json = reader(resultPointer);
                    if (!FindCrossLocalResultParser.TryParse(json, out FindCrossLocalResult parsed, out string parseError))
                    {
                        result = FindCrossLocalResult.CreateFailure("ResultParseFailed", returnCode, parseError, json);
                    }
                    else
                    {
                        result = parsed.WithNativeContext(json, returnCode);
                    }
                }
            }
            catch (DllNotFoundException ex)
            {
                result = FindCrossLocalResult.CreateFailure("NativeLibraryUnavailable", interopDiagnostic: ex.Message);
            }
            catch (EntryPointNotFoundException ex)
            {
                result = FindCrossLocalResult.CreateFailure("NativeEntryPointUnavailable", interopDiagnostic: ex.Message);
            }
            catch (BadImageFormatException ex)
            {
                result = FindCrossLocalResult.CreateFailure("NativeLibraryIncompatible", interopDiagnostic: ex.Message);
            }
            catch (Exception ex) when (ex is MarshalDirectiveException or SEHException)
            {
                result = FindCrossLocalResult.CreateFailure("NativeAbiMismatch", interopDiagnostic: ex.Message);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or DecoderFallbackException)
            {
                result = FindCrossLocalResult.CreateFailure("ManagedInteropFailed", interopDiagnostic: ex.Message);
            }
            finally
            {
                if (resultPointer != IntPtr.Zero)
                {
                    try
                    {
                        releaseReturnCode = releaser(resultPointer);
                    }
                    catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or
                        MarshalDirectiveException or SEHException or InvalidOperationException)
                    {
                        releaseException = ex;
                    }
                }
            }

            if (releaseException != null || releaseReturnCode != 0)
            {
                string reason = releaseException switch
                {
                    DllNotFoundException => "NativeLibraryUnavailable",
                    EntryPointNotFoundException => "NativeFreeEntryPointUnavailable",
                    BadImageFormatException => "NativeLibraryIncompatible",
                    MarshalDirectiveException or SEHException => "NativeAbiMismatch",
                    _ => "NativeResultReleaseFailed"
                };
                string detail = releaseException != null
                    ? $"FreeResult failed: {releaseException.Message}"
                    : $"FreeResult returned failure code {releaseReturnCode}.";
                return result.AsInteropFailure(reason, detail);
            }
            return result;
        }

        private static string ReadNativeLastError()
        {
            const int maximumErrorBytes = 64 * 1024;
            int required = OpenCVMediaHelper.M_FindCrossLocalGetLastError(null, 0);
            for (int attempt = 0; attempt < 3 && required > 1 && required <= maximumErrorBytes; attempt++)
            {
                byte[] buffer = new byte[required];
                int result = OpenCVMediaHelper.M_FindCrossLocalGetLastError(buffer, checked((uint)buffer.Length));
                if (result <= 1) return string.Empty;
                if (result <= buffer.Length) return Encoding.UTF8.GetString(buffer, 0, result - 1);
                required = result;
            }
            return string.Empty;
        }

        private static string TryReadNativeLastError(NativeLastErrorReader? reader)
        {
            if (reader == null) return string.Empty;
            try
            {
                return reader() ?? string.Empty;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or
                BadImageFormatException or MarshalDirectiveException or SEHException or
                InvalidOperationException or ArgumentException or DecoderFallbackException)
            {
                // The primary native return code remains authoritative. Older DLLs do
                // not expose the optional diagnostic entry point, so a failed lookup
                // must not hide the original FindCross error.
                return string.Empty;
            }
        }

        private static bool TryValidateConfigurationJson(string json, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Configuration JSON is empty.";
                return false;
            }
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    error = "Configuration JSON must be an object.";
                    return false;
                }
                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string GetNativeFailureReason(int returnCode) => returnCode switch
        {
            -1 => "NativeInvalidArgument",
            -3 => "NativeAllocationFailed",
            -4 => "NativeConfigurationInvalid",
            -5 => "NativeOpenCvError",
            -6 or -7 => "NativeProcessingFailed",
            _ => "NativeCallFailed"
        };
    }
}
