using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Algorithms;

public enum AlgorithmCoordinateSpace
{
    /// <summary>Pixels use a top-left origin; integer coordinates address pixel centers and rectangles are half-open.</summary>
    Pixel,
    /// <summary>Physical coordinates are millimetres, with the same top-left origin and axis direction as pixel space.</summary>
    Physical,
}

public readonly record struct AlgorithmPoint(double X, double Y);

public static class AlgorithmCoordinates
{
    private const double MillimetresPerInch = 25.4;

    public static AlgorithmPoint ToPixel(AlgorithmPoint point, AlgorithmCoordinateSpace space, double dpiX, double dpiY)
    {
        ValidateDpi(dpiX, dpiY);
        return space == AlgorithmCoordinateSpace.Pixel
            ? point
            : new AlgorithmPoint(
                SnapNearInteger(point.X * dpiX / MillimetresPerInch),
                SnapNearInteger(point.Y * dpiY / MillimetresPerInch));
    }

    public static AlgorithmPoint FromPixel(AlgorithmPoint point, AlgorithmCoordinateSpace space, double dpiX, double dpiY)
    {
        ValidateDpi(dpiX, dpiY);
        return space == AlgorithmCoordinateSpace.Pixel
            ? point
            : new AlgorithmPoint(point.X * MillimetresPerInch / dpiX, point.Y * MillimetresPerInch / dpiY);
    }

    private static void ValidateDpi(double dpiX, double dpiY)
    {
        if (!double.IsFinite(dpiX) || dpiX <= 0) throw new ArgumentOutOfRangeException(nameof(dpiX));
        if (!double.IsFinite(dpiY) || dpiY <= 0) throw new ArgumentOutOfRangeException(nameof(dpiY));
    }

    private static double SnapNearInteger(double value)
    {
        double rounded = Math.Round(value);
        return Math.Abs(value - rounded) <= 1e-10 * Math.Max(1, Math.Abs(value)) ? rounded : value;
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(RectangleAlgorithmRoi), "rectangle")]
[JsonDerivedType(typeof(CircleAlgorithmRoi), "circle")]
[JsonDerivedType(typeof(PolygonAlgorithmRoi), "polygon")]
[JsonDerivedType(typeof(PolylineAlgorithmRoi), "polyline")]
public abstract record AlgorithmRoi
{
    public AlgorithmCoordinateSpace CoordinateSpace { get; init; } = AlgorithmCoordinateSpace.Pixel;

    public abstract AlgorithmValidationResult Validate();
}

public sealed record RectangleAlgorithmRoi(double X, double Y, double Width, double Height) : AlgorithmRoi
{
    public override AlgorithmValidationResult Validate()
    {
        AlgorithmValidationResult result = new();
        if (!double.IsFinite(X) || !double.IsFinite(Y)) result.Add("roi", "invalid_origin", "ROI origin must be finite.");
        if (!double.IsFinite(Width) || Width <= 0) result.Add("roi.width", "invalid_size", "ROI width must be positive and finite.");
        if (!double.IsFinite(Height) || Height <= 0) result.Add("roi.height", "invalid_size", "ROI height must be positive and finite.");
        return result;
    }
}

public sealed record CircleAlgorithmRoi(AlgorithmPoint Center, double Radius) : AlgorithmRoi
{
    public override AlgorithmValidationResult Validate()
    {
        AlgorithmValidationResult result = new();
        if (!double.IsFinite(Center.X) || !double.IsFinite(Center.Y)) result.Add("roi.center", "invalid_center", "Circle center must be finite.");
        if (!double.IsFinite(Radius) || Radius <= 0) result.Add("roi.radius", "invalid_radius", "Circle radius must be positive and finite.");
        return result;
    }
}

public sealed record PolygonAlgorithmRoi(IReadOnlyList<AlgorithmPoint> Points) : AlgorithmRoi
{
    public override AlgorithmValidationResult Validate() => ValidatePoints(Points, 3, "polygon");

    internal static AlgorithmValidationResult ValidatePoints(IReadOnlyList<AlgorithmPoint>? points, int minimum, string kind)
    {
        AlgorithmValidationResult result = new();
        if (points == null || points.Count < minimum)
        {
            result.Add("roi.points", "too_few_points", $"A {kind} requires at least {minimum} points.");
            return result;
        }

        if (points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            result.Add("roi.points", "invalid_point", "ROI points must be finite.");
        return result;
    }
}

public sealed record PolylineAlgorithmRoi(IReadOnlyList<AlgorithmPoint> Points) : AlgorithmRoi
{
    public override AlgorithmValidationResult Validate() => PolygonAlgorithmRoi.ValidatePoints(Points, 2, "polyline");
}

public sealed record AlgorithmInputReference(string Name, string? Uri = null, string? Revision = null, string? Checksum = null);

public sealed class AlgorithmInvocation
{
    public Guid InvocationId { get; init; } = Guid.NewGuid();

    public AlgorithmId AlgorithmId { get; init; }

    public AlgorithmVersion? AlgorithmVersion { get; init; }

    public int ParameterSchemaVersion { get; init; } = 1;

