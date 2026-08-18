using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot.Mcp
{
    internal static class CopilotMcpInputContractValidator
    {
        public static bool TryValidate(
            object? inputSchema,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            out string error)
        {
            if (inputSchema is not JsonElement schema
                || schema.ValueKind != JsonValueKind.Object)
            {
                error = "The tool input schema is unavailable or invalid.";
                return false;
            }

            arguments ??= new Dictionary<string, JsonElement>();
            var properties = schema.TryGetProperty("properties", out var declaredProperties)
                && declaredProperties.ValueKind == JsonValueKind.Object
                    ? declaredProperties
                    : default;

            if (schema.TryGetProperty("additionalProperties", out var additionalProperties)
                && additionalProperties.ValueKind == JsonValueKind.False)
            {
                var unknownArgument = arguments.Keys.FirstOrDefault(name =>
                    properties.ValueKind != JsonValueKind.Object
                    || !properties.TryGetProperty(name, out _));
                if (!string.IsNullOrWhiteSpace(unknownArgument))
                {
                    error = $"Argument '{unknownArgument}' is not declared by the tool input schema.";
                    return false;
                }
            }

            if (schema.TryGetProperty("required", out var required)
                && required.ValueKind == JsonValueKind.Array)
            {
                foreach (var requiredNameElement in required.EnumerateArray())
                {
                    var requiredName = requiredNameElement.ValueKind == JsonValueKind.String
                        ? requiredNameElement.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(requiredName)
                        && !arguments.ContainsKey(requiredName))
                    {
                        error = $"Required argument '{requiredName}' is missing.";
                        return false;
                    }
                }
            }

            if (properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var argument in arguments)
                {
                    if (!properties.TryGetProperty(argument.Key, out var propertySchema))
                        continue;
                    if (!TryValidateValue(argument.Key, argument.Value, propertySchema, out error))
                        return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateValue(
            string path,
            JsonElement value,
            JsonElement schema,
            out string error)
        {
            if (schema.ValueKind != JsonValueKind.Object)
            {
                error = $"The input schema for argument '{path}' is invalid.";
                return false;
            }

            if (schema.TryGetProperty("type", out var typeElement)
                && typeElement.ValueKind == JsonValueKind.String)
            {
                var expectedType = typeElement.GetString() ?? string.Empty;
                if (!MatchesType(value, expectedType))
                {
                    error = $"Argument '{path}' must be {FormatType(expectedType)}.";
                    return false;
                }
            }

            if (schema.TryGetProperty("enum", out var allowedValues)
                && allowedValues.ValueKind == JsonValueKind.Array
                && !allowedValues.EnumerateArray().Any(candidate => JsonElement.DeepEquals(candidate, value)))
            {
                error = $"Argument '{path}' is not one of the values allowed by the tool input schema.";
                return false;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var length = value.GetString()?.Length ?? 0;
                if (TryGetInt64(schema, "minLength", out var minimumLength)
                    && length < minimumLength)
                {
                    error = $"Argument '{path}' must contain at least {minimumLength} characters.";
                    return false;
                }
                if (TryGetInt64(schema, "maxLength", out var maximumLength)
                    && length > maximumLength)
                {
                    error = $"Argument '{path}' must contain at most {maximumLength} characters.";
                    return false;
                }
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                var hasNumericValue = value.TryGetDecimal(out var numericValue);
                if (TryGetDecimal(schema, "minimum", out var minimum)
                    && (!hasNumericValue || numericValue < minimum))
                {
                    error = $"Argument '{path}' must be greater than or equal to {minimum}.";
                    return false;
                }
                if (TryGetDecimal(schema, "maximum", out var maximum)
                    && (!hasNumericValue || numericValue > maximum))
                {
                    error = $"Argument '{path}' must be less than or equal to {maximum}.";
                    return false;
                }
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var length = value.GetArrayLength();
                if (TryGetInt64(schema, "minItems", out var minimumItems)
                    && length < minimumItems)
                {
                    error = $"Argument '{path}' must contain at least {minimumItems} items.";
                    return false;
                }
                if (TryGetInt64(schema, "maxItems", out var maximumItems)
                    && length > maximumItems)
                {
                    error = $"Argument '{path}' must contain at most {maximumItems} items.";
                    return false;
                }
                if (schema.TryGetProperty("items", out var itemSchema))
                {
                    var index = 0;
                    foreach (var item in value.EnumerateArray())
                    {
                        if (!TryValidateValue($"{path}[{index}]", item, itemSchema, out error))
                            return false;
                        index++;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool MatchesType(JsonElement value, string expectedType) => expectedType switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "array" => value.ValueKind == JsonValueKind.Array,
            "object" => value.ValueKind == JsonValueKind.Object,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => false,
        };

        private static string FormatType(string expectedType) => expectedType switch
        {
            "integer" => "an integer",
            "array" => "an array",
            "object" => "an object",
            _ => $"a {expectedType}",
        };

        private static bool TryGetInt64(JsonElement schema, string name, out long value)
        {
            value = default;
            return schema.TryGetProperty(name, out var element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt64(out value);
        }

        private static bool TryGetDecimal(JsonElement schema, string name, out decimal value)
        {
            value = default;
            return schema.TryGetProperty(name, out var element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetDecimal(out value);
        }
    }
}
