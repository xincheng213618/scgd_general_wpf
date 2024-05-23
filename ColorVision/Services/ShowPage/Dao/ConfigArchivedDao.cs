using ColorVision.Common.MVVM;
using ColorVision.MySql.ORM;
using System.Data;

namespace ColorVision.Services.ShowPage.Dao
{
    public class ConfigArchivedModel : ViewModelBase, IPKModel
    {
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string Path { get => _Path; set { _Path = value; NotifyPropertyChanged(); } }
        private string _Path;

        public string CronExpression { get => _CronExpression; set { _CronExpression = value; NotifyPropertyChanged(); } }
        private string _CronExpression;
        public int DataSaveDays { get => _DataSaveDays; set { _DataSaveDays = value; NotifyPropertyChanged(); } }
        private int _DataSaveDays;
    }

    public class ConfigArchivedDao : BaseTableDao<ConfigArchivedModel>
    {
        public static ConfigArchivedDao Instance { get; set; } = new ConfigArchivedDao();

        public ConfigArchivedDao() : base("t_scgd_sys_config_archived", "id")
        {

        }

        public override DataTable CreateColumns(DataTable dataTable)
        {
            dataTable.Columns.Add("path", typeof(string));
            dataTable.Columns.Add("cron_expression", typeof(string));
            dataTable.Columns.Add("data_save_days", typeof(int));
            return dataTable;
        }

        public override ConfigArchivedModel GetModelFromDataRow(DataRow item) => new()
        {
            Id = item.Field<int>("id"),
            Path = item.Field<string>("path"),
            CronExpression = item.Field<string>("cron_expression") ?? string.Empty,
            DataSaveDays = item.Field<int>("data_save_days")
        };

        public override DataRow Model2Row(ConfigArchivedModel item, DataRow row)
        {
            row["id"] = item.Id;
            row["path"] = item.Path;
            row["cron_expression"] = item.CronExpression;
            row["data_save_days"] = item.DataSaveDays;
            return row;
        }

    }
}