    public JsonElement Parameters { get; init; }

    public IReadOnlyList<AlgorithmInputReference> Inputs { get; init; } = Array.Empty<AlgorithmInputReference>();

    public AlgorithmRoi? Roi { get; init; }

    public string? PresetId { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    [JsonIgnore]
    public bool HasParameters => Parameters.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;

    public static AlgorithmInvocation Create<TParameters>(AlgorithmId algorithmId, TParameters parameters, AlgorithmRoi? roi = null)
        where TParameters : IAlgorithmParameters
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new AlgorithmInvocation
        {
            AlgorithmId = algorithmId,
            ParameterSchemaVersion = parameters.SchemaVersion,
            Parameters = AlgorithmJson.ToElement(parameters),
            Roi = roi,
        };
    }
}

/// <summary>A provider-neutral, serializable parameter preset that can recreate an invocation without embedding host state.</summary>
public sealed class AlgorithmParameterPreset
{
    public const string CurrentSchema = "colorvision.algorithm-parameter-preset/v1";
    public const int MaximumParameterJsonCharacters = 1_048_576;
    public const int MaximumParameterJsonDepth = 32;
    public const int MaximumParameterJsonNodes = 65_536;
    public const int MaximumPresetIdLength = 256;
    public const int MaximumMetadataEntries = 128;
    public const int MaximumMetadataKeyLength = 128;
    public const int MaximumMetadataValueLength = 4_096;
    public const int MaximumMetadataCharacters = 65_536;

    public string Schema { get; init; } = CurrentSchema;

    public required string PresetId { get; init; }

    public required AlgorithmId AlgorithmId { get; init; }

    public AlgorithmVersion? AlgorithmVersion { get; init; }

    public int ParameterSchemaVersion { get; init; } = 1;

