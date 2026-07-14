using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot
{
    public enum CopilotToolParameterType
    {
        Text,
        WholeNumber,
    }

    public sealed class CopilotToolParameter
    {
        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public CopilotToolParameterType Type { get; init; }

        public bool Required { get; init; }
    }

    public sealed class CopilotToolInputSchema
    {
        private const int MaximumSerializedArgumentsLength = 65_536;
        private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly HashSet<string> SupportedNames = new(new[] { "query", "path", "startLine", "endLine" }, NameComparer);

        public static CopilotToolInputSchema Empty { get; } = new(Array.Empty<CopilotToolParameter>());

        public static CopilotToolInputSchema OptionalQuery { get; } = Query("Focused request, search text, target, or payload for this tool.");

        public CopilotToolInputSchema(IEnumerable<CopilotToolParameter> parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            Parameters = parameters.ToArray();
            ValidateParameters(Parameters);
            JsonSchema = BuildJsonSchema(Parameters);
        }

        private CopilotToolInputSchema(JsonElement jsonSchema)
        {
            if (jsonSchema.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("A tool input schema must be a JSON object.", nameof(jsonSchema));

            Parameters = Array.Empty<CopilotToolParameter>();
            JsonSchema = jsonSchema.Clone();
            UsesArbitraryArguments = true;
        }

        public IReadOnlyList<CopilotToolParameter> Parameters { get; }

        public JsonElement JsonSchema { get; }

        public bool UsesArbitraryArguments { get; }

        public static CopilotToolInputSchema FromJsonSchema(JsonElement jsonSchema) => new(jsonSchema);

        public static CopilotToolInputSchema Query(string description, bool required = false)
        {
            return new CopilotToolInputSchema(new[]
            {
                new CopilotToolParameter { Name = "query", Description = description, Type = CopilotToolParameterType.Text, Required = required },
            });
        }

        public static CopilotToolInputSchema Path(string description, bool required = true)
        {
            return new CopilotToolInputSchema(new[]
            {
                new CopilotToolParameter { Name = "path", Description = description, Type = CopilotToolParameterType.Text, Required = required },
            });
        }

        public static CopilotToolInputSchema FileRead(bool requirePath = false)
        {
            return new CopilotToolInputSchema(new[]
            {
                new CopilotToolParameter { Name = "path", Description = "Allowed local text file path. Omit only when the current request should read all selected files.", Type = CopilotToolParameterType.Text, Required = requirePath },
                new CopilotToolParameter { Name = "startLine", Description = "Optional one-based first line to read.", Type = CopilotToolParameterType.WholeNumber },
                new CopilotToolParameter { Name = "endLine", Description = "Optional one-based last line to read; must not precede startLine.", Type = CopilotToolParameterType.WholeNumber },
            });
        }

        public bool TryBind(IReadOnlyDictionary<string, object?> arguments, out CopilotAgentToolInput input, out string error)
        {
            arguments ??= new Dictionary<string, object?>();
            if (UsesArbitraryArguments)
                return TryBindArbitraryArguments(arguments, out input, out error);

            var parametersByName = Parameters.ToDictionary(parameter => parameter.Name, NameComparer);
            var unknown = arguments.Keys.FirstOrDefault(name => !parametersByName.ContainsKey(name));
            if (!string.IsNullOrWhiteSpace(unknown))
            {
                input = CopilotAgentToolInput.Empty;
                error = $"Unknown argument '{unknown}'. Allowed arguments: {FormatAllowedParameters()}.";
                return false;
            }

            foreach (var required in Parameters.Where(parameter => parameter.Required))
            {
                if (!TryGetValue(arguments, required.Name, out var value) || IsMissing(value))
                {
                    input = CopilotAgentToolInput.Empty;
                    error = $"Required argument '{required.Name}' is missing.";
                    return false;
                }
            }

            if (!TryReadString(arguments, "query", out var query, out error)
                || !TryReadString(arguments, "path", out var path, out error)
                || !TryReadPositiveInt(arguments, "startLine", out var startLine, out error)
                || !TryReadPositiveInt(arguments, "endLine", out var endLine, out error))
            {
                input = CopilotAgentToolInput.Empty;
                return false;
            }

            if (startLine.HasValue && endLine.HasValue && endLine < startLine)
            {
                input = CopilotAgentToolInput.Empty;
                error = "Argument 'endLine' must be greater than or equal to 'startLine'.";
                return false;
            }

            input = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>(arguments, NameComparer),
                Query = query,
                Path = path,
                StartLine = startLine,
                EndLine = endLine,
            };
            error = string.Empty;
            return true;
        }

        private bool TryBindArbitraryArguments(IReadOnlyDictionary<string, object?> arguments, out CopilotAgentToolInput input, out string error)
        {
            error = string.Empty;
            var copiedArguments = new Dictionary<string, object?>(arguments, NameComparer);
            string serialized;
            try
            {
                serialized = JsonSerializer.Serialize(copiedArguments);
            }
            catch (Exception ex)
            {
                input = CopilotAgentToolInput.Empty;
                error = "Tool arguments could not be serialized: " + ex.Message;
                return false;
            }

            if (serialized.Length > MaximumSerializedArgumentsLength)
            {
                input = CopilotAgentToolInput.Empty;
                error = $"Tool arguments exceed the {MaximumSerializedArgumentsLength}-character limit.";
                return false;
            }

            if (JsonSchema.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var requiredNameElement in requiredElement.EnumerateArray())
                {
                    var requiredName = requiredNameElement.GetString();
                    if (string.IsNullOrWhiteSpace(requiredName))
                        continue;
                    if (!TryGetValue(copiedArguments, requiredName, out var requiredValue) || IsMissing(requiredValue))
                    {
                        input = CopilotAgentToolInput.Empty;
                        error = $"Required argument '{requiredName}' is missing.";
                        return false;
                    }
                }
            }

            if (JsonSchema.TryGetProperty("additionalProperties", out var additionalProperties)
                && additionalProperties.ValueKind == JsonValueKind.False
                && JsonSchema.TryGetProperty("properties", out var propertiesElement)
                && propertiesElement.ValueKind == JsonValueKind.Object)
            {
                var knownNames = propertiesElement.EnumerateObject().Select(property => property.Name).ToHashSet(NameComparer);
                var unknownName = copiedArguments.Keys.FirstOrDefault(name => !knownNames.Contains(name));
                if (!string.IsNullOrWhiteSpace(unknownName))
                {
                    input = CopilotAgentToolInput.Empty;
                    error = $"Unknown argument '{unknownName}'.";
                    return false;
                }
            }

            if (JsonSchema.TryGetProperty("properties", out var validationProperties)
                && validationProperties.ValueKind == JsonValueKind.Object)
            {
                foreach (var argument in copiedArguments)
                {
                    var property = validationProperties.EnumerateObject()
                        .FirstOrDefault(candidate => NameComparer.Equals(candidate.Name, argument.Key));
                    if (string.IsNullOrWhiteSpace(property.Name))
                        continue;
                    if (TryValidateTopLevelValue(argument.Key, argument.Value, property.Value, out error))
                        continue;
                    input = CopilotAgentToolInput.Empty;
                    return false;
                }
            }

            input = new CopilotAgentToolInput
            {
                Arguments = copiedArguments,
                Query = TryReadCompatibleString(copiedArguments, "query"),
                Path = TryReadCompatibleString(copiedArguments, "path"),
                StartLine = TryReadCompatibleInt(copiedArguments, "startLine"),
                EndLine = TryReadCompatibleInt(copiedArguments, "endLine"),
            };
            error = string.Empty;
            return true;
        }

        private static bool TryValidateTopLevelValue(
            string name,
            object? value,
            JsonElement schema,
            out string error)
        {
            error = string.Empty;
            if (value == null || value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
                return true;
            if (schema.TryGetProperty("type", out var typeElement)
                && typeElement.ValueKind == JsonValueKind.String)
            {
                var expectedType = typeElement.GetString() ?? string.Empty;
                if (!MatchesJsonType(value, expectedType))
                {
                    error = $"Argument '{name}' must be a JSON {expectedType}.";
                    return false;
                }
            }
            if (schema.TryGetProperty("enum", out var enumElement)
                && enumElement.ValueKind == JsonValueKind.Array
                && TryGetStringValue(value, out var textValue))
            {
                var allowed = enumElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .ToArray();
                if (allowed.Length > 0 && !allowed.Contains(textValue, StringComparer.Ordinal))
                {
                    error = $"Argument '{name}' must be one of: {string.Join(", ", allowed)}.";
                    return false;
                }
            }
            if (TryGetStringValue(value, out var stringValue))
            {
                if (schema.TryGetProperty("minLength", out var minimumLengthElement)
                    && minimumLengthElement.TryGetInt32(out var minimumLength)
                    && stringValue.Length < minimumLength)
                {
                    error = $"Argument '{name}' must contain at least {minimumLength} characters.";
                    return false;
                }
                if (schema.TryGetProperty("maxLength", out var maximumLengthElement)
                    && maximumLengthElement.TryGetInt32(out var maximumLength)
                    && stringValue.Length > maximumLength)
                {
                    error = $"Argument '{name}' must contain at most {maximumLength} characters.";
                    return false;
                }
            }
            if (TryGetNumberValue(value, out var numberValue))
            {
                if (schema.TryGetProperty("minimum", out var minimumElement)
                    && minimumElement.TryGetDouble(out var minimum)
                    && numberValue < minimum)
                {
                    error = $"Argument '{name}' must be at least {minimum}.";
                    return false;
                }
                if (schema.TryGetProperty("maximum", out var maximumElement)
                    && maximumElement.TryGetDouble(out var maximum)
                    && numberValue > maximum)
                {
                    error = $"Argument '{name}' must be at most {maximum}.";
                    return false;
                }
            }
            return true;
        }

        private static bool MatchesJsonType(object value, string expectedType)
        {
            return expectedType switch
            {
                "string" => TryGetStringValue(value, out _),
                "integer" => TryGetIntegerValue(value, out _),
                "number" => TryGetNumberValue(value, out _),
                "boolean" => value is bool || value is JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False },
                "object" => value is IReadOnlyDictionary<string, object?>
                    || value is IDictionary<string, object?>
                    || value is JsonElement { ValueKind: JsonValueKind.Object },
                "array" => value is System.Collections.IEnumerable and not string
                    || value is JsonElement { ValueKind: JsonValueKind.Array },
                _ => true,
            };
        }

        private static bool TryGetStringValue(object value, out string text)
        {
            if (value is string stringValue)
            {
                text = stringValue;
                return true;
            }
            if (value is JsonElement { ValueKind: JsonValueKind.String } element)
            {
                text = element.GetString() ?? string.Empty;
                return true;
            }
            text = string.Empty;
            return false;
        }

        private static bool TryGetIntegerValue(object value, out long number)
        {
            switch (value)
            {
                case byte byteValue: number = byteValue; return true;
                case short shortValue: number = shortValue; return true;
                case int intValue: number = intValue; return true;
                case long longValue: number = longValue; return true;
                case JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt64(out var jsonValue):
                    number = jsonValue;
                    return true;
                default:
                    number = 0;
                    return false;
            }
        }

        private static bool TryGetNumberValue(object value, out double number)
        {
            if (TryGetIntegerValue(value, out var integer))
            {
                number = integer;
                return true;
            }
            switch (value)
            {
                case float floatValue: number = floatValue; return true;
                case double doubleValue: number = doubleValue; return true;
                case decimal decimalValue: number = (double)decimalValue; return true;
                case JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetDouble(out var jsonValue):
                    number = jsonValue;
                    return true;
                default:
                    number = 0;
                    return false;
            }
        }

        private string FormatAllowedParameters()
        {
            return Parameters.Count == 0 ? "none" : string.Join(", ", Parameters.Select(parameter => parameter.Name));
        }

        private static void ValidateParameters(IReadOnlyList<CopilotToolParameter> parameters)
        {
            var names = new HashSet<string>(NameComparer);
            foreach (var parameter in parameters)
            {
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
                    throw new ArgumentException("Copilot tool parameter names cannot be empty.", nameof(parameters));
                if (!SupportedNames.Contains(parameter.Name))
                    throw new ArgumentException($"Unsupported Copilot tool parameter '{parameter.Name}'.", nameof(parameters));
                if (!names.Add(parameter.Name))
                    throw new ArgumentException($"Copilot tool parameter '{parameter.Name}' is duplicated.", nameof(parameters));
            }
        }

        private static JsonElement BuildJsonSchema(IReadOnlyList<CopilotToolParameter> parameters)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                var property = new Dictionary<string, object?>
                {
                    ["type"] = parameter.Type == CopilotToolParameterType.WholeNumber ? "integer" : "string",
                    ["description"] = parameter.Description,
                };
                if (parameter.Type == CopilotToolParameterType.WholeNumber)
                    property["minimum"] = 1;
                properties[parameter.Name] = property;
            }

            var schema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false,
            };
            var required = parameters.Where(parameter => parameter.Required).Select(parameter => parameter.Name).ToArray();
            if (required.Length > 0)
                schema["required"] = required;
            return JsonSerializer.SerializeToElement(schema);
        }

        private static bool TryReadString(IReadOnlyDictionary<string, object?> arguments, string name, out string value, out string error)
        {
            value = string.Empty;
            error = string.Empty;
            if (!TryGetValue(arguments, name, out var raw) || raw == null)
                return true;

            if (raw is JsonElement element)
            {
                if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    return true;
                if (element.ValueKind != JsonValueKind.String)
                {
                    error = $"Argument '{name}' must be a string.";
                    return false;
                }
                value = element.GetString()?.Trim() ?? string.Empty;
            }
            else if (raw is string text)
            {
                value = text.Trim();
            }
            else
            {
                error = $"Argument '{name}' must be a string.";
                return false;
            }

            var maximumLength = string.Equals(name, "path", StringComparison.OrdinalIgnoreCase) ? 4096 : 32768;
            if (value.Length > maximumLength)
            {
                error = $"Argument '{name}' exceeds its {maximumLength}-character limit.";
                return false;
            }
            return true;
        }

        private static bool TryReadPositiveInt(IReadOnlyDictionary<string, object?> arguments, string name, out int? value, out string error)
        {
            value = null;
            error = string.Empty;
            if (!TryGetValue(arguments, name, out var raw) || raw == null)
                return true;

            if (raw is JsonElement element)
            {
                if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    return true;
                if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var jsonValue))
                {
                    error = $"Argument '{name}' must be an integer.";
                    return false;
                }
                value = jsonValue;
            }
            else
            {
                value = raw switch
                {
                    byte number => number,
                    short number => number,
                    int number => number,
                    long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
                    _ => null,
                };
                if (!value.HasValue)
                {
                    error = $"Argument '{name}' must be an integer.";
                    return false;
                }
            }

            if (value is < 1 or > 1_000_000)
            {
                error = $"Argument '{name}' must be between 1 and 1000000.";
                return false;
            }
            return true;
        }

        private static bool TryGetValue(IReadOnlyDictionary<string, object?> arguments, string name, out object? value)
        {
            foreach (var pair in arguments)
            {
                if (NameComparer.Equals(pair.Key, name))
                {
                    value = pair.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        private static bool IsMissing(object? value)
        {
            if (value == null)
                return true;
            if (value is string text)
                return string.IsNullOrWhiteSpace(text);
            if (value is JsonElement element)
                return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                    || element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString());
            return false;
        }

        private static string TryReadCompatibleString(IReadOnlyDictionary<string, object?> arguments, string name)
        {
            if (!TryGetValue(arguments, name, out var value) || value == null)
                return string.Empty;
            if (value is string text)
                return text.Trim();
            if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
                return element.GetString()?.Trim() ?? string.Empty;
            return string.Empty;
        }

        private static int? TryReadCompatibleInt(IReadOnlyDictionary<string, object?> arguments, string name)
        {
            if (!TryGetValue(arguments, name, out var value) || value == null)
                return null;
            if (value is int integer)
                return integer;
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var jsonInteger))
                return jsonInteger;
            return null;
        }
    }
}
