using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed record CopilotWindowsServiceSnapshot(
        string ServiceName,
        string DisplayName,
        string Status,
        string ServiceType,
        bool? CanStop,
        bool? CanPauseAndContinue,
        bool? CanShutdown);

    public interface ICopilotWindowsServiceProvider
    {
        IReadOnlyList<CopilotWindowsServiceSnapshot> Capture(string query, string status, CancellationToken cancellationToken);
    }

    public sealed class CopilotWindowsServiceProvider : ICopilotWindowsServiceProvider
    {
        public IReadOnlyList<CopilotWindowsServiceSnapshot> Capture(
            string query,
            string status,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows())
                return Array.Empty<CopilotWindowsServiceSnapshot>();

            var snapshots = new List<CopilotWindowsServiceSnapshot>();
            var controllers = ServiceController.GetServices();
            try
            {
                foreach (var controller in controllers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var serviceName = ReadString(() => controller.ServiceName, 256);
                    var displayName = ReadString(() => controller.DisplayName, 512);
                    if (!MatchesQuery(serviceName, displayName, query))
                        continue;

                    var currentStatus = ReadStatus(controller);
                    if (!MatchesStatus(currentStatus, status))
                        continue;

                    snapshots.Add(new CopilotWindowsServiceSnapshot(
                        serviceName,
                        displayName,
                        currentStatus,
                        ReadString(() => controller.ServiceType.ToString(), 128),
                        ReadNullable(() => controller.CanStop),
                        ReadNullable(() => controller.CanPauseAndContinue),
                        ReadNullable(() => controller.CanShutdown)));
                }
                return snapshots;
            }
            finally
            {
                foreach (var controller in controllers)
                    controller.Dispose();
            }
        }

        internal static bool MatchesQuery(string serviceName, string displayName, string query)
        {
            return string.IsNullOrWhiteSpace(query)
                || serviceName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || displayName.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool MatchesStatus(string serviceStatus, string requestedStatus)
        {
            return requestedStatus switch
            {
                "running" => serviceStatus == "running",
                "stopped" => serviceStatus == "stopped",
                "paused" => serviceStatus == "paused",
                "pending" => serviceStatus.EndsWith("_pending", StringComparison.Ordinal),
                _ => true,
            };
        }

        private static string ReadStatus(ServiceController controller)
        {
            var status = ReadNullable(() => controller.Status);
            return status switch
            {
                ServiceControllerStatus.Running => "running",
                ServiceControllerStatus.Stopped => "stopped",
                ServiceControllerStatus.Paused => "paused",
                ServiceControllerStatus.StartPending => "start_pending",
                ServiceControllerStatus.StopPending => "stop_pending",
                ServiceControllerStatus.ContinuePending => "continue_pending",
                ServiceControllerStatus.PausePending => "pause_pending",
                _ => "unknown",
            };
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

        private static string ReadString(Func<string> getter, int maximumLength)
        {
            try
            {
                return Sanitize(getter(), maximumLength);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return string.Empty;
            }
        }

        internal static string Sanitize(string? value, int maximumLength)
        {
            var normalized = new string((value ?? string.Empty)
                .Select(character => char.IsControl(character) ? ' ' : character)
                .ToArray())
                .Trim();
            return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
        }
    }

    public sealed class CopilotWindowsServiceInspectionService
    {
        public const int MaximumResults = 50;
        private readonly ICopilotWindowsServiceProvider _provider;

        public CopilotWindowsServiceInspectionService()
            : this(new CopilotWindowsServiceProvider())
        {
        }

        public CopilotWindowsServiceInspectionService(ICopilotWindowsServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            cancellationToken.ThrowIfCancellationRequested();

            if (!OperatingSystem.IsWindows())
                return Task.FromResult(Failure(CopilotToolFailureKind.NotFound, "Windows service inspection is unavailable.", "This fixed diagnostic is available only on Windows."));
            if (!TryReadInput(input, out var query, out var status, out var sortBy, out var limit, out var error))
                return Task.FromResult(Failure(CopilotToolFailureKind.Validation, "The Windows service inspection request is invalid.", error));

            IReadOnlyList<CopilotWindowsServiceSnapshot> captured;
            try
            {
                captured = _provider.Capture(query, status, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return Task.FromResult(Failure(CopilotToolFailureKind.Internal, "Windows service inspection failed.", "The fixed Windows service provider failed."));
            }

            var matches = captured
                .Where(snapshot => CopilotWindowsServiceProvider.MatchesQuery(snapshot.ServiceName, snapshot.DisplayName, query))
                .Where(snapshot => CopilotWindowsServiceProvider.MatchesStatus(snapshot.Status, status))
                .Select(Normalize)
                .ToArray();
            var ordered = Order(matches, query, sortBy).Take(limit).ToArray();
            var result = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["query"] = query,
                ["status_filter"] = status,
                ["sort_by"] = sortBy,
                ["limit"] = limit,
                ["matched_service_count"] = matches.Length,
                ["returned_service_count"] = ordered.Length,
                ["entries_truncated"] = matches.Length > ordered.Length,
                ["services"] = ordered.Select(snapshot => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["service_name"] = snapshot.ServiceName,
                    ["display_name"] = snapshot.DisplayName,
                    ["status"] = snapshot.Status,
                    ["service_type"] = snapshot.ServiceType,
                    ["can_stop"] = snapshot.CanStop,
                    ["can_pause_and_continue"] = snapshot.CanPauseAndContinue,
                    ["can_shutdown"] = snapshot.CanShutdown,
                }).ToArray(),
            };

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = "InspectWindowsServices",
                Success = true,
                Summary = BuildSummary(query, status, matches.Length, ordered.Length),
                Content = $"[Windows Service Inspection]\nresult_json: {JsonSerializer.Serialize(result)}",
            });
        }

        private static IOrderedEnumerable<CopilotWindowsServiceSnapshot> Order(
            IEnumerable<CopilotWindowsServiceSnapshot> snapshots,
            string query,
            string sortBy)
        {
            var exactFirst = snapshots.OrderByDescending(snapshot => IsExactMatch(snapshot, query));
            return sortBy switch
            {
                "display_name" => exactFirst.ThenBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(snapshot => snapshot.ServiceName, StringComparer.OrdinalIgnoreCase),
                "status" => exactFirst.ThenBy(snapshot => snapshot.Status, StringComparer.Ordinal)
                    .ThenBy(snapshot => snapshot.ServiceName, StringComparer.OrdinalIgnoreCase),
                _ => exactFirst.ThenBy(snapshot => snapshot.ServiceName, StringComparer.OrdinalIgnoreCase),
            };
        }

        private static bool IsExactMatch(CopilotWindowsServiceSnapshot snapshot, string query)
        {
            return !string.IsNullOrWhiteSpace(query)
                && (string.Equals(snapshot.ServiceName, query, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(snapshot.DisplayName, query, StringComparison.OrdinalIgnoreCase));
        }

        private static CopilotWindowsServiceSnapshot Normalize(CopilotWindowsServiceSnapshot snapshot)
        {
            return snapshot with
            {
                ServiceName = CopilotWindowsServiceProvider.Sanitize(snapshot.ServiceName, 256),
                DisplayName = CopilotWindowsServiceProvider.Sanitize(snapshot.DisplayName, 512),
                Status = NormalizeStatus(snapshot.Status),
                ServiceType = CopilotWindowsServiceProvider.Sanitize(snapshot.ServiceType, 128),
            };
        }

        private static string NormalizeStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalized is "running" or "stopped" or "paused" or "start_pending" or "stop_pending" or "continue_pending" or "pause_pending"
                ? normalized
                : "unknown";
        }

        private static bool TryReadInput(
            CopilotAgentToolInput input,
            out string query,
            out string status,
            out string sortBy,
            out int limit,
            out string error)
        {
            query = string.Empty;
            status = "all";
            sortBy = "name";
            limit = 20;
            error = string.Empty;

            if (!TryReadOptionalString(input, "query", out query, out error))
                return false;
            if (query.Length > 256)
            {
                error = "Argument 'query' must contain at most 256 characters.";
                return false;
            }
            query = CopilotWindowsServiceProvider.Sanitize(query, 256);
            if (!TryReadOptionalString(input, "status", out var requestedStatus, out error))
                return false;
            if (!string.IsNullOrWhiteSpace(requestedStatus))
                status = requestedStatus.Trim().ToLowerInvariant();
            if (status is not ("all" or "running" or "stopped" or "paused" or "pending"))
            {
                error = "Argument 'status' must be one of: all, running, stopped, paused, pending.";
                return false;
            }
            if (!TryReadOptionalString(input, "sortBy", out var requestedSort, out error))
                return false;
            if (!string.IsNullOrWhiteSpace(requestedSort))
                sortBy = requestedSort.Trim().ToLowerInvariant();
            if (sortBy is not ("name" or "display_name" or "status"))
            {
                error = "Argument 'sortBy' must be one of: name, display_name, status.";
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

        private static string BuildSummary(string query, string status, int matchedCount, int returnedCount)
        {
            if (matchedCount == 0)
            {
                if (!string.IsNullOrWhiteSpace(query))
                    return $"No installed Windows service matched query '{query}'.";
                if (status != "all")
                    return $"No installed Windows service matched status {status}.";
                return "No installed Windows services were available to inspect.";
            }
            if (matchedCount == returnedCount)
                return $"Found {matchedCount} matching installed Windows service(s).";
            return $"Returned {returnedCount} of {matchedCount} matching installed Windows service(s).";
        }

        private static CopilotToolResult Failure(CopilotToolFailureKind kind, string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = "InspectWindowsServices",
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }
}