    public JsonElement Parameters { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public static AlgorithmParameterPreset Create<TParameters>(
        string presetId,
        AlgorithmId algorithmId,
        AlgorithmVersion algorithmVersion,
        TParameters parameters,
        IReadOnlyDictionary<string, string>? metadata = null)
        where TParameters : IAlgorithmParameters
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetId);
        ArgumentNullException.ThrowIfNull(parameters);
        return new AlgorithmParameterPreset
        {
            PresetId = presetId,
            AlgorithmId = algorithmId,
            AlgorithmVersion = algorithmVersion,
            ParameterSchemaVersion = parameters.SchemaVersion,
            Parameters = AlgorithmJson.ToElement(parameters),
            Metadata = metadata == null
                ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
                : new ReadOnlyDictionary<string, string>(metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)),
        };
    }

    public AlgorithmValidationResult Validate()
    {
        AlgorithmValidationResult result = new();
        if (!string.Equals(Schema, CurrentSchema, StringComparison.Ordinal))
            result.Add(nameof(Schema), "unsupported_preset_schema", $"Preset schema '{Schema}' is not supported.");
        if (string.IsNullOrWhiteSpace(PresetId)) result.Add(nameof(PresetId), "required", "PresetId is required.");
        else if (PresetId.Length > MaximumPresetIdLength)
            result.Add(nameof(PresetId), "preset_id_too_long", $"PresetId cannot exceed {MaximumPresetIdLength} characters.");
        if (string.IsNullOrWhiteSpace(AlgorithmId.Value)) result.Add(nameof(AlgorithmId), "required", "AlgorithmId is required.");
        if (AlgorithmVersion == null) result.Add(nameof(AlgorithmVersion), "required", "AlgorithmVersion is required.");
        if (ParameterSchemaVersion < 1) result.Add(nameof(ParameterSchemaVersion), "out_of_range", "ParameterSchemaVersion must be positive.");
        if (Parameters.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            result.Add(nameof(Parameters), "required", "Preset parameters are required.");
        else if (Parameters.ValueKind != JsonValueKind.Object)
            result.Add(nameof(Parameters), "parameters_not_object", "Preset parameters must be a JSON object.");
        else
            ValidateParameters(Parameters, result);
        ValidateMetadata(Metadata, result);
        return result;
    }

    public AlgorithmInvocation ToInvocation(AlgorithmRoi? roi = null)
    {
        AlgorithmParameterPreset snapshot = CreateSnapshot();
        AlgorithmValidationResult validation = snapshot.Validate();
        if (!validation.IsValid) throw new InvalidOperationException(string.Join("; ", validation.Issues));
        return new AlgorithmInvocation
        {
            AlgorithmId = snapshot.AlgorithmId,
            AlgorithmVersion = snapshot.AlgorithmVersion,
            ParameterSchemaVersion = snapshot.ParameterSchemaVersion,
            Parameters = snapshot.Parameters,
            Roi = roi,
            PresetId = snapshot.PresetId,
            Metadata = snapshot.Metadata,
        };
    }

    private AlgorithmParameterPreset CreateSnapshot()
    {
        JsonElement parameters;
        IReadOnlyDictionary<string, string> metadata;
        try
        {
            parameters = Parameters.ValueKind == JsonValueKind.Undefined ? default : Parameters.Clone();
            metadata = Metadata == null
                ? null!
                : new ReadOnlyDictionary<string, string>(Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new InvalidOperationException("The preset changed while an immutable invocation snapshot was being created.", exception);
        }

        return new AlgorithmParameterPreset
        {
            Schema = Schema,
            PresetId = PresetId,
            AlgorithmId = AlgorithmId,
            AlgorithmVersion = AlgorithmVersion,
            ParameterSchemaVersion = ParameterSchemaVersion,
            Parameters = parameters,
            Metadata = metadata,
        };
    }

    private static void ValidateParameters(JsonElement parameters, AlgorithmValidationResult result)
    {
        string rawJson;
        try
        {
            rawJson = parameters.GetRawText();
        }
        catch (InvalidOperationException)
        {
            result.Add(nameof(Parameters), "parameters_unreadable", "Preset parameter JSON is no longer readable.");
            return;
        }
        if (rawJson.Length > MaximumParameterJsonCharacters)
        {
            result.Add(nameof(Parameters), "json_size_exceeded",
                $"Preset parameter JSON exceeds the {MaximumParameterJsonCharacters} character limit.");
            return;
        }

        int nodeCount = 0;
        bool depthExceeded = false;
        bool nodeCountExceeded = false;
        bool nonFinite = false;
        bool duplicateProperty = false;
        Stack<(JsonElement Value, int Depth)> pending = new();
        pending.Push((parameters, 1));
        while (pending.Count > 0)
        {
            (JsonElement value, int depth) = pending.Pop();
            if (++nodeCount > MaximumParameterJsonNodes)
            {
                nodeCountExceeded = true;
                break;
            }
            if (depth > MaximumParameterJsonDepth)
            {
                depthExceeded = true;
                break;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    HashSet<string> names = new(StringComparer.Ordinal);
                    foreach (JsonProperty property in value.EnumerateObject())
                    {
                        if (!names.Add(property.Name)) duplicateProperty = true;
                        pending.Push((property.Value, depth + 1));
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement item in value.EnumerateArray()) pending.Push((item, depth + 1));
                    break;
                case JsonValueKind.Number:
                    if (!value.TryGetDouble(out double number) || !double.IsFinite(number)) nonFinite = true;
                    break;
                case JsonValueKind.String:
                    string? text = value.GetString();
                    if (string.Equals(text, "NaN", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(text, "Infinity", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(text, "+Infinity", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(text, "-Infinity", StringComparison.OrdinalIgnoreCase))
                    {
                        nonFinite = true;
                    }
                    break;
            }
        }

        if (depthExceeded)
            result.Add(nameof(Parameters), "json_depth_exceeded", $"Preset parameter JSON exceeds the maximum depth of {MaximumParameterJsonDepth}.");
        if (nodeCountExceeded)
            result.Add(nameof(Parameters), "json_node_count_exceeded", $"Preset parameter JSON exceeds the {MaximumParameterJsonNodes} node limit.");
        if (nonFinite)
            result.Add(nameof(Parameters), "non_finite_number", "Preset parameters cannot contain NaN or infinity.");
        if (duplicateProperty)
            result.Add(nameof(Parameters), "duplicate_parameter_property", "Preset parameter objects cannot contain duplicate property names.");
    }

    private static void ValidateMetadata(IReadOnlyDictionary<string, string>? metadata, AlgorithmValidationResult result)
    {
        if (metadata == null)
        {
            result.Add(nameof(Metadata), "required", "Preset Metadata cannot be null.");
            return;
        }

        long totalCharacters = 0;
        try
        {
            if (metadata.Count > MaximumMetadataEntries)
                result.Add(nameof(Metadata), "metadata_count_exceeded", $"Preset Metadata cannot contain more than {MaximumMetadataEntries} entries.");

            foreach ((string key, string value) in metadata)
            {
                if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsControl))
                    result.Add(nameof(Metadata), "metadata_key_invalid", "Preset Metadata keys must be non-empty and cannot contain control characters.");
                else if (key.Length > MaximumMetadataKeyLength)
                    result.Add(nameof(Metadata), "metadata_key_too_long", $"Preset Metadata keys cannot exceed {MaximumMetadataKeyLength} characters.");

                if (value == null || value.Any(char.IsControl))
                    result.Add(nameof(Metadata), "metadata_value_invalid", "Preset Metadata values cannot be null or contain control characters.");
                else if (value.Length > MaximumMetadataValueLength)
                    result.Add(nameof(Metadata), "metadata_value_too_long", $"Preset Metadata values cannot exceed {MaximumMetadataValueLength} characters.");

                totalCharacters += key?.Length ?? 0;
                totalCharacters += value?.Length ?? 0;
            }
        }
        catch (InvalidOperationException)
        {
            result.Add(nameof(Metadata), "metadata_changed", "Preset Metadata changed while it was being validated.");
            return;
        }

        if (totalCharacters > MaximumMetadataCharacters)
            result.Add(nameof(Metadata), "metadata_size_exceeded", $"Preset Metadata exceeds the {MaximumMetadataCharacters} character aggregate limit.");
    }
}
