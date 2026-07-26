using ColorVision.Copilot.Mcp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotToolProgressUpdate
    {
        public string Message { get; init; } = string.Empty;

        public long? Completed { get; init; }

        public long? Total { get; init; }

        public string Unit { get; init; } = string.Empty;
    }

    public sealed class CopilotToolProgressContext
    {
        private const int MaximumMessageLength = 240;
        private const int MaximumUnitLength = 24;
        private const long MaximumCount = 1_000_000_000;
        private readonly object _syncRoot = new();
        private CopilotToolProgressUpdate? _latest;
        private bool _isCompleted;

        public void Report(
            string message,
            long? completed = null,
            long? total = null,
            string? unit = null)
        {
            Report(new CopilotToolProgressUpdate
            {
                Message = message ?? string.Empty,
                Completed = completed,
                Total = total,
                Unit = unit ?? string.Empty,
            });
        }

        public void Report(CopilotToolProgressUpdate update)
        {
            ArgumentNullException.ThrowIfNull(update);
            var normalized = Normalize(update);
            if (string.IsNullOrWhiteSpace(normalized.Message)
                && !normalized.Completed.HasValue
                && !normalized.Total.HasValue)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (!_isCompleted)
                    _latest = normalized;
            }
        }

        internal CopilotToolProgressUpdate? LatestSnapshot
        {
            get
            {
                lock (_syncRoot)
                    return _latest;
            }
        }

        internal void Complete()
        {
            lock (_syncRoot)
                _isCompleted = true;
        }

        private static CopilotToolProgressUpdate Normalize(CopilotToolProgressUpdate update)
        {
            var message = CollapseWhitespace(CopilotMcpAuditLogger.RedactText(update.Message));
            if (message.Length > MaximumMessageLength)
                message = message[..MaximumMessageLength] + "...";

            var unit = CollapseWhitespace(CopilotMcpAuditLogger.RedactText(update.Unit));
            if (unit.Length > MaximumUnitLength)
                unit = unit[..MaximumUnitLength];

            var completed = NormalizeCount(update.Completed);
            var total = NormalizeCount(update.Total);
            if (completed.HasValue && total.HasValue)
                completed = Math.Min(completed.Value, total.Value);

            return new CopilotToolProgressUpdate
            {
                Message = message,
                Completed = completed,
                Total = total,
                Unit = unit,
            };
        }

        private static long? NormalizeCount(long? value)
        {
            return value.HasValue
                ? Math.Clamp(value.Value, 0, MaximumCount)
                : null;
        }

        private static string CollapseWhitespace(string? value)
        {
            var characters = (value ?? string.Empty).ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                if (char.IsControl(characters[index]) && !char.IsWhiteSpace(characters[index]))
                    characters[index] = ' ';
            }

            return string.Join(" ", new string(characters)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    public interface ICopilotProgressReportingTool : ICopilotTool
    {
        Task<CopilotToolResult> ExecuteWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken);
    }

    public interface ICopilotFrameworkApprovedProgressReportingTool : ICopilotFrameworkApprovedTool
    {
        Task<CopilotToolResult> ExecuteApprovedWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken);
    }
}
