using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed record OperationsApplicationRestartResult(bool Accepted, string EvidenceId);

    public interface IOperationsApplicationRestartController
    {
        OperationsApplicationRestartResult RequestRestart(string jobId);
    }

    public sealed class UnavailableOperationsApplicationRestartController : IOperationsApplicationRestartController
    {
        public static UnavailableOperationsApplicationRestartController Instance { get; } = new();

        private UnavailableOperationsApplicationRestartController()
        {
        }

        public OperationsApplicationRestartResult RequestRestart(string jobId) =>
            new(false, "application_restart_controller_unavailable");
    }

    public sealed class OperationsApplicationRestartHandoff
    {
        private sealed class RestartHandoffState
        {
            public int SchemaVersion { get; set; } = 1;

            public string JobId { get; set; } = string.Empty;

            public DateTimeOffset ScheduledAt { get; set; }
        }

        private static readonly TimeSpan MaximumHandoffAge = TimeSpan.FromMinutes(5);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        private readonly string _path;

        public OperationsApplicationRestartHandoff(string? path = null)
        {
            _path = path ?? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision", "Operations", "application-restart-handoff.json");
        }

        public string Path => _path;

        public void Prepare(string jobId, DateTimeOffset? scheduledAt = null)
        {
            ValidateJobId(jobId);
            string? directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            RestartHandoffState state = new()
            {
                JobId = jobId,
                ScheduledAt = scheduledAt ?? DateTimeOffset.UtcNow,
            };
            string temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(temporaryPath, _path, true);
        }

        public OperationsJob? CompletePending(
            OperationsWorkStore workStore,
            string? expectedJobId,
            DateTimeOffset? observedAt = null)
        {
            ArgumentNullException.ThrowIfNull(workStore);
            if (!File.Exists(_path))
                return null;

            try
            {
                RestartHandoffState? state = JsonSerializer.Deserialize<RestartHandoffState>(
                    File.ReadAllText(_path), JsonOptions);
                if (state == null || state.SchemaVersion != 1 || !IsValidJobId(state.JobId))
                    return null;

                OperationsJob? job = workStore.GetJobs().FirstOrDefault(item =>
                    item.JobId == state.JobId
                    && item.CapabilityId == "ops.application.restart"
                    && item.Status == "executing");
                if (job == null)
                    return null;

                if (!string.Equals(expectedJobId, state.JobId, StringComparison.Ordinal))
                {
                    return workStore.CompleteJob(
                        state.JobId, false, "application_restart:handoff_mismatch");
                }

                DateTimeOffset now = observedAt ?? DateTimeOffset.UtcNow;
                bool fresh = state.ScheduledAt <= now
                    && now - state.ScheduledAt <= MaximumHandoffAge;
                return workStore.CompleteJob(
                    state.JobId,
                    fresh,
                    fresh ? "application_restart:completed" : "application_restart:handoff_expired");
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                TryDelete();
            }
        }

        public void Clear(string jobId)
        {
            ValidateJobId(jobId);
            if (!File.Exists(_path))
                return;

            try
            {
                RestartHandoffState? state = JsonSerializer.Deserialize<RestartHandoffState>(
                    File.ReadAllText(_path), JsonOptions);
                if (state?.JobId == jobId)
                    TryDelete();
            }
            catch (JsonException)
            {
                TryDelete();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void ValidateJobId(string jobId)
        {
            if (!IsValidJobId(jobId))
                throw new ArgumentException("The Operations job id is invalid.", nameof(jobId));
        }

        private static bool IsValidJobId(string jobId) =>
            jobId.Length == 32 && jobId.All(char.IsLetterOrDigit);

        private void TryDelete()
        {
            try
            {
                File.Delete(_path);
                File.Delete(_path + ".tmp");
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
