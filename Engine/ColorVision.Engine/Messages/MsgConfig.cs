using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Messages
{
    public class MsgConfig : ViewModelBase,IConfig
    {
        public static MsgConfig Instance => ConfigService.Instance.GetRequiredService<MsgConfig>();

        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";
        public static string SqliteDbPath { get; set; } = DirectoryPath + "MsgRecords.db";
        private readonly SqlSugarClient _db;

        public MsgConfig()
        {
            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            // 确保表存在
            _db.CodeFirst.InitTables<MsgRecord>();
        }

        [JsonIgnore]
        public ObservableCollection<MsgRecord> MsgRecords { get; set; } = new ObservableCollection<MsgRecord>();


    }
}
