using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI.PropertyEditor;
using System.ComponentModel;

namespace ColorVision.Engine.DataHistory.Dao
{
    public class ConfigArchivedModel : ViewModelBase, IPKModel
    {
        [Column("id"), Browsable(false)]
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;
        [Column("path"), DisplayName("数据目录"), PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public string Path { get => _Path; set { _Path = value; NotifyPropertyChanged(); } }
        private string _Path;
        [Column("cron_expression"), DisplayName("Cron表达式"), PropertyEditorType(PropertyEditorType.CronExpression)]
        public string CronExpression { get => _CronExpression; set { _CronExpression = value; NotifyPropertyChanged(); } }
        private string _CronExpression;
        [Column("data_save_days"), DisplayName("数据保存天数"),]
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
