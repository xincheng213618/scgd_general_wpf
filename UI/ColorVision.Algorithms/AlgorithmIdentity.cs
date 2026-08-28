using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Algorithms;

/// <summary>A stable, case-insensitive algorithm identity persisted by invocations and compatibility adapters.</summary>
[JsonConverter(typeof(AlgorithmIdJsonConverter))]
public readonly record struct AlgorithmId
{
    public AlgorithmId(string value)
    {
        Value = Normalize(value);
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 128 || normalized.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_')))
        {
            throw new ArgumentException("Algorithm IDs may contain only lowercase ASCII letters, digits, '.', '-' and '_'.", nameof(value));
        }

        return normalized;
    }
}

/// <summary>Semantic version for an algorithm's behavior and result contract.</summary>
[JsonConverter(typeof(AlgorithmVersionJsonConverter))]
public readonly record struct AlgorithmVersion : IComparable<AlgorithmVersion>
{
    public AlgorithmVersion(int major, int minor, int patch)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        ArgumentOutOfRangeException.ThrowIfNegative(patch);
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public static bool operator <(AlgorithmVersion left, AlgorithmVersion right) => left.CompareTo(right) < 0;

    public static bool operator <=(AlgorithmVersion left, AlgorithmVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >(AlgorithmVersion left, AlgorithmVersion right) => left.CompareTo(right) > 0;

    public static bool operator >=(AlgorithmVersion left, AlgorithmVersion right) => left.CompareTo(right) >= 0;

    public static AlgorithmVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string[] parts = value.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int patch)
            || major < 0 || minor < 0 || patch < 0)
        {
            throw new FormatException($"Invalid algorithm version '{value}'. Expected MAJOR.MINOR.PATCH.");
        }

        return new AlgorithmVersion(major, minor, patch);
    }

    public int CompareTo(AlgorithmVersion other)
    {
        int comparison = Major.CompareTo(other.Major);
        if (comparison != 0) return comparison;
        comparison = Minor.CompareTo(other.Minor);
        return comparison != 0 ? comparison : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

internal sealed class AlgorithmIdJsonConverter : JsonConverter<AlgorithmId>
{
    public override AlgorithmId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return new AlgorithmId(reader.GetString()!);
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && TryGetProperty(document.RootElement, "value", out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return new AlgorithmId(value.GetString()!);
        }
        throw new JsonException("AlgorithmId must be a string or an object with a string value property.");
    }

    public override void Write(Utf8JsonWriter writer, AlgorithmId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
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

internal sealed class AlgorithmVersionJsonConverter : JsonConverter<AlgorithmVersion>
{
    public override AlgorithmVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return AlgorithmVersion.Parse(reader.GetString()!);
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && TryGetProperty(root, "major", out JsonElement major)
            && TryGetProperty(root, "minor", out JsonElement minor)
            && TryGetProperty(root, "patch", out JsonElement patch))
        {
            return new AlgorithmVersion(major.GetInt32(), minor.GetInt32(), patch.GetInt32());
        }
        throw new JsonException("AlgorithmVersion must be a semantic-version string or an object with major, minor and patch properties.");
    }

    public override void Write(Utf8JsonWriter writer, AlgorithmVersion value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
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

public enum AlgorithmExecutionPlane
{
    Local,
    RemoteDevice,
}

[Flags]
public enum AlgorithmHostCapabilities
{
    None = 0,
    Interactive = 1 << 0,
    Batch = 1 << 1,
    Flow = 1 << 2,
    Headless = 1 << 3,
    Local = 1 << 4,
    Deterministic = 1 << 5,
    Copilot = 1 << 6,
    MultiInput = 1 << 7,
    Roi = 1 << 8,
    RemoteDevice = 1 << 9,
}

public enum AlgorithmProviderKind
{
    Cpu,
    Native,
    Gpu,
    Remote,
}

/// <summary>
/// Canonical, provider-neutral pixel layouts. Color samples are always interleaved BGR;
/// four-channel layouts use a meaningful, straight (non-premultiplied) alpha channel.
/// UI/native adapters must normalize RGB, BGRX, premultiplied-alpha and indexed inputs
/// before constructing an <see cref="AlgorithmImageBuffer"/>.
/// </summary>
public enum AlgorithmImageFormat
{
    Gray8,
    Gray16,
    Gray32Float,
    /// <summary>Interleaved B, G, R bytes.</summary>
    Bgr24,
    /// <summary>Interleaved 16-bit B, G, R samples.</summary>
    Bgr48,
    /// <summary>Interleaved 32-bit floating-point B, G, R samples.</summary>
    Bgr96Float,
    /// <summary>Interleaved B, G, R and straight alpha bytes.</summary>
    Bgra32,
    /// <summary>Interleaved 16-bit B, G, R and straight alpha samples.</summary>
    Bgra64,
    /// <summary>Interleaved 32-bit floating-point B, G, R and straight alpha samples.</summary>
    Bgra128Float,
}

public static class AlgorithmImageFormatExtensions
{
    public static int Channels(this AlgorithmImageFormat format) => format switch
    {
        AlgorithmImageFormat.Gray8 or AlgorithmImageFormat.Gray16 or AlgorithmImageFormat.Gray32Float => 1,
        AlgorithmImageFormat.Bgr24 or AlgorithmImageFormat.Bgr48 or AlgorithmImageFormat.Bgr96Float => 3,
        AlgorithmImageFormat.Bgra32 or AlgorithmImageFormat.Bgra64 or AlgorithmImageFormat.Bgra128Float => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static int BitsPerChannel(this AlgorithmImageFormat format) => format switch
    {
        AlgorithmImageFormat.Gray8 or AlgorithmImageFormat.Bgr24 or AlgorithmImageFormat.Bgra32 => 8,
        AlgorithmImageFormat.Gray16 or AlgorithmImageFormat.Bgr48 or AlgorithmImageFormat.Bgra64 => 16,
        AlgorithmImageFormat.Gray32Float or AlgorithmImageFormat.Bgr96Float or AlgorithmImageFormat.Bgra128Float => 32,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static int BytesPerPixel(this AlgorithmImageFormat format) => checked(format.Channels() * format.BitsPerChannel() / 8);

    public static bool IsFloatingPoint(this AlgorithmImageFormat format)
        => format is AlgorithmImageFormat.Gray32Float or AlgorithmImageFormat.Bgr96Float or AlgorithmImageFormat.Bgra128Float;
}
