using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Algorithms;

public sealed record AlgorithmValidationIssue(string Path, string Code, string Message);

public sealed class AlgorithmValidationResult
{
    private readonly List<AlgorithmValidationIssue> _issues = new();

    public IReadOnlyList<AlgorithmValidationIssue> Issues => _issues;

    public bool IsValid => _issues.Count == 0;

    public void Add(string path, string code, string message) => _issues.Add(new AlgorithmValidationIssue(path, code, message));

    public static AlgorithmValidationResult Valid() => new();
}

/// <summary>Implemented by stable parameter contracts. Validation must not mutate the instance.</summary>
public interface IAlgorithmParameters
{
    int SchemaVersion { get; }

    AlgorithmValidationResult Validate();
}

public sealed record AlgorithmParameterField(
    string Name,
    string ValueType,
    JsonElement DefaultValue,
    bool Required = true,
    double? Minimum = null,
    double? Maximum = null,
    IReadOnlyList<string>? AllowedValues = null,
    string? Unit = null,
    string? Description = null);

public sealed record AlgorithmParameterSchema(
    int Version,
    IReadOnlyList<AlgorithmParameterField> Fields,
    JsonElement Defaults);

public interface IAlgorithmParameterMigrator
{
    AlgorithmId AlgorithmId { get; }

    int FromSchemaVersion { get; }

    int ToSchemaVersion { get; }

    JsonElement Migrate(JsonElement parameters);
}

public static class AlgorithmJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static JsonElement ToElement<T>(T value)
        => value == null
            ? JsonSerializer.SerializeToElement<object?>(null, Options)
            : JsonSerializer.SerializeToElement(value, value.GetType(), Options);

    public static T Deserialize<T>(JsonElement value) where T : notnull
        => value.Deserialize<T>(Options) ?? throw new JsonException($"Could not deserialize {typeof(T).Name}.");

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
