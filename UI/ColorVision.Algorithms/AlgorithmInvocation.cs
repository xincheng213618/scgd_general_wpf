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
