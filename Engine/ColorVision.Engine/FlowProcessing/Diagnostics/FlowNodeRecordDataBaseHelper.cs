using ColorVision.UI;
using log4net;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ParameterType = System.Data.DbType;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal readonly record struct FlowAnalysisDeleteResult(int RecordCount, int MessageCount);

    public static class FlowNodeRecordDataBaseHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowNodeRecordDataBaseHelper));
        private static volatile bool _initialized;
        private static Exception? _initializationException;
        private static DateTime _nextInitializationAttemptUtc;
        private static readonly object _initLock = new object();
        private static readonly TimeSpan _synchronousWriteTimeout = TimeSpan.FromSeconds(5);

        // Shared persistent connection for the write queue
        private static SqlSugarClient? _sharedDb;
        private static string? _preparedConnectionString;
        private static int _preparedSchemaVersion;
        private static readonly BlockingCollection<Action<SqlSugarClient>> _writeQueue = new BlockingCollection<Action<SqlSugarClient>>();
        private static readonly ConcurrentDictionary<long, Task> _pendingWriteProducers = new ConcurrentDictionary<long, Task>();
        private static Thread _writerThread;
        private static long _pendingWriteProducerId;

        private static SqlSugarClient CreateDb()
        {
            FlowNodeRecordConfig config = ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>();
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={config.SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = false
            });
        }

        private static bool EnsureInitialized()
        {
            if (_initialized) return true;
            if (_initializationException != null
                && DateTime.UtcNow < _nextInitializationAttemptUtc)
            {
                return false;
            }

            lock (_initLock)
            {
                if (_initialized) return true;
                if (_initializationException != null
                    && DateTime.UtcNow < _nextInitializationAttemptUtc)
                {
                    return false;
                }

                try
                {
                    SqlSugarClient sharedDb = CreateDb();
                    _sharedDb = sharedDb;
                    FlowDiagnosticsMaintenanceGate.RunExclusive(() =>
                    {
                        FlowDiagnosticsSchemaMigrator.EnsureSchema(sharedDb);
                        string connectionString = sharedDb.CurrentConnectionConfig.ConnectionString;
                        string dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
                        _preparedConnectionString = Path.IsPathFullyQualified(dataSource) ? connectionString : null;
                        if (_preparedConnectionString != null)
                            _preparedSchemaVersion = sharedDb.Ado.GetInt("PRAGMA schema_version;");
                    });

                    _writerThread = new Thread(WriteLoop)
                    {
                        IsBackground = true,
                        Name = "FlowNodeRecord-Writer"
                    };
                    _writerThread.Start();

                    _initialized = true;
                    _initializationException = null;
                    _nextInitializationAttemptUtc = default;
                    return true;
                }
                catch (Exception ex)
                {
                    _initializationException = ex;
                    _nextInitializationAttemptUtc = DateTime.UtcNow.AddSeconds(30);
                    _sharedDb?.Dispose();
                    _sharedDb = null;
                    log.Error("初始化FlowNodeRecord表失败", ex);
                    return false;
                }
            }
        }

        internal static bool HasPreparedSchema(SqlSugarClient db) =>
            UsesWriterDatabase(db)
            && db.Ado.GetInt("PRAGMA schema_version;") == Volatile.Read(ref _preparedSchemaVersion);

        internal static void RefreshPreparedSchema(SqlSugarClient db)
        {
            if (UsesWriterDatabase(db))
                Volatile.Write(ref _preparedSchemaVersion, db.Ado.GetInt("PRAGMA schema_version;"));
        }

        private static bool UsesWriterDatabase(SqlSugarClient db) =>
            _initialized && _preparedConnectionString != null
            && db.CurrentConnectionConfig.DbType == DbType.Sqlite
            && string.Equals(db.CurrentConnectionConfig.ConnectionString, _preparedConnectionString, StringComparison.Ordinal);

        private static void WriteLoop()
        {
            foreach (var action in _writeQueue.GetConsumingEnumerable())
            {
                try
                {
                    SqlSugarClient? sharedDb = _sharedDb;
                    if (sharedDb == null)
                    {
                        log.Error("FlowNodeRecord写入队列没有可用数据库连接。");
                        continue;
                    }
                    FlowDiagnosticsMaintenanceGate.RunExclusive(
                        () => action(sharedDb));
                }
                catch (Exception ex)
                {
                    log.Error("FlowNodeRecord写入队列执行失败", ex);
                }
            }
        }

        internal static bool TryGetSynchronousWriteResult<T>(
            Task<T> task,
            TimeSpan timeout,
            out T result)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentOutOfRangeException.ThrowIfLessThan(
                timeout,
                TimeSpan.Zero);

            try
            {
                if (!task.Wait(timeout))
                {
                    result = default!;
                    return false;
                }

                result = task.GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                log.Error("等待流程诊断同步写入失败。", ex);
                result = default!;
                return false;
            }
        }

        public static bool FlushPendingWrites(TimeSpan? timeout = null)
        {
            EnsureInitialized();
            if (!_initialized)
                return false;

            TimeSpan effectiveTimeout = timeout ?? TimeSpan.FromSeconds(2);
            DateTime deadline = DateTime.UtcNow + effectiveTimeout;
            while (true)
            {
                Task[] pending = _pendingWriteProducers.Values
                    .Where(task => !task.IsCompleted)
                    .ToArray();
                if (pending.Length == 0)
                    break;

                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    return false;

                try
                {
                    if (!Task.WhenAll(pending).Wait(remaining))
                        return false;
                }
                catch (AggregateException ex)
                {
                    log.Warn("等待流程节点写入任务完成时发生异常。", ex.Flatten());
                }
            }

            TimeSpan barrierTimeout = deadline - DateTime.UtcNow;
            if (barrierTimeout <= TimeSpan.Zero)
                return false;

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _writeQueue.Add(_ => completion.TrySetResult(true));
            return completion.Task.Wait(barrierTimeout);
        }

        public static void TrackPendingWrite(Task writeTask)
        {
            if (writeTask == null)
                return;

            long id = Interlocked.Increment(ref _pendingWriteProducerId);
            _pendingWriteProducers[id] = writeTask;
            _ = writeTask.ContinueWith(
                completedTask => _pendingWriteProducers.TryRemove(id, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Queues one complete node-start write. The returned task completes
        /// after both node and message rows have been persisted by the single database
        /// writer, so callers do not need a second Task.Run queue.
        /// </summary>
        internal static Task<int> InsertNodeExecutionAsync(
            FlowNodeRecord record,
            FlowNodeMessage? message,
            bool preserveTimingOnMessageFailure = false)
        {
            ArgumentNullException.ThrowIfNull(record);
            if (!EnsureInitialized())
                return Task.FromResult(-1);

            FlowNodeRecord startRecord = CloneNodeStartRecord(record);
            FlowNodeMessage? startMessage = message == null
                ? null
                : CloneNodeStartMessage(message);
            var completion = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _writeQueue.Add(db =>
            {
                int recordId = -1;
                try
                {
                    int messageId = 0;
                    WriteNodeExecution(db,
                        () => recordId = InsertTimingRecord(db, startRecord),
                        startMessage == null ? null : () =>
                        {
                            startMessage.NodeRecordId = recordId;
                            int id = InsertStartMessage(db, startMessage);
                            FlowNodeMessagePayloadStorage.SaveSendPayload(db, id, startMessage.SendPayload);
                            messageId = id;
                        },
                        preserveTimingOnMessageFailure);
                    record.Id = recordId;
                    if (message != null)
                    {
                        message.Id = messageId;
                        message.NodeRecordId = messageId > 0 ? recordId : null;
                    }
                }
                catch (Exception ex)
                {
                    TryRollback(db);
                    record.Id = 0;
                    if (message != null)
                    {
                        message.Id = 0;
                        message.NodeRecordId = null;
                    }
                    log.Error("插入流程节点及消息记录失败", ex);
                    recordId = -1;
                }

                completion.TrySetResult(recordId);
            });
            return completion.Task;
        }

        /// <summary>
        /// Queues the final node record and message update as one
        /// ordered database command.
        /// </summary>
        internal static Task UpdateNodeExecutionAsync(
            FlowNodeRecord record,
            FlowNodeMessage? message,
            bool preserveTimingOnMessageFailure = false)
        {
            ArgumentNullException.ThrowIfNull(record);
            if (!EnsureInitialized())
                return Task.CompletedTask;

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _writeQueue.Add(db =>
            {
                try
                {
                    WriteNodeExecution(db,
                        () => UpdateTimingRecord(db, record),
                        message == null ? null : () =>
                        {
                            UpdateMessageMetadata(db, message);
                            FlowNodeMessagePayloadStorage.SaveRecvPayload(db, message.Id, message.RecvPayload);
                        },
                        preserveTimingOnMessageFailure);
                }
                catch (Exception ex)
                {
                    TryRollback(db);
                    log.Error("更新流程节点及消息记录失败", ex);
                }
                completion.TrySetResult(true);
            });
            return completion.Task;
        }

        private static int InsertTimingRecord(SqlSugarClient db, FlowNodeRecord record)
        {
            return db.Ado.GetInt("""
                INSERT INTO FlowNodeRecord (
                    batch_id, serial_number, node_id, node_name, node_type, start_time, end_time, elapsed_ms)
                VALUES (@batchId, @serialNumber, @nodeId, @nodeName, @nodeType, @startTime, @endTime, @elapsedMs);
                SELECT last_insert_rowid();
                """, CreateTimingParameters(record));
        }

        private static void UpdateTimingRecord(SqlSugarClient db, FlowNodeRecord record)
        {
            db.Ado.ExecuteCommand("""
                UPDATE FlowNodeRecord SET
                    batch_id = @batchId, serial_number = @serialNumber, node_id = @nodeId, node_name = @nodeName,
                    node_type = @nodeType, start_time = @startTime, end_time = @endTime, elapsed_ms = @elapsedMs
                WHERE id = @id;
                """, CreateTimingParameters(record, includeId: true));
        }

        private static SugarParameter[] CreateTimingParameters(FlowNodeRecord record, bool includeId = false)
        {
            var parameters = new SugarParameter[includeId ? 9 : 8];
            parameters[0] = new("@batchId", record.BatchId);
            parameters[1] = new("@serialNumber", record.SerialNumber, ParameterType.String);
            parameters[2] = new("@nodeId", record.NodeId, ParameterType.String);
            parameters[3] = new("@nodeName", record.NodeName, ParameterType.String);
            parameters[4] = new("@nodeType", record.NodeType, ParameterType.String);
            parameters[5] = new("@startTime", record.StartTime, ParameterType.DateTime);
            parameters[6] = new("@endTime", record.EndTime, ParameterType.DateTime);
            parameters[7] = new("@elapsedMs", record.ElapsedMs);
            if (includeId)
                parameters[8] = new("@id", record.Id);
            return parameters;
        }

        private static int InsertStartMessage(SqlSugarClient db, FlowNodeMessage message)
        {
            // The queued start snapshot has no receive fields. Keep payload storage in the same transaction.
            return db.Ado.GetInt("""
                INSERT INTO FlowNodeMessage (
                    batch_id, serial_number, node_record_id, node_id, node_name, msg_id, event_name,
                    send_topic, send_time, recv_topic, recv_time, status_code, status_message, state)
                VALUES (
                    @batchId, @serialNumber, @nodeRecordId, @nodeId, @nodeName, @msgId, @eventName,
                    @sendTopic, @sendTime, NULL, NULL, NULL, NULL, @state);
                SELECT last_insert_rowid();
                """,
                new SugarParameter("@batchId", message.BatchId),
                new SugarParameter("@serialNumber", message.SerialNumber, ParameterType.String),
                new SugarParameter("@nodeRecordId", message.NodeRecordId, ParameterType.Int32),
                new SugarParameter("@nodeId", message.NodeId, ParameterType.String),
                new SugarParameter("@nodeName", message.NodeName, ParameterType.String),
                new SugarParameter("@msgId", message.MsgId, ParameterType.String),
                new SugarParameter("@eventName", message.EventName, ParameterType.String),
                new SugarParameter("@sendTopic", message.SendTopic, ParameterType.String),
                new SugarParameter("@sendTime", message.SendTime, ParameterType.DateTime),
                new SugarParameter("@state", (long)message.State));
        }

        private static void UpdateMessageMetadata(SqlSugarClient db, FlowNodeMessage message)
        {
            db.Ado.ExecuteCommand("""
                UPDATE FlowNodeMessage SET
                    batch_id = @batchId, serial_number = @serialNumber, node_record_id = @nodeRecordId,
                    node_id = @nodeId, node_name = @nodeName, msg_id = @msgId, event_name = @eventName,
                    send_topic = @sendTopic, send_time = @sendTime, recv_topic = @recvTopic,
                    recv_time = @recvTime, status_code = @statusCode, status_message = @statusMessage, state = @state
                WHERE id = @id;
                """,
                new SugarParameter("@id", message.Id),
                new SugarParameter("@batchId", message.BatchId),
                new SugarParameter("@serialNumber", message.SerialNumber, ParameterType.String),
                new SugarParameter("@nodeRecordId", message.NodeRecordId, ParameterType.Int32),
                new SugarParameter("@nodeId", message.NodeId, ParameterType.String),
                new SugarParameter("@nodeName", message.NodeName, ParameterType.String),
                new SugarParameter("@msgId", message.MsgId, ParameterType.String),
                new SugarParameter("@eventName", message.EventName, ParameterType.String),
                new SugarParameter("@sendTopic", message.SendTopic, ParameterType.String),
                new SugarParameter("@sendTime", message.SendTime, ParameterType.DateTime),
                new SugarParameter("@recvTopic", message.RecvTopic, ParameterType.String),
                new SugarParameter("@recvTime", message.RecvTime, ParameterType.DateTime),
                new SugarParameter("@statusCode", message.StatusCode, ParameterType.Int32),
                new SugarParameter("@statusMessage", message.StatusMessage, ParameterType.String),
                new SugarParameter("@state", (long)message.State));
        }

        private static void WriteNodeExecution(SqlSugarClient db, Action writeTiming, Action? writeMessage,
            bool preserveTimingOnMessageFailure)
        {
            if (writeMessage == null)
            {
                writeTiming();
                return;
            }

            db.Ado.BeginTran();
            writeTiming();
            try
            {
                writeMessage();
            }
            catch (Exception ex) when (preserveTimingOnMessageFailure)
            {
                // Direct FlowEngine hosts have always retained the timing row when message storage fails.
                db.Ado.RollbackTran();
                log.Error("保存流程节点消息失败，单独保存节点计时", ex);
                writeTiming();
                return;
            }
            db.Ado.CommitTran();
        }

        internal static FlowNodeRecord CloneNodeStartRecord(
            FlowNodeRecord source)
        {
            return new FlowNodeRecord
            {
                BatchId = source.BatchId,
                SerialNumber = source.SerialNumber,
                NodeId = source.NodeId,
                NodeName = source.NodeName,
                NodeType = source.NodeType,
                StartTime = source.StartTime,
                EndTime = null,
                ElapsedMs = 0,
            };
        }

        internal static FlowNodeMessage CloneNodeStartMessage(
            FlowNodeMessage source)
        {
            return new FlowNodeMessage
            {
                BatchId = source.BatchId,
                SerialNumber = source.SerialNumber,
                NodeId = source.NodeId,
                NodeName = source.NodeName,
                MsgId = source.MsgId,
                EventName = source.EventName,
                SendTopic = source.SendTopic,
                SendPayload = source.SendPayload,
                SendTime = source.SendTime,
                State = FlowMessageState.Sent,
            };
        }

        public static int Insert(FlowNodeRecord item)
        {
            if (item == null || !EnsureInitialized())
                return -1;

            try
            {
                // The identity is returned synchronously, but the write still
                // goes through the single database writer.
                var tcs = new TaskCompletionSource<int>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _writeQueue.Add(db =>
                {
                    try
                    {
                        int id = InsertTimingRecord(db, item);
                        item.Id = id;
                        tcs.TrySetResult(id);
                    }
                    catch (Exception ex)
                    {
                        log.Error("插入FlowNodeRecord失败", ex);
                        tcs.TrySetResult(-1);
                    }
                });
                if (!TryGetSynchronousWriteResult(
                    tcs.Task,
                    _synchronousWriteTimeout,
                    out int result))
                {
                    log.Error("插入FlowNodeRecord等待写入队列超时。");
                    return -1;
                }
                return result;
            }
            catch (Exception ex)
            {
                log.Error("插入FlowNodeRecord失败", ex);
                return -1;
            }
        }

        public static void Update(FlowNodeRecord item)
        {
            if (item == null || !EnsureInitialized())
                return;
            _writeQueue.Add(db =>
            {
                try
                {
                    UpdateTimingRecord(db, item);
                }
                catch (Exception ex)
                {
                    log.Error("更新FlowNodeRecord失败", ex);
                }
            });
        }

        private static SqlSugarClient CreateReadDb()
        {
            FlowNodeRecordConfig config = ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>();
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={config.SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }

        public static List<FlowNodeRecord> GetByBatchId(int batchId)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeRecord>().Where(x => x.BatchId == batchId).OrderBy(x => x.StartTime).ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询FlowNodeRecord失败", ex);
                return new List<FlowNodeRecord>();
            }
        }

        public static List<FlowNodeRecord> GetByRun(int batchId, string? serialNumber)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                var query = db.Queryable<FlowNodeRecord>().Where(item => item.BatchId == batchId);
                if (!string.IsNullOrWhiteSpace(serialNumber))
                    query = query.Where(item => item.SerialNumber == serialNumber);
                return query.OrderBy(item => item.StartTime).ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询流程执行节点记录失败", ex);
                return new List<FlowNodeRecord>();
            }
        }

        public static FlowNodeRecord? GetLatestRecord()
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeRecord>()
                    .OrderByDescending(item => item.StartTime)
                    .First();
            }
            catch (Exception ex)
            {
                log.Error("查询最近流程节点记录失败", ex);
                return null;
            }
        }

        public static List<FlowNodeRecord> GetByBatchIds(List<int> batchIds)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeRecord>().Where(x => batchIds.Contains(x.BatchId)).OrderBy(x => x.StartTime).ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询FlowNodeRecord失败", ex);
                return new List<FlowNodeRecord>();
            }
        }

        internal static List<FlowRunNavigationSummary> ReadRunNavigation(Func<SqlSugarClient, List<FlowRunNavigationSummary>> read)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return read(db);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Error("查询流程历史导航失败", ex);
                return [];
            }
        }

        internal static List<FlowNodeRecord> GetNodeRecordsForRuns(string nodeId, int[] batchIds, string[] serialNumbers)
        {
            EnsureInitialized();
            using var db = CreateReadDb();
            return SelectNodeRecordsForRuns(db.Queryable<FlowNodeRecord>(), nodeId, batchIds, serialNumbers).ToList();
        }

        internal static ISugarQueryable<FlowNodeRecord> SelectNodeRecordsForRuns(
            ISugarQueryable<FlowNodeRecord> query, string nodeId, int[] batchIds, string[] serialNumbers)
        {
            if (serialNumbers.Length != 0 || batchIds.Length == 0
                || query.Context.CurrentConnectionConfig.DbType != DbType.Sqlite)
            {
                return query.Where(item => item.NodeId == nodeId && (batchIds.Contains(item.BatchId) || serialNumbers.Contains(item.SerialNumber)))
                    .OrderByDescending(item => item.StartTime);
            }

            string batches = JsonSerializer.Serialize(batchIds);
            if (!ShouldSeekComparisonByBatch(query.Context, nodeId, batches, batchIds.Length))
                return query.Where(item => item.NodeId == nodeId && batchIds.Contains(item.BatchId));

            // Use the same projection for live and archived schemas. The comparison orders the merged records.
            return query.Context.SqlQueryable<FlowNodeRecord>($"""
                SELECT record.* FROM (SELECT DISTINCT value FROM json_each(@comparisonBatches)) requested
                CROSS JOIN ({query.ToSql().Key}) record ON record.batch_id = requested.value
                WHERE record.node_id = @comparisonNode
                """).AddParameters(new { comparisonBatches = batches, comparisonNode = nodeId });
        }

        private static bool ShouldSeekComparisonByBatch(ISqlSugarClient db, string nodeId, string batches, int batchCount)
        {
            // Compare bounded index-only reads. Keep the node lookup for short histories or broad batches.
            return db.Ado.GetInt("""
                SELECT CASE WHEN (
                    SELECT COUNT(*) FROM sqlite_schema WHERE type = 'index' AND tbl_name = 'FlowNodeRecord'
                        AND name IN ('idx_flow_node_record_node_time', 'idx_flow_node_record_batch_time')
                ) = 2 THEN EXISTS (
                    WITH node_count AS MATERIALIZED (
                        SELECT COUNT(*) AS n FROM (
                            SELECT 1 FROM FlowNodeRecord WHERE node_id = @nodeId LIMIT 32768)
                    )
                    SELECT 1 FROM node_count
                    WHERE CASE WHEN n > @minimumNodeRows THEN NOT EXISTS (
                        SELECT 1 FROM FlowNodeRecord
                        WHERE batch_id IN (SELECT value FROM json_each(@batches))
                        LIMIT 1 OFFSET (SELECT n / 2 FROM node_count)
                    ) ELSE 0 END
                ) ELSE 0 END
                """, new { nodeId, batches, minimumNodeRows = (long)batchCount * 2 }) != 0;
        }

        public static List<FlowNodeRecord> GetBySerialNumbers(IEnumerable<string> serialNumbers)
        {
            string[] selectedSerialNumbers = serialNumbers?
                .Where(serialNumber => !string.IsNullOrWhiteSpace(serialNumber))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            if (selectedSerialNumbers.Length == 0)
                return new List<FlowNodeRecord>();

            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeRecord>()
                    .Where(item => selectedSerialNumbers.Contains(item.SerialNumber))
                    .OrderBy(item => item.StartTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询多次流程执行节点记录失败", ex);
                return new List<FlowNodeRecord>();
            }
        }

        public static Dictionary<string, long> GetLastElapsedByNodeIds(IEnumerable<string> nodeIds)
        {
            EnsureInitialized();
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            try
            {
                string[] ids = nodeIds?
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray() ?? Array.Empty<string>();
                if (ids.Length == 0)
                    return result;

                using var db = CreateReadDb();
                ReadLatestCompletedDurations(db, ids, result);
            }
            catch (Exception ex)
            {
                log.Error("查询节点上次执行耗时失败", ex);
            }
            return result;
        }

        internal static void ReadLatestCompletedDurations(SqlSugarClient db, IEnumerable<string> ids, Dictionary<string, long> result)
        {
            // End-time ties follow the previous node/start-time index's order.
            foreach (string[] batch in ids.Chunk(256))
            {
                SugarParameter[] parameters = batch.Select((id, index) => new SugarParameter($"@node{index}", id)).ToArray();
                string values = string.Join(",", parameters.Select(parameter => $"({parameter.ParameterName})"));
                using var reader = db.Ado.GetDataReader($"""
                    WITH requested(node_id) AS (VALUES {values})
                    SELECT requested.node_id, (
                        SELECT history.elapsed_ms
                        FROM FlowNodeRecord AS history
                        WHERE history.node_id = requested.node_id
                            AND history.end_time IS NOT NULL AND history.elapsed_ms > 0
                        ORDER BY history.end_time DESC, history.start_time DESC, history.id ASC
                        LIMIT 1
                    )
                    FROM requested
                    """, parameters);
                while (reader.Read())
                    if (!reader.IsDBNull(1))
                        result[reader.GetString(0)] = reader.GetInt64(1);
            }
        }

        public static long GetLastCompletedFlowElapsed(
            FlowIdentity identity)
        {
            if (identity.IsEmpty)
                return 0;
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return ReadLastCompletedFlowElapsed(db, identity);
            }
            catch (Exception ex)
            {
                log.Error("查询流程上次执行耗时失败", ex);
                return 0;
            }
        }

        internal static long ReadLastCompletedFlowElapsed(SqlSugarClient db, FlowIdentity identity)
        {
            if (identity.IsEmpty)
                return 0;

            if (db.CurrentConnectionConfig.DbType == DbType.Sqlite
                && identity.FlowKey is string flowKey && identity.TemplateId > 0)
            {
                FlowRunRecord? latest = ReadLatestCompletedFlowCandidate(db, flowKey, identity.TemplateId);
                if (latest == null)
                    return 0;
                // The left join returns an empty entity when the original ordering is needed.
                if (latest.Id != 0)
                    return latest.ElapsedMs;
            }

            var query = db.Queryable<FlowRunRecord>()
                .Where(item => item.Status == FlowStatus.Completed && item.ElapsedMs > 0);
            return ApplyFlowIdentityFilter(query, identity)
                .OrderByDescending(item => item.CompletedTime)
                .First()?.ElapsedMs ?? 0;
        }

        private static FlowRunRecord? ReadLatestCompletedFlowCandidate(SqlSugarClient db, string flowKey, int templateId)
        {
            // Each branch can seek using its own completion index. Two rows are enough to detect ties,
            // which the original query continues to resolve. An older legacy run cannot beat the keyed leader.
            return db.SqlQueryable<FlowRunRecord>("""
                WITH keyed AS (
                    SELECT id, completed_time FROM FlowRunRecord
                    WHERE flow_key = @flowKey AND status = @completed AND elapsed_ms > 0
                    ORDER BY completed_time DESC LIMIT 2
                ), candidates AS MATERIALIZED (
                    SELECT id, completed_time FROM keyed
                    UNION ALL
                    SELECT id, completed_time FROM (
                        SELECT id, completed_time FROM FlowRunRecord
                        WHERE template_id = @templateId AND (flow_key IS NULL OR flow_key = '')
                            AND status = @completed AND elapsed_ms > 0
                            AND completed_time >= COALESCE(
                                (SELECT MAX(completed_time) FROM keyed),
                                (SELECT MIN(completed_time) FROM FlowRunRecord WHERE template_id = @templateId))
                        ORDER BY completed_time DESC LIMIT 2
                    )
                ), leaders AS (
                    SELECT MIN(id) AS id, COUNT(*) AS frequency FROM candidates
                    WHERE completed_time = (SELECT MAX(completed_time) FROM candidates)
                )
                SELECT run.* FROM leaders LEFT JOIN FlowRunRecord AS run ON leaders.frequency = 1 AND run.id = leaders.id
                WHERE leaders.frequency > 0
                    OR EXISTS (SELECT 1 FROM keyed WHERE completed_time IS NULL)
                    OR EXISTS (
                        SELECT 1 FROM FlowRunRecord
                        WHERE template_id = @templateId AND (flow_key IS NULL OR flow_key = '')
                            AND status = @completed AND elapsed_ms > 0 AND completed_time IS NULL
                    )
                """).AddParameters(new { flowKey, templateId, completed = (int)FlowStatus.Completed }).First();
        }

        internal static (long? FlowElapsedMs, Dictionary<string, long> NodeDurations)? ReadProgressHistory(
            FlowIdentity? identity, string[] nodeIds)
        {
            if (!EnsureInitialized())
                return null;

            using var db = CreateReadDb();
            long? elapsed = identity is FlowIdentity flow ? ReadLastCompletedFlowElapsed(db, flow) : null;
            var durations = new Dictionary<string, long>(StringComparer.Ordinal);
            ReadLatestCompletedDurations(db, nodeIds, durations);
            return (elapsed, durations);
        }

        internal static FlowRunRecord? GetFlowRun(
            int batchId,
            string? serialNumber)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return SelectFlowRun(db.Queryable<FlowRunRecord>(), batchId, serialNumber).First();
            }
            catch (Exception ex)
            {
                log.Error("查询流程运行 journal 失败", ex);
                return null;
            }
        }

        internal static ISugarQueryable<FlowRunRecord> SelectFlowRun(ISugarQueryable<FlowRunRecord> runs, int batchId, string? serialNumber)
        {
            var query = runs.Where(item => item.BatchId == batchId);
            // Analysis displays missing and whitespace-only serials as one group.
            if (string.IsNullOrWhiteSpace(serialNumber))
                query = query.Where($"coalesce({FlowRunIdentitySql.TrimmedKey("serial_number")}, '') = ''");
            else
                query = query.Where("serial_number COLLATE BINARY = @runSerialNumber", new { runSerialNumber = serialNumber });

            return query.OrderByDescending(item => item.StartedTimeUtc).OrderByDescending(item => item.Id).Take(1);
        }

        internal static List<FlowRunRecord> GetLegacyFlowRuns(string serialNumber)
        {
            if (!EnsureInitialized())
                return [];
            try
            {
                using var db = CreateReadDb();
                return SelectLegacyFlowRuns(db.Queryable<FlowRunRecord>(), serialNumber).ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询旧流程运行记录失败", ex);
                return [];
            }
        }

        internal static ISugarQueryable<FlowRunRecord> SelectLegacyFlowRuns(ISugarQueryable<FlowRunRecord> runs, string serialNumber)
            => runs.Where(item => item.BatchId == null)
                .Where("serial_number COLLATE BINARY = @runSerialNumber", new { runSerialNumber = serialNumber })
                .Take(2);

        internal static List<FlowRunRecord> GetFlowRuns(
            FlowIdentity identity,
            int limit = 500)
        {
            if (identity.IsEmpty || limit <= 0)
                return new List<FlowRunRecord>();

            if (!EnsureInitialized())
                return new List<FlowRunRecord>();

            try
            {
                using var db = CreateReadDb();
                return ApplyFlowIdentityFilter(
                        db.Queryable<FlowRunRecord>(),
                        identity)
                    .OrderByDescending(item => item.CompletedTime)
                    .Take(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询当前流程执行记录失败", ex);
                return new List<FlowRunRecord>();
            }
        }

        internal static List<FlowExecutionEvent> GetExecutionEvents(
            int runRecordId)
        {
            if (runRecordId <= 0 || !EnsureInitialized())
                return new List<FlowExecutionEvent>();

            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowExecutionEvent>()
                    .Where(item => item.RunRecordId == runRecordId)
                    .OrderBy(item => item.SequenceNo)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询流程阶段事件失败", ex);
                return new List<FlowExecutionEvent>();
            }
        }

        public static int RecordFlowRun(
            int templateId,
            string? flowName,
            string? serialNumber,
            FlowStatus status,
            long elapsedMs)
        {
            Task<int> completion = QueueFlowRunAsync(templateId, flowName, serialNumber, status, elapsedMs);
            if (TryGetSynchronousWriteResult(completion, _synchronousWriteTimeout, out int result))
                return result;

            log.Error("插入流程运行记录等待写入队列超时。");
            return -1;
        }

        /// <summary>Queues the terminal diagnostic record and returns its identity after the database write.</summary>
        public static Task<int> QueueFlowRunAsync(
            int templateId,
            string? flowName,
            string? serialNumber,
            FlowStatus status,
            long elapsedMs)
        {
            if (!EnsureInitialized())
                return Task.FromResult(-1);

            try
            {
                var record = new FlowRunRecord
                {
                    TemplateId = templateId,
                    FlowName = flowName,
                    SerialNumber = serialNumber,
                    Status = status,
                    ElapsedMs = Math.Max(0, elapsedMs),
                    CompletedTime = DateTime.Now,
                };
                var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                _writeQueue.Add(db =>
                {
                    try
                    {
                        int id = db.Insertable(record).ExecuteReturnIdentity();
                        record.Id = id;
                        completion.TrySetResult(id);
                    }
                    catch (Exception ex)
                    {
                        log.Error("插入流程运行记录失败", ex);
                        completion.TrySetResult(-1);
                    }
                });
                return completion.Task;
            }
            catch (Exception ex)
            {
                log.Error("插入流程运行记录失败", ex);
                return Task.FromResult(-1);
            }
        }

        public static List<FlowRunRecord> GetSameFlowRuns(
            string? serialNumber,
            int limit = 500)
        {
            if (string.IsNullOrWhiteSpace(serialNumber) || limit <= 0)
                return new List<FlowRunRecord>();

            EnsureInitialized();
            try
            {
                FlowRunRecord? currentRun;
                using (var db = CreateReadDb())
                {
                    currentRun = db.Queryable<FlowRunRecord>()
                        .Where(item => item.SerialNumber == serialNumber)
                        .OrderByDescending(item => item.CompletedTime)
                        .First();
                }
                if (currentRun == null)
                    return new List<FlowRunRecord>();

                var identity = new FlowIdentity(
                    currentRun.TemplateId,
                    currentRun.FlowKey,
                    currentRun.FlowName);
                return GetFlowRuns(identity, limit);
            }
            catch (Exception ex)
            {
                log.Error("查询相同流程历史执行记录失败", ex);
                return new List<FlowRunRecord>();
            }
        }

        internal static ISugarQueryable<FlowRunRecord>
            ApplyFlowIdentityFilter(
                ISugarQueryable<FlowRunRecord> query,
                FlowIdentity identity)
        {
            if (identity.FlowKey is string flowKey)
            {
                if (identity.TemplateId > 0)
                {
                    int templateId = identity.TemplateId;
                    return query.Where(item =>
                        item.FlowKey == flowKey
                        || ((item.FlowKey == null
                                || item.FlowKey == string.Empty)
                            && item.TemplateId == templateId));
                }

                return query.Where(item => item.FlowKey == flowKey);
            }

            if (identity.TemplateId > 0)
            {
                int templateId = identity.TemplateId;
                return query.Where(item =>
                    item.TemplateId == templateId);
            }

            string? flowName = identity.FlowName;
            return query.Where(item =>
                item.TemplateId <= 0
                && (item.FlowKey == null
                    || item.FlowKey == string.Empty)
                && item.FlowName == flowName);
        }

        public static bool UpdateFlowRun(
            int runRecordId,
            FlowStatus status,
            long elapsedMs)
        {
            if (runRecordId <= 0)
                return false;

            if (!EnsureInitialized())
                return false;

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _writeQueue.Add(db =>
            {
                try
                {
                    DateTime completedTime = DateTime.Now;
                    int affected = db.Updateable<FlowRunRecord>()
                        .SetColumns(item => new FlowRunRecord
                        {
                            Status = status,
                            ElapsedMs = Math.Max(0, elapsedMs),
                            CompletedTime = completedTime,
                            CompletedTimeUtc = completedTime.ToUniversalTime()
                        })
                        .Where(item => item.Id == runRecordId)
                        .ExecuteCommand();
                    completion.TrySetResult(affected > 0);
                }
                catch (Exception ex)
                {
                    log.Error("更新流程运行记录失败", ex);
                    completion.TrySetResult(false);
                }
            });
            if (!TryGetSynchronousWriteResult(
                completion.Task,
                _synchronousWriteTimeout,
                out bool result))
            {
                log.Error("更新流程运行记录等待写入队列超时。");
                return false;
            }
            return result;
        }

        public static FlowNodeRecord? GetLastByNodeId(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return null;

            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeRecord>()
                    .Where(item => item.NodeId == nodeId)
                    .OrderByDescending(item => item.StartTime)
                    .First();
            }
            catch (Exception ex)
            {
                log.Error("查询节点最近执行记录失败", ex);
                return null;
            }
        }

        public static List<FlowNodeRecord> GetByNodeId(string nodeId, int limit = 50)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return new List<FlowNodeRecord>();

            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeRecord>()
                    .Where(item => item.NodeId == nodeId)
                    .OrderByDescending(item => item.StartTime)
                    .Take(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询流程节点历史执行记录失败", ex);
                return new List<FlowNodeRecord>();
            }
        }

        public static List<int> GetDistinctBatchIds(int limit = 100)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeRecord>().GroupBy(x => x.BatchId).OrderByDescending(x => x.BatchId).Select(x => x.BatchId).Take(limit).ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询BatchId列表失败", ex);
                return new List<int>();
            }
        }

        // ===== FlowNodeMessage methods =====

        public static int InsertMessage(FlowNodeMessage item)
        {
            if (item == null || !EnsureInitialized())
                return -1;

            var tcs = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _writeQueue.Add(db =>
            {
                try
                {
                    db.Ado.BeginTran();
                    int id = db.Insertable(item).ExecuteReturnIdentity();
                    FlowNodeMessagePayloadStorage.SaveSendPayload(
                        db,
                        id,
                        item.SendPayload);
                    db.Ado.CommitTran();
                    item.Id = id;
                    tcs.TrySetResult(id);
                }
                catch (Exception ex)
                {
                    TryRollback(db);
                    log.Error("插入FlowNodeMessage失败", ex);
                    tcs.TrySetResult(-1);
                }
            });
            if (!TryGetSynchronousWriteResult(
                tcs.Task,
                _synchronousWriteTimeout,
                out int result))
            {
                log.Error("插入FlowNodeMessage等待写入队列超时。");
                return -1;
            }
            return result;
        }

        public static void UpdateMessage(FlowNodeMessage item)
        {
            if (item == null || !EnsureInitialized())
                return;
            _writeQueue.Add(db =>
            {
                try
                {
                    db.Ado.BeginTran();
                    db.Updateable(item).ExecuteCommand();
                    FlowNodeMessagePayloadStorage.SaveRecvPayload(
                        db,
                        item.Id,
                        item.RecvPayload);
                    db.Ado.CommitTran();
                }
                catch (Exception ex)
                {
                    TryRollback(db);
                    log.Error("更新FlowNodeMessage失败", ex);
                }
            });
        }

        public static List<FlowNodeMessage> GetMessagesByBatchIds(List<int> batchIds)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeMessage>().Where(x => batchIds.Contains(x.BatchId)).OrderBy(x => x.SendTime).ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询FlowNodeMessage失败", ex);
                return new List<FlowNodeMessage>();
            }
        }

        public static List<FlowNodeMessage> GetMessagesByRun(int batchId, string? serialNumber)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                var query = db.Queryable<FlowNodeMessage>().Where(item => item.BatchId == batchId);
                if (!string.IsNullOrWhiteSpace(serialNumber))
                    query = query.Where(item => item.SerialNumber == serialNumber);
                return query.OrderBy(item => item.SendTime).ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询流程执行消息记录失败", ex);
                return new List<FlowNodeMessage>();
            }
        }

        public static List<FlowNodeMessage> GetMessagesByNodeId(int batchId, string nodeId)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                // Equal send times follow the existing batch/time index's reverse scan.
                return db.Queryable<FlowNodeMessage>()
                    .Where(x => x.BatchId == batchId && x.NodeId == nodeId)
                    .OrderBy(x => x.SendTime)
                    .OrderByDescending(x => x.Id)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询FlowNodeMessage失败", ex);
                return new List<FlowNodeMessage>();
            }
        }

        public static List<FlowNodeMessage> GetMessagesByNodeId(string nodeId, int limit = 500)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return new List<FlowNodeMessage>();

            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeMessage>()
                    .Where(item => item.NodeId == nodeId)
                    .OrderByDescending(item => item.SendTime)
                    .Take(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询节点历史消息失败", ex);
                return new List<FlowNodeMessage>();
            }
        }

        internal static List<FlowNodeMessage> GetHistoryMessagesByNodeId(
            string nodeId,
            IEnumerable<int> batchIds)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return new List<FlowNodeMessage>();

            int[] selectedBatchIds = batchIds?
                .Distinct()
                .ToArray() ?? Array.Empty<int>();
            if (selectedBatchIds.Length == 0)
                return new List<FlowNodeMessage>();

            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeMessage>()
                    .Where(item =>
                        item.NodeId == nodeId
                        && selectedBatchIds.Contains(item.BatchId))
                    .OrderByDescending(item => item.SendTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询节点历史执行对应消息失败", ex);
                return new List<FlowNodeMessage>();
            }
        }

        public static List<FlowNodeMessage> GetAllMessages(int limit = 500)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                return db.Queryable<FlowNodeMessage>().OrderByDescending(x => x.SendTime).Take(limit).ToList();
            }
            catch (Exception ex)
            {
                log.Error("查询FlowNodeMessage失败", ex);
                return new List<FlowNodeMessage>();
            }
        }

        internal static FlowNodeMessagePayloads GetMessagePayloads(int messageId)
        {
            if (messageId <= 0 || !EnsureInitialized())
                return default;

            try
            {
                using var db = CreateReadDb();
                return FlowNodeMessagePayloadStorage.LoadPayloads(db, messageId);
            }
            catch (Exception ex)
            {
                log.Error($"读取流程消息 {messageId} 的压缩载荷失败", ex);
                throw new InvalidOperationException(
                    $"流程消息 {messageId} 的 Payload 读取或解压失败。",
                    ex);
            }
        }

        internal static FlowAnalysisDeleteResult DeleteAnalysisForNodeId(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return default;

            return DeleteAnalysisForNodeIds(new[] { nodeId });
        }

        internal static FlowAnalysisDeleteResult DeleteAnalysisForNodeIds(IEnumerable<string> nodeIds)
        {
            string[] selectedNodeIds = nodeIds?
                .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            if (selectedNodeIds.Length == 0)
                return default;

            return ExecuteDelete(db =>
            {
                int messageCount = db.Deleteable<FlowNodeMessage>()
                    .Where(item => selectedNodeIds.Contains(item.NodeId))
                    .ExecuteCommand();
                int recordCount = db.Deleteable<FlowNodeRecord>()
                    .Where(item => selectedNodeIds.Contains(item.NodeId))
                    .ExecuteCommand();
                return new FlowAnalysisDeleteResult(recordCount, messageCount);
            });
        }

        internal static FlowAnalysisDeleteResult DeleteAllAnalysis()
        {
            return ExecuteDelete(db =>
            {
                int messageCount = db.Deleteable<FlowNodeMessage>().ExecuteCommand();
                int recordCount = db.Deleteable<FlowNodeRecord>().ExecuteCommand();
                return new FlowAnalysisDeleteResult(recordCount, messageCount);
            });
        }

        private static FlowAnalysisDeleteResult ExecuteDelete(
            Func<SqlSugarClient, FlowAnalysisDeleteResult> deleteAction)
        {
            ArgumentNullException.ThrowIfNull(deleteAction);
            EnsureInitialized();
            if (!_initialized)
                throw new InvalidOperationException("流程分析数据库尚未初始化。");
            if (!FlushPendingWrites(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("等待流程分析记录写入完成超时，请稍后重试。");

            var completion =
                new TaskCompletionSource<FlowAnalysisDeleteResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            _writeQueue.Add(db =>
            {
                try
                {
                    db.Ado.BeginTran();
                    FlowAnalysisDeleteResult result = deleteAction(db);
                    db.Ado.CommitTran();
                    completion.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    try
                    {
                        db.Ado.RollbackTran();
                    }
                    catch (Exception rollbackException)
                    {
                        log.Warn("回滚流程分析记录清理事务失败", rollbackException);
                    }

                    log.Error("清理流程分析记录失败", ex);
                    completion.TrySetException(ex);
                }
            });
            return completion.Task.GetAwaiter().GetResult();
        }

        public static void DeleteAllMessages()
        {
            if (!EnsureInitialized())
                return;
            _writeQueue.Add(db =>
            {
                try
                {
                    db.Deleteable<FlowNodeMessage>().ExecuteCommand();
                }
                catch (Exception ex)
                {
                    log.Error("删除FlowNodeMessage失败", ex);
                }
            });
        }

        private static void TryRollback(SqlSugarClient db)
        {
            try
            {
                db.Ado.RollbackTran();
            }
            catch (Exception ex)
            {
                log.Warn("回滚流程诊断数据库事务失败", ex);
            }
        }
    }
}
