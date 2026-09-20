using Anthropic.Exceptions;
using Anthropic.Models;
using System;
using System.ClientModel;
using System.Text.Json;

namespace ColorVision.Copilot
{
    // Only structured error fields affect retry eligibility; prose is not a protocol contract.
    internal static class CopilotProviderErrorPolicy
    {
        private const string ErrorDataKey = "ColorVision.Copilot.ProviderErrorCode";
        private const int MaximumErrorBytes = 256 * 1024;

        internal readonly record struct ErrorCode(string Code, string Type)
        {
            public string PreferredCode => string.IsNullOrWhiteSpace(Code) ? Type ?? string.Empty : Code;
            public bool RequiresUserAction => IsPermanent(Code) || IsPermanent(Type);
            public string DiagnosticCode => IsKnownDiagnosticCode(Code) ? Normalize(Code)
                : IsKnownDiagnosticCode(Type) ? Normalize(Type) : string.Empty;
        }

        public static void PreserveHttpError(Exception exception, string body)
        {
            if (body.Length > MaximumErrorBytes)
                return;
            try
            {
                using var document = JsonDocument.Parse(body);
                exception.Data[ErrorDataKey] = ReadError(document.RootElement);
            }
            catch (JsonException)
            {
                // Non-JSON HTTP errors retain their status-based handling.
            }
        }

        public static bool IsTransientHttpFailure(Exception exception, int statusCode, out string failureKind)
        {
            var error = FindHttpError(exception);
            var code = Normalize(error.PreferredCode);
            failureKind = IsTransient(code) || IsPermanent(code)
                ? $"HTTP {statusCode} ({code})"
                : $"HTTP {statusCode}";
            return CopilotProviderRetryChatClient.IsTransientStatusCode(statusCode) && !error.RequiresUserAction;
        }

        public static bool IsTransientPayload(string? code, string? type)
            => !IsPermanent(code) && !IsPermanent(type) && (IsTransient(code) || IsTransient(type));

        internal static ErrorCode FindHttpError(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current.Data[ErrorDataKey] is ErrorCode preserved)
                    return preserved;
                if (current is AnthropicSseException sseException)
                    return new ErrorCode(string.Empty, sseException.ErrorType switch
                    {
                        ErrorType.AuthenticationError => "authentication_error",
                        ErrorType.InvalidRequestError => "invalid_request_error",
                        ErrorType.OverloadedError => "overloaded_error",
                        ErrorType.RateLimitError => "rate_limit_error",
                        ErrorType.ApiError => "api_error",
                        ErrorType.TimeoutError => "timeout_error",
                        _ => string.Empty,
                    });
                if (current is not ClientResultException sdkException)
                    continue;
                try
                {
                    var body = sdkException.GetRawResponse()?.Content;
                    if (body == null || body.ToMemory().Length > MaximumErrorBytes)
                        continue;
                    using var document = JsonDocument.Parse(body.ToMemory());
                    return ReadError(document.RootElement);
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException)
                {
                    // Optional, buffered SDK metadata must not replace the original error.
                }
            }
            return default;
        }

        private static ErrorCode ReadError(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.Object)
                return default;
            return new ErrorCode(ReadString(error, "code"), ReadString(error, "type"));
        }

        private static string ReadString(JsonElement element, string name)
            => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private static string Normalize(string? code) => (code ?? string.Empty).Trim().Replace('-', '_').Replace('.', '_').ToLowerInvariant();

        private static bool IsKnownDiagnosticCode(string? code) => IsTransient(code) || IsPermanent(code)
            || Normalize(code) is "authentication_error" or "invalid_api_key" or "permission_error"
                or "permission_denied" or "invalid_request_error" or "not_found_error" or "model_not_found";

        private static bool IsPermanent(string? code) => Normalize(code) is "insufficient_quota"
            or "credit_balance_exhausted"
            or "organization_spend_limit_exceeded"
            or "project_spend_limit_exceeded"
            or "organization_usage_limit_exceeded";

        private static bool IsTransient(string? code) => Normalize(code) is "overloaded_error"
            or "rate_limit_error"
            or "rate_limit_exceeded"
            or "slow_down"
            or "server_is_overloaded"
            or "api_error"
            or "server_error"
            or "timeout_error"
            or "service_unavailable"
            or "service_unavailable_error";
    }
}
