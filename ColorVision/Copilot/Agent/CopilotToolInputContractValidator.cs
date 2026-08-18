using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    internal static class CopilotToolInputContractValidator
    {
        private static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
        {
            "string",
            "integer",
            "number",
            "boolean",
            "array",
            "object",
            "null",
        };
        private static readonly HashSet<string> SupportedSchemaKeywords = new(StringComparer.Ordinal)
        {
            "$comment",
            "$id",
            "$schema",
            "type",
            "title",
            "description",
            "default",
            "examples",
            "deprecated",
            "readOnly",
            "writeOnly",
            "enum",
            "pattern",
            "minLength",
            "maxLength",
            "minimum",
            "maximum",
            "minItems",
            "maxItems",
            "uniqueItems",
            "items",
            "properties",
            "required",
            "additionalProperties",
        };

        public static bool TryValidateSchema(
            object? inputSchema,
            out string error,
            bool requireClosedObjects = true)
        {
            if (inputSchema is not JsonElement schema
                || schema.ValueKind != JsonValueKind.Object)
            {
                error = "The tool input schema must be a frozen JSON object.";
                return false;
            }
            if (!schema.TryGetProperty("type", out var rootType)
                || rootType.ValueKind != JsonValueKind.String
                || !string.Equals(rootType.GetString(), "object", StringComparison.Ordinal))
            {
                error = "The tool input schema root type must be object.";
                return false;
            }
            if (schema.TryGetProperty("additionalProperties", out var additionalProperties)
                && additionalProperties.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = "The tool input schema additionalProperties member must be a boolean.";
                return false;
            }
            if (requireClosedObjects
                && (!schema.TryGetProperty("additionalProperties", out additionalProperties)
                    || additionalProperties.ValueKind != JsonValueKind.False))
            {
                error = "The tool input schema must set additionalProperties to false.";
                return false;
            }

            return TryValidatePropertySchema("$", schema, requireClosedObjects, out error);
        }

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

        private static bool TryValidateObjectSchema(
            string path,
            JsonElement schema,
            bool requireClosedObjects,
            out string error)
        {
            var properties = schema.TryGetProperty("properties", out var declaredProperties)
                ? declaredProperties
                : default;
            if (properties.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Object))
            {
                error = $"Schema '{path}' must declare an object-valued properties member.";
                return false;
            }
            if (schema.TryGetProperty("additionalProperties", out var additionalProperties)
                && additionalProperties.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = $"Schema '{path}.additionalProperties' must be a boolean.";
                return false;
            }
            if (requireClosedObjects
                && (!schema.TryGetProperty("additionalProperties", out additionalProperties)
                    || additionalProperties.ValueKind != JsonValueKind.False))
            {
                error = $"Schema '{path}' must set additionalProperties to false.";
                return false;
            }

            var propertyNames = properties.ValueKind == JsonValueKind.Object
                ? properties.EnumerateObject()
                    .Select(property => property.Name)
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            if (propertyNames.Any(string.IsNullOrWhiteSpace))
            {
                error = $"Schema '{path}' contains an empty property name.";
                return false;
            }

            if (schema.TryGetProperty("required", out var required))
            {
                if (required.ValueKind != JsonValueKind.Array)
                {
                    error = $"Schema '{path}.required' must be an array.";
                    return false;
                }

                var seenRequired = new HashSet<string>(StringComparer.Ordinal);
                foreach (var requiredElement in required.EnumerateArray())
                {
                    if (requiredElement.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(requiredElement.GetString()))
                    {
                        error = $"Schema '{path}.required' contains an invalid property name.";
                        return false;
                    }

                    var requiredName = requiredElement.GetString()!;
                    if (!seenRequired.Add(requiredName))
                    {
                        error = $"Schema '{path}.required' contains duplicate property '{requiredName}'.";
                        return false;
                    }
                    if (!propertyNames.Contains(requiredName))
                    {
                        error = $"Schema '{path}.required' references undeclared property '{requiredName}'.";
                        return false;
                    }
                }
            }

            foreach (var property in properties.ValueKind == JsonValueKind.Object
                ? properties.EnumerateObject()
                : Enumerable.Empty<JsonProperty>())
            {
                if (!TryValidatePropertySchema(
                        $"{path}.{property.Name}",
                        property.Value,
                        requireClosedObjects,
                        out error))
                    return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidatePropertySchema(
            string path,
            JsonElement schema,
            bool requireClosedObjects,
            out string error)
        {
            if (schema.ValueKind != JsonValueKind.Object)
            {
                error = $"Schema '{path}' must be an object.";
                return false;
            }
            var unsupportedKeyword = schema.EnumerateObject()
                .Select(property => property.Name)
                .FirstOrDefault(keyword => !SupportedSchemaKeywords.Contains(keyword));
            if (!string.IsNullOrWhiteSpace(unsupportedKeyword))
            {
                error = $"Schema '{path}' uses unsupported keyword '{unsupportedKeyword}'.";
                return false;
            }
            if (schema.TryGetProperty("description", out var description)
                && description.ValueKind != JsonValueKind.String)
            {
                error = $"Schema '{path}.description' must be a string.";
                return false;
            }
            foreach (var annotationName in new[] { "$comment", "$id", "$schema", "title" })
            {
                if (schema.TryGetProperty(annotationName, out var annotation)
                    && annotation.ValueKind != JsonValueKind.String)
                {
                    error = $"Schema '{path}.{annotationName}' must be a string.";
                    return false;
                }
            }
            if (schema.TryGetProperty("examples", out var examples)
                && examples.ValueKind != JsonValueKind.Array)
            {
                error = $"Schema '{path}.examples' must be an array.";
                return false;
            }
            foreach (var annotationName in new[] { "deprecated", "readOnly", "writeOnly" })
            {
                if (schema.TryGetProperty(annotationName, out var annotation)
                    && annotation.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    error = $"Schema '{path}.{annotationName}' must be a boolean.";
                    return false;
                }
            }

            string? declaredType = null;
            if (schema.TryGetProperty("type", out var typeElement))
            {
                if (typeElement.ValueKind != JsonValueKind.String
                    || !SupportedTypes.Contains(typeElement.GetString() ?? string.Empty))
                {
                    error = $"Schema '{path}' declares an unsupported type.";
                    return false;
                }
                declaredType = typeElement.GetString();
            }

            if (schema.TryGetProperty("enum", out var allowedValues))
            {
                if (allowedValues.ValueKind != JsonValueKind.Array
                    || allowedValues.GetArrayLength() == 0)
                {
                    error = $"Schema '{path}.enum' must be a non-empty array.";
                    return false;
                }
                if (declaredType != null
                    && allowedValues.EnumerateArray().Any(value => !MatchesType(value, declaredType)))
                {
                    error = $"Schema '{path}.enum' contains a value that does not match type '{declaredType}'.";
                    return false;
                }
            }

            var hasStringConstraint = schema.TryGetProperty("pattern", out _)
                || schema.TryGetProperty("minLength", out _)
                || schema.TryGetProperty("maxLength", out _);
            var hasArrayConstraint = schema.TryGetProperty("minItems", out _)
                || schema.TryGetProperty("maxItems", out _)
                || schema.TryGetProperty("uniqueItems", out _)
                || schema.TryGetProperty("items", out _);
            var hasNumericConstraint = schema.TryGetProperty("minimum", out _)
                || schema.TryGetProperty("maximum", out _);
            var hasObjectConstraint = schema.TryGetProperty("properties", out _)
                || schema.TryGetProperty("required", out _)
                || schema.TryGetProperty("additionalProperties", out _);
            if (hasStringConstraint
                && !string.Equals(declaredType, "string", StringComparison.Ordinal))
            {
                error = $"Schema '{path}' uses string constraints without type 'string'.";
                return false;
            }
            if (hasArrayConstraint
                && !string.Equals(declaredType, "array", StringComparison.Ordinal))
            {
                error = $"Schema '{path}' uses array constraints without type 'array'.";
                return false;
            }
            if (hasNumericConstraint
                && declaredType is not ("integer" or "number"))
            {
                error = $"Schema '{path}' uses numeric constraints without an integer or number type.";
                return false;
            }
            if (hasObjectConstraint
                && !string.Equals(declaredType, "object", StringComparison.Ordinal))
            {
                error = $"Schema '{path}' uses object constraints without type 'object'.";
                return false;
            }

            if (!TryValidateNonNegativeRange(schema, path, "minLength", "maxLength", out error)
                || !TryValidateNonNegativeRange(schema, path, "minItems", "maxItems", out error)
                || !TryValidateNumericRange(schema, path, out error))
            {
                return false;
            }
            if (schema.TryGetProperty("uniqueItems", out var uniqueItems)
                && uniqueItems.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = $"Schema '{path}.uniqueItems' must be a boolean.";
                return false;
            }

            if (schema.TryGetProperty("pattern", out var pattern))
            {
                if (pattern.ValueKind != JsonValueKind.String)
                {
                    error = $"Schema '{path}.pattern' must be a string.";
                    return false;
                }
                try
                {
                    _ = new Regex(
                        pattern.GetString() ?? string.Empty,
                        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
                        TimeSpan.FromMilliseconds(100));
                }
                catch (ArgumentException exception)
                {
                    error = $"Schema '{path}.pattern' is invalid: {exception.Message}";
                    return false;
                }
                catch (NotSupportedException exception)
                {
                    error = $"Schema '{path}.pattern' is unsupported: {exception.Message}";
                    return false;
                }
            }

            if (string.Equals(declaredType, "array", StringComparison.Ordinal))
            {
                if (!schema.TryGetProperty("items", out var itemSchema))
                {
                    error = $"Schema '{path}' declares an array without an items schema.";
                    return false;
                }
                if (!TryValidatePropertySchema(
                        path + "[]",
                        itemSchema,
                        requireClosedObjects,
                        out error))
                    return false;
            }

            if (string.Equals(declaredType, "object", StringComparison.Ordinal)
                && !TryValidateObjectSchema(path, schema, requireClosedObjects, out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateNonNegativeRange(
            JsonElement schema,
            string path,
            string minimumName,
            string maximumName,
            out string error)
        {
            var hasMinimum = TryGetInt64(schema, minimumName, out var minimum);
            var hasMaximum = TryGetInt64(schema, maximumName, out var maximum);
            if ((schema.TryGetProperty(minimumName, out _) && !hasMinimum)
                || (schema.TryGetProperty(maximumName, out _) && !hasMaximum)
                || minimum < 0
                || maximum < 0
                || (hasMinimum && hasMaximum && minimum > maximum))
            {
                error = $"Schema '{path}' has an invalid {minimumName}/{maximumName} range.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateNumericRange(
            JsonElement schema,
            string path,
            out string error)
        {
            var hasMinimum = TryGetDecimal(schema, "minimum", out var minimum);
            var hasMaximum = TryGetDecimal(schema, "maximum", out var maximum);
            if ((schema.TryGetProperty("minimum", out _) && !hasMinimum)
                || (schema.TryGetProperty("maximum", out _) && !hasMaximum)
                || (hasMinimum && hasMaximum && minimum > maximum))
            {
                error = $"Schema '{path}' has an invalid minimum/maximum range.";
                return false;
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
                var stringValue = value.GetString() ?? string.Empty;
                var length = stringValue.Length;
                if (schema.TryGetProperty("pattern", out var pattern)
                    && pattern.ValueKind == JsonValueKind.String)
                {
                    try
                    {
                        if (!Regex.IsMatch(
                                stringValue,
                                pattern.GetString() ?? string.Empty,
                                RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
                                TimeSpan.FromMilliseconds(100)))
                        {
                            error = $"Argument '{path}' does not match the required pattern.";
                            return false;
                        }
                    }
                    catch (Exception exception) when (exception is ArgumentException
                        or NotSupportedException
                        or RegexMatchTimeoutException)
                    {
                        error = $"The input schema pattern for argument '{path}' is invalid: {exception.Message}";
                        return false;
                    }
                }
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
                if (schema.TryGetProperty("uniqueItems", out var uniqueItems)
                    && uniqueItems.ValueKind == JsonValueKind.True)
                {
                    var items = value.EnumerateArray().ToArray();
                    for (var left = 0; left < items.Length; left++)
                    {
                        for (var right = left + 1; right < items.Length; right++)
                        {
                            if (!JsonElement.DeepEquals(items[left], items[right]))
                                continue;

                            error = $"Argument '{path}' must not contain duplicate items.";
                            return false;
                        }
                    }
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

            if (value.ValueKind == JsonValueKind.Object
                && schema.TryGetProperty("type", out var objectType)
                && objectType.ValueKind == JsonValueKind.String
                && string.Equals(objectType.GetString(), "object", StringComparison.Ordinal)
                && !TryValidateObjectValue(path, value, schema, out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateObjectValue(
            string path,
            JsonElement value,
            JsonElement schema,
            out string error)
        {
            var properties = schema.TryGetProperty("properties", out var declaredProperties)
                && declaredProperties.ValueKind == JsonValueKind.Object
                    ? declaredProperties
                    : default;

            if (schema.TryGetProperty("additionalProperties", out var additionalProperties)
                && additionalProperties.ValueKind == JsonValueKind.False)
            {
                foreach (var property in value.EnumerateObject())
                {
                    if (properties.ValueKind != JsonValueKind.Object
                        || !properties.TryGetProperty(property.Name, out _))
                    {
                        error = $"Argument '{path}.{property.Name}' is not declared by the tool input schema.";
                        return false;
                    }
                }
            }

            if (schema.TryGetProperty("required", out var required)
                && required.ValueKind == JsonValueKind.Array)
            {
                foreach (var requiredNameElement in required.EnumerateArray())
                {
                    var requiredName = requiredNameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(requiredName)
                        && !value.TryGetProperty(requiredName, out _))
                    {
                        error = $"Required argument '{path}.{requiredName}' is missing.";
                        return false;
                    }
                }
            }

            foreach (var property in value.EnumerateObject())
            {
                if (properties.ValueKind != JsonValueKind.Object
                    || !properties.TryGetProperty(property.Name, out var propertySchema))
                    continue;
                if (!TryValidateValue($"{path}.{property.Name}", property.Value, propertySchema, out error))
                    return false;
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
