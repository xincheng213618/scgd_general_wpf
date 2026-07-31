using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ColorVision.Database;

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
                    FlowDiagnosticsSchemaMigrator.EnsureSchema(sharedDb);

                    DatabaseBrowserProviderRegistry.Register(new SqliteDatabaseBrowserProvider(
                        "sqlite.flownoderecords",
                        "流程节点记录",
                        () => ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath,
                        dbPath => new SqlSugarClient(new ConnectionConfig
                        {
                            ConnectionString = $"Data Source={dbPath}",
                            DbType = DbType.Sqlite,
                            IsAutoCloseConnection = true,
                            InitKeyType = InitKeyType.Attribute
                        })));

                    _sharedDb = sharedDb;
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
                    action(sharedDb);
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

        public static int Insert(FlowNodeRecord item)
        {
            if (item == null || !EnsureInitialized())
                return -1;

            try
            {
                // Use a temporary connection for synchronous insert (needs return value)
                // but enqueue for hot path when called from Task.Run
                var tcs = new TaskCompletionSource<int>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _writeQueue.Add(db =>
                {
                    try
                    {
                        int id = db.Insertable(item).ExecuteReturnIdentity();
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
                    db.Updateable(item).ExecuteCommand();
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
                foreach (string nodeId in ids)
                {
                    FlowNodeRecord record = db.Queryable<FlowNodeRecord>()
                        .Where(item => item.NodeId == nodeId && item.EndTime != null && item.ElapsedMs > 0)
                        .OrderByDescending(item => item.EndTime)
                        .First();
                    if (record != null)
                        result[nodeId] = record.ElapsedMs;
                }
            }
            catch (Exception ex)
            {
                log.Error("查询节点上次执行耗时失败", ex);
            }
            return result;
        }

        public static long GetLastCompletedFlowElapsed(int templateId, string? flowName)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                var query = db.Queryable<FlowRunRecord>()
                    .Where(item => item.Status == FlowStatus.Completed && item.ElapsedMs > 0);
                if (templateId > 0)
                    query = query.Where(item => item.TemplateId == templateId);
                else if (!string.IsNullOrWhiteSpace(flowName))
                    query = query.Where(item => item.FlowName == flowName);
                else
                    return 0;

                FlowRunRecord? record = query
                    .OrderByDescending(item => item.CompletedTime)
                    .First();
                return record?.ElapsedMs ?? 0;
            }
            catch (Exception ex)
            {
                log.Error("查询流程上次执行耗时失败", ex);
                return 0;
            }
        }

        internal static FlowRunRecord? GetFlowRun(
            int batchId,
            string? serialNumber)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateReadDb();
                if (!string.IsNullOrWhiteSpace(serialNumber))
                {
                    FlowRunRecord? exact = db.Queryable<FlowRunRecord>()
                        .Where(item => item.BatchId == batchId)
                        .Where(item => item.SerialNumber == serialNumber)
                        .OrderByDescending(item => item.StartedTimeUtc)
                        .First();
                    if (exact != null)
                        return exact;
                }

                return db.Queryable<FlowRunRecord>()
                    .Where(item => item.BatchId == batchId)
                    .OrderByDescending(item => item.StartedTimeUtc)
                    .First();
            }
            catch (Exception ex)
            {
                log.Error("查询流程运行 journal 失败", ex);
                return null;
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
            if (!EnsureInitialized())
                return -1;

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
                if (!TryGetSynchronousWriteResult(
                    completion.Task,
                    _synchronousWriteTimeout,
                    out int result))
                {
                    log.Error("插入流程运行记录等待写入队列超时。");
                    return -1;
                }
                return result;
            }
            catch (Exception ex)
            {
                log.Error("插入流程运行记录失败", ex);
                return -1;
            }
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
                    int id = db.Insertable(item).ExecuteReturnIdentity();
                    item.Id = id;
                    tcs.TrySetResult(id);
                }
                catch (Exception ex)
                {
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
                    db.Updateable(item).ExecuteCommand();
                }
                catch (Exception ex)
                {
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
                return db.Queryable<FlowNodeMessage>().Where(x => x.BatchId == batchId && x.NodeId == nodeId).OrderBy(x => x.SendTime).ToList();
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
    }
}
