using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.IO;

namespace ColorVision.Engine.Messages
{
    public static class MsgRecordDataBaseHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MsgRecordDataBaseHelper));
        private static readonly object InitLocker = new();
        private static volatile bool _isInitialized;

        public static event EventHandler<MsgRecord> Inserted;

        private static SqlSugarClient CreateDb(string sqliteDbPath)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={sqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }

        public static void EnsureDatabaseInitialized(MsgRecordManagerConfig config = null)
        {
            if (_isInitialized) return;

            lock (InitLocker)
            {
                if (_isInitialized) return;

                config ??= ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>();

                string directoryPath = Path.GetDirectoryName(config.SqliteDbPath);
                if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using var db = CreateDb(config.SqliteDbPath);
                db.CodeFirst.InitTables<MsgRecord>();
                _isInitialized = true;
            }
        }

        public static void Insert(MsgRecord item)
        {
            try
            {
                if (item == null) return;

                MsgRecordManagerConfig msgRecordManagerConfig = ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>();
                EnsureDatabaseInitialized(msgRecordManagerConfig);

                using var _db = CreateDb(msgRecordManagerConfig.SqliteDbPath);
                int id = _db.Insertable(item).ExecuteReturnIdentity();
                item.Id = id;
                item.MsgRecordStateChanged += (s, e) =>
                {
                    using var db = CreateDb(msgRecordManagerConfig.SqliteDbPath);
                    db.Updateable(item).ExecuteCommand();
                };
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return;
            }
            Inserted?.Invoke(null, item);
        }           
    }
}
