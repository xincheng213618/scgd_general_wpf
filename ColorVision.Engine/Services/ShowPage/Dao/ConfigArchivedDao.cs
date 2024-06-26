#pragma warning disable CS8601
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using System.Data;

namespace ColorVision.Engine.Services.ShowPage.Dao
{
    public class ConfigArchivedModel : ViewModelBase, IPKModel
    {
        [Column("id")]
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;
        [Column("path")]
        public string Path { get => _Path; set { _Path = value; NotifyPropertyChanged(); } }
        private string _Path;
        [Column("cron_expression")]
        public string CronExpression { get => _CronExpression; set { _CronExpression = value; NotifyPropertyChanged(); } }
        private string _CronExpression;
        [Column("data_save_days")]
        public int DataSaveDays { get => _DataSaveDays; set { _DataSaveDays = value; NotifyPropertyChanged(); } }
        private int _DataSaveDays;
    }

    public class ConfigArchivedDao : BaseTableDao<ConfigArchivedModel>
    {
        public static ConfigArchivedDao Instance { get; set; } = new ConfigArchivedDao();

        public ConfigArchivedDao() : base("t_scgd_sys_config_archived")
        {
        }
    }
}
