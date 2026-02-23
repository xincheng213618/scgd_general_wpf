using ColorVision.UI;
using log4net;
using SqlSugar;
using System;

namespace ColorVision.Engine.Messages
{
    public static class MsgRecordDataBaseHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MsgRecordDataBaseHelper));

        public static void Insert(MsgRecord item)
        {
            MsgRecordManagerConfig msgRecordManagerConfig = ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>();
            using var _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={msgRecordManagerConfig.SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            try
            {
                if (item == null) return;
                int id = _db.Insertable(item).ExecuteReturnIdentity();
                item.Id = id;
                item.MsgRecordStateChanged += (s, e) =>
                {
                    using var db = new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={msgRecordManagerConfig.SqliteDbPath}",
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true
                    });
                    db.Updateable(item).ExecuteCommand();
                };
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }           
    }
}
