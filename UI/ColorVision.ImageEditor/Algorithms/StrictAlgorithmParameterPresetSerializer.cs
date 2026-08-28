using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms
{
    internal static class StrictAlgorithmParameterPresetSerializer
    {
        private static readonly string[] EnvelopeFields =
        [
            "schema",
            "presetId",
            "algorithmId",
            "algorithmVersion",
            "parameterSchemaVersion",
            "parameters",
            "metadata",
        ];

        public static (string PresetId, TParameters Parameters) Deserialize<TParameters>(
            string json,
            AlgorithmDescriptor descriptor)
            where TParameters : IAlgorithmParameters
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            ArgumentNullException.ThrowIfNull(descriptor);

            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("The preset document must be an object.");

            RejectDuplicateProperties(root, "$");
            ValidateObjectShape(root, EnvelopeFields, EnvelopeFields, "$");
            JsonElement parameterElement = GetRequiredProperty(root, "parameters", "$");
            if (parameterElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Preset property '$.parameters' must be an object.");

            Dictionary<string, AlgorithmParameterField> fields = descriptor.ParameterSchema.Fields.ToDictionary(
                field => JsonNamingPolicy.CamelCase.ConvertName(field.Name),
                StringComparer.OrdinalIgnoreCase);
            ValidateObjectShape(
                parameterElement,
                fields.Keys,
                fields.Where(pair => pair.Value.Required).Select(pair => pair.Key),
                "$.parameters");
            ValidateParameterValueKinds(parameterElement, fields);

            AlgorithmParameterPreset preset = JsonSerializer.Deserialize<AlgorithmParameterPreset>(json, AlgorithmJson.Options)
                ?? throw new JsonException("The preset document is empty.");
            AlgorithmValidationResult validation = preset.Validate();
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("; ", validation.Issues));
            if (preset.AlgorithmId != descriptor.Id)
                throw new InvalidOperationException($"Preset algorithm '{preset.AlgorithmId}' does not match '{descriptor.Id}'.");
            if (!preset.AlgorithmVersion.HasValue)
                throw new InvalidOperationException("Preset algorithm version is required.");
            if (preset.AlgorithmVersion.Value.Major != descriptor.Version.Major)
                throw new InvalidOperationException($"Preset algorithm version '{preset.AlgorithmVersion}' is not major-compatible with '{descriptor.Version}'.");
            if (preset.ParameterSchemaVersion != descriptor.ParameterSchema.Version)
                throw new InvalidOperationException($"Preset parameter schema {preset.ParameterSchemaVersion} does not match {descriptor.ParameterSchema.Version}.");

            TParameters parameters = preset.Parameters.Deserialize<TParameters>(AlgorithmJson.Options)
                ?? throw new JsonException("The preset parameters are empty.");
            validation = parameters.Validate();
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("; ", validation.Issues));
            return (preset.PresetId, parameters);
        }

        private static void ValidateObjectShape(
            JsonElement element,
            IEnumerable<string> allowedNames,
            IEnumerable<string> requiredNames,
            string path)
        {
            HashSet<string> allowed = new(allowedNames, StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                    throw new JsonException($"Unknown preset property '{path}.{property.Name}'.");
            }

            foreach (string required in requiredNames)
            {
                if (!TryGetProperty(element, required, out _))
                    throw new JsonException($"Required preset property '{path}.{required}' is missing.");
            }
        }

        private static void ValidateParameterValueKinds(
            JsonElement parameters,
            IReadOnlyDictionary<string, AlgorithmParameterField> fields)
        {
            foreach (JsonProperty property in parameters.EnumerateObject())
            {
                AlgorithmParameterField field = fields[property.Name];
                bool valid = field.AllowedValues != null
                    ? property.Value.ValueKind == JsonValueKind.String
                    : field.ValueType switch
                    {
                        nameof(String) => property.Value.ValueKind == JsonValueKind.String,
                        nameof(Boolean) => property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                        nameof(Byte) or nameof(SByte) or nameof(Int16) or nameof(UInt16) or nameof(Int32)
                            or nameof(UInt32) or nameof(Int64) or nameof(UInt64) => IsIntegral(property.Value),
                        nameof(Single) or nameof(Double) or nameof(Decimal) => property.Value.ValueKind == JsonValueKind.Number,
                        _ => true,
                    };
                if (!valid)
                    throw new JsonException($"Preset property '$.parameters.{property.Name}' does not match {field.ValueType}.");
            }
        }

        private static bool IsIntegral(JsonElement value)
            => value.ValueKind == JsonValueKind.Number
                && (value.TryGetInt64(out _) || value.TryGetUInt64(out _));

        private static void RejectDuplicateProperties(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!seen.Add(property.Name))
                        throw new JsonException($"Duplicate preset property '{path}.{property.Name}'.");
                    RejectDuplicateProperties(property.Value, $"{path}.{property.Name}");
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                    RejectDuplicateProperties(item, $"{path}[{index++}]");
            }
        }

        private static JsonElement GetRequiredProperty(JsonElement element, string name, string path)
            => TryGetProperty(element, name, out JsonElement value)
                ? value
                : throw new JsonException($"Required preset property '{path}.{name}' is missing.");

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
}
