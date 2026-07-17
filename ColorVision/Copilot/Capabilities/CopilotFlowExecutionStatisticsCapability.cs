using ColorVision.Database;
using ColorVision.Engine.Templates.Flow;
using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed record CopilotFlowExecutionStatusCount(
        int? StatusCode,
        int ExecutionCount,
        double AverageDurationMs);

    public interface ICopilotFlowExecutionStatisticsSource
    {
        bool IsAvailable { get; }

        Task<IReadOnlyList<CopilotFlowExecutionStatusCount>> QueryAsync(
            DateTime startInclusive,
            DateTime endExclusive,
            CancellationToken cancellationToken);
    }

    public sealed class CopilotMySqlFlowExecutionStatisticsSource : ICopilotFlowExecutionStatisticsSource
    {
        private const string QuerySql = """
            SELECT
                result_code AS StatusCode,
                COUNT(*) AS ExecutionCount,
                COALESCE(AVG(total_time), 0) AS AverageDurationMs
            FROM t_scgd_measure_batch
            WHERE create_date >= @startInclusive AND create_date < @endExclusive
            GROUP BY result_code
            """;

        public bool IsAvailable => MySqlControl.GetInstance().IsConnect;

        public async Task<IReadOnlyList<CopilotFlowExecutionStatusCount>> QueryAsync(
            DateTime startInclusive,
            DateTime endExclusive,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var db = MySqlControl.CreateDbClient();
            var rows = await db.Ado.SqlQueryAsync<FlowExecutionStatusRow>(QuerySql, new
            {
                startInclusive,
                endExclusive,
            });
            cancellationToken.ThrowIfCancellationRequested();
            return rows.Select(row => new CopilotFlowExecutionStatusCount(
                row.StatusCode,
                Math.Max(0, row.ExecutionCount),
                Math.Max(0, row.AverageDurationMs))).ToArray();
        }

        private sealed class FlowExecutionStatusRow
        {
            public int? StatusCode { get; set; }

            public int ExecutionCount { get; set; }

            public double AverageDurationMs { get; set; }
        }
    }

    public sealed class CopilotFlowExecutionStatisticsService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CopilotFlowExecutionStatisticsService));
        private readonly ICopilotFlowExecutionStatisticsSource _source;
        private readonly Func<DateTime> _localNowProvider;

        public CopilotFlowExecutionStatisticsService()
            : this(new CopilotMySqlFlowExecutionStatisticsSource(), () => DateTime.Now)
        {
        }

        public CopilotFlowExecutionStatisticsService(
            ICopilotFlowExecutionStatisticsSource source,
            Func<DateTime>? localNowProvider = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _localNowProvider = localNowProvider ?? (() => DateTime.Now);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            input ??= CopilotAgentToolInput.Empty;
            if (!TryReadPeriod(input, out var period))
            {
                return Failure(
                    CopilotToolFailureKind.Validation,
                    "The requested flow statistics period is not supported.",
                    "period must be exactly 'today', 'yesterday', or 'last7days'.");
            }
            if (!_source.IsAvailable)
            {
                return Failure(
                    CopilotToolFailureKind.Transient,
                    "The ColorVision business database is not connected.",
                    "Connect the configured MySQL database, then retry the read-only statistics query.");
            }

            var today = _localNowProvider().Date;
            var (startInclusive, endExclusive) = period switch
            {
                "yesterday" => (today.AddDays(-1), today),
                "last7days" => (today.AddDays(-6), today.AddDays(1)),
                _ => (today, today.AddDays(1)),
            };

            IReadOnlyList<CopilotFlowExecutionStatusCount> rows;
            try
            {
                rows = await _source.QueryAsync(startInclusive, endExclusive, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error("Copilot flow execution statistics query failed: " + ex.GetType().Name);
                return Failure(
                    CopilotToolFailureKind.Transient,
                    "The read-only flow statistics query failed.",
                    "The database did not return a usable aggregate result. See application logs for diagnostics.");
            }

            return new CopilotToolResult
            {
                ToolName = "QueryFlowExecutionStats",
                Success = true,
                Summary = BuildSummary(period, rows),
                Content = BuildContent(period, startInclusive, endExclusive, rows),
            };
        }

        private static bool TryReadPeriod(CopilotAgentToolInput input, out string period)
        {
            period = "today";
            if (!input.Arguments.TryGetValue("period", out var raw) || raw == null)
                return true;
            if (raw is string text)
                period = text.Trim().ToLowerInvariant();
            else if (raw is JsonElement { ValueKind: JsonValueKind.String } element)
                period = (element.GetString() ?? string.Empty).Trim().ToLowerInvariant();
            else
                return false;
            return period is "today" or "yesterday" or "last7days";
        }

        private static string BuildSummary(string period, IReadOnlyList<CopilotFlowExecutionStatusCount> rows)
        {
            var total = rows.Sum(row => Math.Max(0, row.ExecutionCount));
            var completed = CountFor(rows, FlowStatus.Completed);
            return $"Flow execution statistics for {period}: {total} attempts, {completed} completed.";
        }

        private static string BuildContent(
            string period,
            DateTime startInclusive,
            DateTime endExclusive,
            IReadOnlyList<CopilotFlowExecutionStatusCount> rows)
        {
            var total = rows.Sum(row => Math.Max(0, row.ExecutionCount));
            var completed = CountFor(rows, FlowStatus.Completed);
            var knownCodes = Enum.GetValues<FlowStatus>().Select(status => (int)status).ToHashSet();
            var unknown = rows.Where(row => !row.StatusCode.HasValue || !knownCodes.Contains(row.StatusCode.Value))
                .Sum(row => Math.Max(0, row.ExecutionCount));
            var weightedDuration = rows.Sum(row => Math.Max(0, row.ExecutionCount) * Math.Max(0, row.AverageDurationMs));
            var averageDuration = total == 0 ? 0 : weightedDuration / total;
            var completedRate = total == 0 ? 0 : completed * 100d / total;
            var builder = new StringBuilder();
            builder.AppendLine("[Flow Execution Statistics]");
            builder.AppendLine($"period: {period}");
            builder.AppendLine($"timezone: {TimeZoneInfo.Local.Id}");
            builder.AppendLine($"start_local_inclusive: {startInclusive:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"end_local_exclusive: {endExclusive:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine("source: t_scgd_measure_batch.create_date");
            builder.AppendLine($"total_attempts: {total}");
            foreach (var status in Enum.GetValues<FlowStatus>())
                builder.AppendLine($"{StatusName(status)}: {CountFor(rows, status)}");
            builder.AppendLine($"unknown: {unknown}");
            builder.AppendLine($"completed_rate_percent: {completedRate.ToString("0.##", CultureInfo.InvariantCulture)}");
            builder.AppendLine($"average_duration_ms: {averageDuration.ToString("0.##", CultureInfo.InvariantCulture)}");
            builder.AppendLine("semantics: one batch row is one flow execution attempt; Ready may include a run that stopped before its final status update.");
            return builder.ToString().TrimEnd();
        }

        private static int CountFor(IReadOnlyList<CopilotFlowExecutionStatusCount> rows, FlowStatus status)
        {
            return rows.Where(row => row.StatusCode == (int)status).Sum(row => Math.Max(0, row.ExecutionCount));
        }

        private static string StatusName(FlowStatus status)
        {
            return status switch
            {
                FlowStatus.Runing => "running",
                FlowStatus.OverTime => "overtime",
                _ => status.ToString().ToLowerInvariant(),
            };
        }

        private static CopilotToolResult Failure(CopilotToolFailureKind kind, string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = "QueryFlowExecutionStats",
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }
}
