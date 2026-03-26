using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Flow
{
    public static class FlowNodeRecordDataBaseHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowNodeRecordDataBaseHelper));
        private static bool _initialized;

        private static SqlSugarClient CreateDb()
        {
            FlowNodeRecordConfig config = ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>();
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={config.SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            try
            {
                using var db = CreateDb();
                db.CodeFirst.InitTables<FlowNodeRecord>();
                _initialized = true;
            }
            catch (Exception ex)
            {
                log.Error("初始化FlowNodeRecord表失败", ex);
            }
        }

        public static int Insert(FlowNodeRecord item)
        {
            EnsureInitialized();
            try
            {
                if (item == null) return -1;
                using var db = CreateDb();
                int id = db.Insertable(item).ExecuteReturnIdentity();
                item.Id = id;
                return id;
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
            try
            {
                if (item == null) return;
                using var db = CreateDb();
                db.Updateable(item).ExecuteCommand();
            }
            catch (Exception ex)
            {
                log.Error("更新FlowNodeRecord失败", ex);
            }
        }

        public static List<FlowNodeRecord> GetByBatchId(int batchId)
        {
            EnsureInitialized();
            try
            {
                using var db = CreateDb();
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
                using var db = CreateDb();
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
                using var db = CreateDb();
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
