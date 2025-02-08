using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI.Menus;
using ColorVision.UI.PropertyEditor;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;

namespace ColorVision.Engine.DataHistory.Dao
{
    public class ConfigArchiveMenu : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override string GuidId => nameof(ConfigArchiveMenu);

        public override string Header => "归档配置";

        public override void Execute()
        {
            string sql = "ALTER TABLE `t_scgd_sys_config_archived`\r\nADD COLUMN `excluding_images` tinyint(1) NOT NULL DEFAULT '0' AFTER `data_save_days`;";
            MySqlControl.GetInstance().ExecuteNonQuery(sql);
            ConfigArchivedModel configArchivedModel = ConfigArchivedDao.Instance.GetById(1);
            if (configArchivedModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到归档配置信息", "ColorVision");
                return;
            }
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(configArchivedModel, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submited += (s, e) => { ConfigArchivedDao.Instance.Save(configArchivedModel); };
            propertyEditorWindow.ShowDialog();
        }
    }

    [DisplayName("归档配置")]
    public class ConfigArchivedModel : ViewModelBase, IPKModel
    {
        [Column("id"), Browsable(false)]
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;
        [Column("path"), DisplayName("数据目录"), PropertyEditorType(PropertyEditorType.TextSelectFolder)]
        public string Path { get => Regex.Replace(_Path, @"(?<!\\)\\(?!\\)", @"\\"); set { _Path = value; NotifyPropertyChanged(); } }
        private string _Path;
        [Column("cron_expression"), DisplayName("Cron表达式"), PropertyEditorType(PropertyEditorType.CronExpression)]
        public string CronExpression { get => _CronExpression; set { _CronExpression = value; NotifyPropertyChanged(); } }
        private string _CronExpression;
        [Column("data_save_days"), DisplayName("数据保存天数"),]
        public int DataSaveDays { get => _DataSaveDays; set { _DataSaveDays = value; NotifyPropertyChanged(); } }
        private int _DataSaveDays;

        [Column("excluding_images"), DisplayName("归档不包含图像"),]
        public bool Excludingimages { get => _Excludingimages; set { _Excludingimages = value; NotifyPropertyChanged(); } }
        private bool _Excludingimages = false;

        
    }

    public class ConfigArchivedDao : BaseTableDao<ConfigArchivedModel>
    {
        public static ConfigArchivedDao Instance { get; set; } = new ConfigArchivedDao();

        public ConfigArchivedDao() : base("t_scgd_sys_config_archived")
        {
        }
    }
}
