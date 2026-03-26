using ColorVision.UI;
using log4net;
using SqlSugar;
using System;

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
    }
}
