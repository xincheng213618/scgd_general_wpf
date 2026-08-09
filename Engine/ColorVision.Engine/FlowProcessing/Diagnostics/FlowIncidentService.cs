using ColorVision.UI;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal static class FlowIncidentStates
    {
        public const string Open = "Open";
        public const string Acknowledged = "Acknowledged";
        public const string Resolved = "Resolved";
        public const string Active = "Active";
        public const string All = "All";
    }

    internal sealed class FlowIncidentQuery
    {
        public string State { get; init; } = FlowIncidentStates.Active;

        public string? Severity { get; init; }

        public string? Kind { get; init; }

        public string? SearchText { get; init; }

        public int PageNumber { get; init; } = 1;

        public int PageSize { get; init; } = 50;
    }

    internal sealed record FlowIncidentListItem(
        FlowIncident Incident,
        FlowRunRecord? Run)
    {
        public long IncidentId => Incident.Id;

        public int RunRecordId => Incident.RunRecordId;

        public string State => Incident.State;

        public string Severity => Incident.Severity;

        public string Kind => Incident.Kind;

        public string Summary => Incident.Summary;

        public string? NodeId => Incident.NodeId;

        public DateTime DetectedTimeUtc => Incident.DetectedTimeUtc;

        public string FlowIdentifier =>
            Run?.FlowKey
            ?? (Run?.TemplateId > 0 ? $"template:{Run.TemplateId}" : string.Empty);

        public string FlowName => Run?.FlowName ?? string.Empty;

        public string RunIdentifier =>
            !string.IsNullOrWhiteSpace(Run?.RunKey)
                ? Run!.RunKey!
                : $"run:{Incident.RunRecordId}";
    }

    internal sealed record FlowIncidentPage(
        IReadOnlyList<FlowIncidentListItem> Items,
        int PageNumber,
        int PageSize,
        int TotalCount)
    {
        public int TotalPages =>
            TotalCount == 0
                ? 1
                : (TotalCount + PageSize - 1) / PageSize;
    }

    internal sealed record FlowIncidentDetail(
        FlowIncident Incident,
        FlowRunRecord? Run,
        IReadOnlyList<FlowExecutionEvent> Events,
        IReadOnlyList<FlowNodeAttempt> Attempts)
    {
        public FlowNodeAttempt? LinkedAttempt =>
            Incident.AttemptId.HasValue
                ? Attempts.FirstOrDefault(
                    attempt => attempt.Id == Incident.AttemptId.Value)
                : null;
    }

    /// <summary>
    /// Read/triage surface for flow incidents. The runtime journal remains
    /// fail-open and does not depend on this service.
    /// </summary>
    internal sealed class FlowIncidentService : IDisposable
    {
        private const int MaximumPageSize = 200;
        private static readonly object ActionLock = new();

        private readonly SqlSugarClient db;
        private readonly bool ownsDb;
        private readonly object dbLock = new();
        private bool disposed;

        public FlowIncidentService()
            : this(CreateDefaultDb(), true)
        {
        }

        internal FlowIncidentService(SqlSugarClient db)
            : this(db, false)
        {
        }

        private FlowIncidentService(SqlSugarClient db, bool ownsDb)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.ownsDb = ownsDb;
            FlowDiagnosticsMaintenanceGate.RunExclusive(
                () => FlowDiagnosticsSchemaMigrator.EnsureSchema(db));
        }

        public FlowIncidentPage Query(FlowIncidentQuery? query = null)
        {
            lock (dbLock)
            {
                return QueryCore(query);
            }
        }

        private FlowIncidentPage QueryCore(FlowIncidentQuery? query)
        {
            ThrowIfDisposed();
            query ??= new FlowIncidentQuery();
            int pageNumber = Math.Max(1, query.PageNumber);
            int pageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize);
            string state = NormalizeFilter(query.State)
                ?? FlowIncidentStates.Active;
            string? severity = NormalizeFilter(query.Severity);
            string? kind = NormalizeFilter(query.Kind);
            string? searchText = NormalizeFilter(query.SearchText);

            ISugarQueryable<FlowIncident> incidents =
                db.Queryable<FlowIncident>();
            if (string.Equals(
                state,
                FlowIncidentStates.Active,
                StringComparison.OrdinalIgnoreCase))
            {
                incidents = incidents.Where(
                    incident => incident.State != FlowIncidentStates.Resolved);
            }
            else if (!string.Equals(
                state,
                FlowIncidentStates.All,
                StringComparison.OrdinalIgnoreCase))
            {
                incidents = incidents.Where(
                    incident => incident.State == state);
            }
            if (severity != null)
            {
                incidents = incidents.Where(
                    incident => incident.Severity == severity);
            }
            if (kind != null)
            {
                incidents = incidents.Where(
                    incident => incident.Kind == kind);
            }
            if (searchText != null)
            {
                incidents = incidents.Where(
                    incident =>
                        incident.Summary.Contains(searchText)
                        || (incident.NodeId != null
                            && incident.NodeId.Contains(searchText))
                        || (incident.DetailsJson != null
                            && incident.DetailsJson.Contains(searchText)));
            }

            int totalCount = 0;
            List<FlowIncident> pageIncidents = incidents
                .OrderByDescending(incident => incident.DetectedTimeUtc)
                .OrderByDescending(incident => incident.Id)
                .ToPageList(pageNumber, pageSize, ref totalCount);

            int[] runIds = pageIncidents
                .Select(incident => incident.RunRecordId)
                .Distinct()
                .ToArray();
            Dictionary<int, FlowRunRecord> runs = runIds.Length == 0
                ? new Dictionary<int, FlowRunRecord>()
                : db.Queryable<FlowRunRecord>()
                    .In(runIds)
                    .ToList()
                    .ToDictionary(run => run.Id);
            List<FlowIncidentListItem> items = pageIncidents
                .Select(incident =>
                    new FlowIncidentListItem(
                        incident,
                        runs.GetValueOrDefault(incident.RunRecordId)))
                .ToList();
            return new FlowIncidentPage(
                items,
                pageNumber,
                pageSize,
                totalCount);
        }

        public FlowIncidentDetail GetDetail(long incidentId)
        {
            lock (dbLock)
            {
                return GetDetailCore(incidentId);
            }
        }

        private FlowIncidentDetail GetDetailCore(long incidentId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(incidentId);
            ThrowIfDisposed();

            FlowIncident? incident =
                db.Queryable<FlowIncident>().InSingle(incidentId);
            if (incident == null)
            {
                throw new InvalidOperationException(
                    $"找不到 Incident {incidentId}。");
            }

            FlowRunRecord? run =
                db.Queryable<FlowRunRecord>().InSingle(incident.RunRecordId);
            List<FlowExecutionEvent> events =
                db.Queryable<FlowExecutionEvent>()
                    .Where(item => item.RunRecordId == incident.RunRecordId)
                    .OrderBy(item => item.SequenceNo)
                    .ToList();
            List<FlowNodeAttempt> attempts =
                db.Queryable<FlowNodeAttempt>()
                    .Where(item => item.RunRecordId == incident.RunRecordId)
                    .OrderBy(item => item.StartedTimeUtc)
                    .OrderBy(item => item.Id)
                    .ToList();
            return new FlowIncidentDetail(incident, run, events, attempts);
        }

        public FlowIncident Acknowledge(
            long incidentId,
            string operatorName,
            string? note,
            DateTime? acknowledgedTimeUtc = null)
        {
            string normalizedOperator = NormalizeRequired(
                operatorName,
                nameof(operatorName),
                "确认人不能为空。");
            string? normalizedNote = NormalizeOptional(note);
            DateTime actionTimeUtc = NormalizeUtc(
                acknowledgedTimeUtc ?? DateTime.UtcNow);
            return UpdateIncident(incidentId, incident =>
            {
                if (string.Equals(
                    incident.State,
                    FlowIncidentStates.Resolved,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "已关闭的 Incident 不能再次确认。");
                }
                if (string.Equals(
                    incident.State,
                    FlowIncidentStates.Acknowledged,
                    StringComparison.Ordinal))
                {
                    return incident;
                }
                if (!string.Equals(
                    incident.State,
                    FlowIncidentStates.Open,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Incident 状态 {incident.State} 不能确认。");
                }

                incident.State = FlowIncidentStates.Acknowledged;
                incident.AcknowledgedTimeUtc = actionTimeUtc;
                incident.AcknowledgedOperator = normalizedOperator;
                incident.AcknowledgmentNote = normalizedNote;
                return incident;
            });
        }

        public FlowIncident Resolve(
            long incidentId,
            string operatorName,
            string resolution,
            DateTime? resolvedTimeUtc = null)
        {
            string normalizedOperator = NormalizeRequired(
                operatorName,
                nameof(operatorName),
                "关闭人不能为空。");
            string normalizedResolution = NormalizeRequired(
                resolution,
                nameof(resolution),
                "关闭备注不能为空。");
            DateTime actionTimeUtc = NormalizeUtc(
                resolvedTimeUtc ?? DateTime.UtcNow);
            return UpdateIncident(incidentId, incident =>
            {
                if (string.Equals(
                    incident.State,
                    FlowIncidentStates.Resolved,
                    StringComparison.Ordinal))
                {
                    return incident;
                }
                if (!string.Equals(
                        incident.State,
                        FlowIncidentStates.Open,
                        StringComparison.Ordinal)
                    && !string.Equals(
                        incident.State,
                        FlowIncidentStates.Acknowledged,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Incident 状态 {incident.State} 不能关闭。");
                }

                incident.State = FlowIncidentStates.Resolved;
                incident.ResolvedTimeUtc = actionTimeUtc;
                incident.Resolution = normalizedResolution;
                incident.OperatorName = normalizedOperator;
                return incident;
            });
        }

        public void Dispose()
        {
            lock (dbLock)
            {
                if (disposed)
                    return;
                disposed = true;
                if (ownsDb)
                    db.Dispose();
            }
        }

        private FlowIncident UpdateIncident(
            long incidentId,
            Func<FlowIncident, FlowIncident> update)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(incidentId);
            ArgumentNullException.ThrowIfNull(update);
            ThrowIfDisposed();

            lock (FlowDiagnosticsMaintenanceGate.SyncRoot)
            {
                lock (ActionLock)
                {
                    lock (dbLock)
                    {
                        ThrowIfDisposed();
                        db.Ado.BeginTran();
                        try
                        {
                            FlowIncident? incident =
                                db.Queryable<FlowIncident>().InSingle(incidentId);
                            if (incident == null)
                            {
                                throw new InvalidOperationException(
                                    $"找不到 Incident {incidentId}。");
                            }

                            FlowIncident updated = update(incident);
                            db.Updateable(updated).ExecuteCommand();
                            db.Ado.CommitTran();
                            return updated;
                        }
                        catch
                        {
                            try
                            {
                                db.Ado.RollbackTran();
                            }
                            catch
                            {
                                // Preserve the original incident action failure.
                            }
                            throw;
                        }
                    }
                }
            }
        }

        private static string NormalizeRequired(
            string value,
            string parameterName,
            string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(errorMessage, parameterName);
            return value.Trim();
        }

        private static string? NormalizeFilter(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }

        private static SqlSugarClient CreateDefaultDb()
        {
            FlowNodeRecordConfig config =
                ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>();
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={config.SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
