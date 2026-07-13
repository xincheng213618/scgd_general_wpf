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

        public IReadOnlyList<CopilotToolParameter> Parameters { get; }

        public JsonElement JsonSchema { get; }

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
                Query = query,
                Path = path,
                StartLine = startLine,
                EndLine = endLine,
            };
            error = string.Empty;
            return true;
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
    }
}
