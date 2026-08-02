using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace ColorVision.Copilot
{
    internal static class CopilotToolRetryPolicy
    {
        public const int MaximumAttemptsPerCall = 2;
        public const int MaximumRepeatableObservationAttempts = 8;

        public static bool IsRetryEligible(CopilotToolInvocation invocation, CopilotToolResult result, CopilotToolExecutionState state)
        {
            return IsRepeatableObservationEligible(invocation, result, state)
                || invocation.Tool.Capability.Idempotency == CopilotToolIdempotency.Idempotent
                && invocation.Attempt < invocation.MaxAttempts
                && result.FailureKind == CopilotToolFailureKind.Transient
                && state is CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut;
        }

        private static bool IsRepeatableObservationEligible(
            CopilotToolInvocation invocation,
            CopilotToolResult result,
            CopilotToolExecutionState state)
        {
            if (invocation.Tool is not ICopilotRepeatableObservationTool
                || invocation.Tool.Capability.Access != CopilotToolAccess.ReadOnly
                || invocation.Attempt >= invocation.MaxAttempts
                || state != CopilotToolExecutionState.Completed
                || !result.Success
                || !result.ObservationCanRepeat)
            {
                return false;
            }

            var currentSignature = NormalizeObservationProgressSignature(
                result.ObservationProgressSignature);
            if (currentSignature.Length == 0)
                return false;

            var previousSignature = NormalizeObservationProgressSignature(
                invocation.PreviousObservationProgressSignature);
            return previousSignature.Length == 0
                || !string.Equals(
                    previousSignature,
                    currentSignature,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static string NormalizeObservationProgressSignature(
            string? signature)
        {
            var normalized = (signature ?? string.Empty).Trim();
            return normalized.Length == 64
                && normalized.All(Uri.IsHexDigit)
                    ? normalized.ToLowerInvariant()
                    : string.Empty;
        }
    }

    internal static class CopilotToolFailureClassifier
    {
        public static CopilotToolFailureKind Classify(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (exception is HttpRequestException httpException)
                return ClassifyHttpStatus(httpException.StatusCode);

            return exception is TimeoutException or IOException or SocketException
                ? CopilotToolFailureKind.Transient
                : CopilotToolFailureKind.Internal;
        }

        private static CopilotToolFailureKind ClassifyHttpStatus(HttpStatusCode? statusCode)
        {
            if (!statusCode.HasValue
                || statusCode == HttpStatusCode.RequestTimeout
                || statusCode == HttpStatusCode.TooManyRequests
                || (int)statusCode.Value >= 500)
            {
                return CopilotToolFailureKind.Transient;
            }

            return statusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => CopilotToolFailureKind.Validation,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => CopilotToolFailureKind.Authorization,
                HttpStatusCode.NotFound or HttpStatusCode.Gone => CopilotToolFailureKind.NotFound,
                HttpStatusCode.Conflict => CopilotToolFailureKind.Conflict,
                _ => CopilotToolFailureKind.Internal,
            };
        }
    }

}
