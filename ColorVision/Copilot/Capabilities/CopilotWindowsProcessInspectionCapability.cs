using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed record CopilotWindowsProcessSnapshot(
        int ProcessId,
        string ProcessName,
        double? CpuPercent,
        long? WorkingSetBytes,
        long? PrivateMemoryBytes,
        int? ThreadCount,
        int? SessionId,
        string StartedAtUtc,
        string ExecutablePath);

    public interface ICopilotWindowsProcessProvider
    {
        Task<IReadOnlyList<CopilotWindowsProcessSnapshot>> CaptureAsync(
            int? processId,
            string processName,
            CancellationToken cancellationToken);
    }

    public sealed class CopilotWindowsProcessProvider : ICopilotWindowsProcessProvider
    {
        public const int SampleWindowMilliseconds = 250;

        public async Task<IReadOnlyList<CopilotWindowsProcessSnapshot>> CaptureAsync(
            int? processId,
            string processName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedName = NormalizeProcessName(processName);
            var selected = new List<(Process Process, string Name, TimeSpan? InitialProcessorTime)>();
            foreach (var process in Process.GetProcesses())
            {
                if (processId.HasValue && process.Id != processId.Value)
                {
                    process.Dispose();
                    continue;
                }

                var name = ReadString(() => process.ProcessName, 260);
                if (string.IsNullOrWhiteSpace(name)
                    || !string.IsNullOrWhiteSpace(normalizedName)
                    && !string.Equals(NormalizeProcessName(name), normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    process.Dispose();
                    continue;
                }

                selected.Add((process, name, ReadNullable(() => process.TotalProcessorTime)));
            }

            if (selected.Count == 0)
                return Array.Empty<CopilotWindowsProcessSnapshot>();

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await Task.Delay(SampleWindowMilliseconds, cancellationToken);
                var cpuPercentages = new Dictionary<int, double?>();
                foreach (var item in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var finalProcessorTime = ReadNullable(() => item.Process.TotalProcessorTime);
                    cpuPercentages[item.Process.Id] = CalculateCpuPercent(item.InitialProcessorTime, finalProcessorTime, stopwatch.Elapsed);
                }
                stopwatch.Stop();
                var includeExecutablePath = processId.HasValue || !string.IsNullOrWhiteSpace(normalizedName);
                var snapshots = new List<CopilotWindowsProcessSnapshot>(selected.Count);
                foreach (var item in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ReadNullable(() => item.Process.HasExited) == true)
                        continue;

                    snapshots.Add(new CopilotWindowsProcessSnapshot(
                        item.Process.Id,
                        item.Name,
                        cpuPercentages.GetValueOrDefault(item.Process.Id),
                        ReadNullable(() => item.Process.WorkingSet64),
                        ReadNullable(() => item.Process.PrivateMemorySize64),
                        ReadNullable(() => item.Process.Threads.Count),
                        ReadNullable(() => item.Process.SessionId),
                        ReadStartedAtUtc(item.Process),
                        includeExecutablePath ? ReadString(() => item.Process.MainModule?.FileName ?? string.Empty, 2_048) : string.Empty));
                }
                return snapshots;
            }
            finally
            {
                foreach (var item in selected)
                    item.Process.Dispose();
            }
        }

        internal static string NormalizeProcessName(string? processName)
        {
            var normalized = (processName ?? string.Empty).Trim();
            return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? normalized[..^4] : normalized;
        }

        private static double? CalculateCpuPercent(TimeSpan? before, TimeSpan? after, TimeSpan elapsed)
        {
            if (!before.HasValue || !after.HasValue || elapsed <= TimeSpan.Zero || after < before)
                return null;
            var processorCount = Math.Max(1, Environment.ProcessorCount);
            var percentage = (after.Value - before.Value).TotalMilliseconds / elapsed.TotalMilliseconds / processorCount * 100d;
            return Math.Round(Math.Clamp(percentage, 0d, 100d), 2, MidpointRounding.AwayFromZero);
        }

        private static string ReadStartedAtUtc(Process process)
        {
            try
            {
                return process.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return string.Empty;
            }
        }

        private static T? ReadNullable<T>(Func<T> getter) where T : struct
        {
            try
            {
                return getter();
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return null;
            }
        }

        private static string ReadString(Func<string> getter, int maxLength)
        {
            try
            {
                return Sanitize(getter(), maxLength);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return string.Empty;
            }
        }

        private static string Sanitize(string? value, int maxLength)
        {
            var normalized = new string((value ?? string.Empty)
                .Select(character => char.IsControl(character) ? ' ' : character)
                .ToArray())
                .Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }
    }

    public sealed class CopilotWindowsProcessInspectionService
    {
        public const int MaximumResults = 25;
        private readonly ICopilotWindowsProcessProvider _provider;

        public CopilotWindowsProcessInspectionService()
            : this(new CopilotWindowsProcessProvider())
        {
        }

        public CopilotWindowsProcessInspectionService(ICopilotWindowsProcessProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryReadInput(input, out var processId, out var processName, out var sortBy, out var limit, out var error))
                return Failure(CopilotToolFailureKind.Validation, "The Windows process inspection request is invalid.", error);

            IReadOnlyList<CopilotWindowsProcessSnapshot> captured;
            try
            {
                captured = await _provider.CaptureAsync(processId, processName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return Failure(
                    CopilotToolFailureKind.Internal,
                    "Windows process inspection failed.",
                    CopilotMcpAuditLogger.RedactText(ex.Message));
            }

            var normalizedName = CopilotWindowsProcessProvider.NormalizeProcessName(processName);
            var matches = captured
                .Where(snapshot => !processId.HasValue || snapshot.ProcessId == processId.Value)
                .Where(snapshot => string.IsNullOrWhiteSpace(normalizedName)
                    || string.Equals(CopilotWindowsProcessProvider.NormalizeProcessName(snapshot.ProcessName), normalizedName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var ordered = Order(matches, sortBy).Take(limit).Select(Normalize).ToArray();
            var result = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["filter_process_id"] = processId,
                ["filter_process_name"] = processName,
                ["sort_by"] = sortBy,
                ["limit"] = limit,
                ["sample_window_ms"] = CopilotWindowsProcessProvider.SampleWindowMilliseconds,
                ["matched_process_count"] = matches.Length,
                ["returned_process_count"] = ordered.Length,
                ["entries_truncated"] = matches.Length > ordered.Length,
                ["processes"] = ordered.Select(snapshot => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["process_id"] = snapshot.ProcessId,
                    ["process_name"] = snapshot.ProcessName,
                    ["cpu_percent"] = snapshot.CpuPercent,
                    ["working_set_bytes"] = snapshot.WorkingSetBytes,
                    ["private_memory_bytes"] = snapshot.PrivateMemoryBytes,
                    ["thread_count"] = snapshot.ThreadCount,
                    ["session_id"] = snapshot.SessionId,
                    ["started_at_utc"] = snapshot.StartedAtUtc,
                    ["executable_path"] = snapshot.ExecutablePath,
                }).ToArray(),
            };
            return new CopilotToolResult
            {
                ToolName = "InspectWindowsProcesses",
                Success = true,
                Summary = BuildSummary(processId, processName, sortBy, matches.Length, ordered.Length),
                Content = $"[Windows Process Inspection]\nresult_json: {JsonSerializer.Serialize(result)}",
            };
        }

        private static IOrderedEnumerable<CopilotWindowsProcessSnapshot> Order(
            IEnumerable<CopilotWindowsProcessSnapshot> snapshots,
            string sortBy)
        {
            return sortBy switch
            {
                "memory" => snapshots.OrderByDescending(snapshot => snapshot.WorkingSetBytes ?? -1)
                    .ThenBy(snapshot => snapshot.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(snapshot => snapshot.ProcessId),
                "name" => snapshots.OrderBy(snapshot => snapshot.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(snapshot => snapshot.ProcessId),
                "process_id" => snapshots.OrderBy(snapshot => snapshot.ProcessId),
                _ => snapshots.OrderByDescending(snapshot => snapshot.CpuPercent ?? -1)
                    .ThenByDescending(snapshot => snapshot.WorkingSetBytes ?? -1)
                    .ThenBy(snapshot => snapshot.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(snapshot => snapshot.ProcessId),
            };
        }

        private static CopilotWindowsProcessSnapshot Normalize(CopilotWindowsProcessSnapshot snapshot)
        {
            return snapshot with
            {
                ProcessName = Sanitize(snapshot.ProcessName, 260),
                CpuPercent = snapshot.CpuPercent.HasValue ? Math.Round(Math.Clamp(snapshot.CpuPercent.Value, 0d, 100d), 2, MidpointRounding.AwayFromZero) : null,
                WorkingSetBytes = NormalizeNonNegative(snapshot.WorkingSetBytes),
                PrivateMemoryBytes = NormalizeNonNegative(snapshot.PrivateMemoryBytes),
                ThreadCount = NormalizeNonNegative(snapshot.ThreadCount),
                SessionId = NormalizeNonNegative(snapshot.SessionId),
                StartedAtUtc = Sanitize(snapshot.StartedAtUtc, 64),
                ExecutablePath = Sanitize(snapshot.ExecutablePath, 2_048),
            };
        }

        private static long? NormalizeNonNegative(long? value) => value.HasValue ? Math.Max(0L, value.Value) : null;

        private static int? NormalizeNonNegative(int? value) => value.HasValue ? Math.Max(0, value.Value) : null;

        private static bool TryReadInput(
            CopilotAgentToolInput input,
            out int? processId,
            out string processName,
            out string sortBy,
            out int limit,
            out string error)
        {
            processId = null;
            processName = string.Empty;
            sortBy = "cpu";
            limit = 10;
            error = string.Empty;

            if (!TryReadOptionalInt(input, "processId", out processId, out error)
                || processId.HasValue && processId is < 1)
            {
                error = string.IsNullOrWhiteSpace(error) ? "Argument 'processId' must be a positive integer." : error;
                return false;
            }
            if (!TryReadOptionalString(input, "name", out processName, out error))
                return false;
            if (processName.Length > 260)
            {
                error = "Argument 'name' must contain at most 260 characters.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(processName)
                && string.IsNullOrWhiteSpace(CopilotWindowsProcessProvider.NormalizeProcessName(processName)))
            {
                error = "Argument 'name' must identify a process name, not only the .exe suffix.";
                return false;
            }
            if (!TryReadOptionalString(input, "sortBy", out var requestedSort, out error))
                return false;
            if (!string.IsNullOrWhiteSpace(requestedSort))
                sortBy = requestedSort.Trim().ToLowerInvariant();
            if (sortBy is not ("cpu" or "memory" or "name" or "process_id"))
            {
                error = "Argument 'sortBy' must be one of: cpu, memory, name, process_id.";
                return false;
            }
            if (!TryReadOptionalInt(input, "limit", out var requestedLimit, out error))
                return false;
            if (requestedLimit.HasValue)
                limit = requestedLimit.Value;
            if (limit is < 1 or > MaximumResults)
            {
                error = $"Argument 'limit' must be between 1 and {MaximumResults}.";
                return false;
            }
            return true;
        }

        private static bool TryReadOptionalInt(CopilotAgentToolInput input, string name, out int? value, out string error)
        {
            value = null;
            error = string.Empty;
            var pair = input.Arguments.FirstOrDefault(argument => string.Equals(argument.Key, name, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                return true;
            value = pair.Value switch
            {
                byte number => number,
                short number => number,
                int number => number,
                long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
                JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var number) => number,
                _ => null,
            };
            if (value.HasValue)
                return true;
            error = $"Argument '{name}' must be an integer.";
            return false;
        }

        private static bool TryReadOptionalString(CopilotAgentToolInput input, string name, out string value, out string error)
        {
            value = string.Empty;
            error = string.Empty;
            var pair = input.Arguments.FirstOrDefault(argument => string.Equals(argument.Key, name, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                return true;
            value = pair.Value switch
            {
                string text => text.Trim(),
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()?.Trim() ?? string.Empty,
                _ => string.Empty,
            };
            if (pair.Value is string || pair.Value is JsonElement { ValueKind: JsonValueKind.String })
                return true;
            error = $"Argument '{name}' must be a string.";
            return false;
        }

        private static string BuildSummary(int? processId, string processName, string sortBy, int matchedCount, int returnedCount)
        {
            if (matchedCount == 0)
            {
                if (processId.HasValue)
                    return $"No running Windows process matched PID {processId.Value}.";
                if (!string.IsNullOrWhiteSpace(processName))
                    return $"No running Windows process matched name {processName}.";
                return "No running Windows processes were available to inspect.";
            }
            if (processId.HasValue || !string.IsNullOrWhiteSpace(processName))
                return $"Found {matchedCount} matching running Windows process(es).";
            var sortLabel = sortBy == "cpu" ? "recent CPU usage" : sortBy == "memory" ? "working-set memory" : sortBy.Replace('_', ' ');
            return $"Returned {returnedCount} running Windows process(es) sorted by {sortLabel}.";
        }

        private static string Sanitize(string? value, int maxLength)
        {
            var normalized = new string((value ?? string.Empty)
                .Select(character => char.IsControl(character) ? ' ' : character)
                .ToArray())
                .Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }

        private static CopilotToolResult Failure(CopilotToolFailureKind kind, string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = "InspectWindowsProcesses",
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }
}
