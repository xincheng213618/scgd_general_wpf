using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.Flow
{
    public static class FlowNodeRecordDataBaseHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowNodeRecordDataBaseHelper));
        private static bool _initialized;
        private static readonly object _initLock = new object();

        // Shared persistent connection for the write queue
        private static SqlSugarClient _sharedDb;
        private static readonly BlockingCollection<Action<SqlSugarClient>> _writeQueue = new BlockingCollection<Action<SqlSugarClient>>();
        private static Thread _writerThread;

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

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;
                try
                {
                    _sharedDb = CreateDb();
                    _sharedDb.CodeFirst.InitTables<FlowNodeRecord>();

                    _writerThread = new Thread(WriteLoop)
                    {
                        IsBackground = true,
                        Name = "FlowNodeRecord-Writer"
                    };
                    _writerThread.Start();

                    _initialized = true;
                }
                catch (Exception ex)
                {
                    log.Error("初始化FlowNodeRecord表失败", ex);
                }
            }
        }

        private static void WriteLoop()
        {
            foreach (var action in _writeQueue.GetConsumingEnumerable())
            {
                try
                {
                    action(_sharedDb);
                }
                catch (Exception ex)
                {
                    log.Error("FlowNodeRecord写入队列执行失败", ex);
                }
            }
        }

        public static int Insert(FlowNodeRecord item)
        {
            EnsureInitialized();
            try
            {
                if (item == null) return -1;
                // Use a temporary connection for synchronous insert (needs return value)
                // but enqueue for hot path when called from Task.Run
                var tcs = new TaskCompletionSource<int>();
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
                return tcs.Task.Result;
            }
            catch (Exception ex)
            {
                log.Error("插入FlowNodeRecord失败", ex);
                return -1;
            }
        }

        public static void Update(FlowNodeRecord item)
        {
            EnsureInitialized();
            if (item == null) return;
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
    }
}
