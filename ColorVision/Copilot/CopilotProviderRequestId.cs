using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal static class CopilotProviderRequestId
    {
        internal const string ExceptionDataKey =
            "ColorVision.Copilot.ProviderRequestId";
        private const int MaximumLength = 128;
        private static readonly string[] ResponseHeaderNames =
        {
            "x-request-id",
            "request-id",
            "x-amzn-requestid",
        };
        private static readonly string[] JsonPropertyNames =
        {
            "request_id",
            "requestId",
        };

        public static string Extract(HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);
            foreach (var headerName in ResponseHeaderNames)
            {
                if (!response.Headers.TryGetValues(headerName, out var values))
                    continue;
                foreach (var value in values)
                {
                    var normalized = Normalize(value);
                    if (normalized.Length > 0)
                        return normalized;
                }
            }
            return string.Empty;
        }

        public static string Extract(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
                return string.Empty;

            foreach (var propertyName in JsonPropertyNames)
            {
                if (root.TryGetProperty(propertyName, out var property)
                    && property.ValueKind == JsonValueKind.String)
                {
                    var normalized = Normalize(property.GetString());
                    if (normalized.Length > 0)
                        return normalized;
                }
            }
            return string.Empty;
        }

        public static string Prefer(string? preferred, string? fallback)
        {
            var normalized = Normalize(preferred);
            return normalized.Length > 0 ? normalized : Normalize(fallback);
        }

        public static string Redact(
            string? value,
            params string?[] sensitiveValues)
        {
            var normalized = Normalize(value);
            foreach (var sensitiveValue in sensitiveValues
                ?? Array.Empty<string?>())
            {
                var sensitive = Normalize(sensitiveValue);
                if (sensitive.Length >= 4)
                {
                    normalized = normalized.Replace(
                        sensitive,
                        "redacted",
                        StringComparison.Ordinal);
                }
            }
            return Normalize(normalized);
        }

        public static string AppendToMessage(string message, string? requestId)
        {
            var normalized = Normalize(requestId);
            return normalized.Length == 0
                ? message
                : $"{message} [request {normalized}]";
        }

        public static void Preserve(Exception exception, string? requestId)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (exception.Data[ExceptionDataKey] is string existing
                && Normalize(existing).Length > 0)
            {
                return;
            }

            var normalized = Normalize(requestId);
            if (normalized.Length > 0)
                exception.Data[ExceptionDataKey] = normalized;
        }

        public static string Find(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current.Data[ExceptionDataKey] is string requestId)
                {
                    var normalized = Normalize(requestId);
                    if (normalized.Length > 0)
                        return normalized;
                }
            }
            return string.Empty;
        }

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(Math.Min(value.Length, MaximumLength));
            foreach (var character in value.Trim())
            {
                if (char.IsLetterOrDigit(character)
                    || character is '_' or '-' or '.' or ':')
                {
                    builder.Append(character);
                }
                else if (builder.Length > 0 && builder[^1] != '_')
                {
                    builder.Append('_');
                }

                if (builder.Length == MaximumLength)
                    break;
            }
            return builder.ToString().Trim('_');
        }
    }
}
