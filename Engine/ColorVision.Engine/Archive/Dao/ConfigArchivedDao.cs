#pragma warning disable CS8603,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ColorVision.Engine.Archive.Dao
{

    [DisplayName("归档配置"),Table("t_scgd_sys_config_archived")]
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

        [Column("data_save_hours"), DisplayName("数据保存小时数"),]
        public int DataSaveHours { get => _DataSaveHours; set { _DataSaveHours = value; NotifyPropertyChanged(); } }
        private int _DataSaveHours;

        [Column("excluding_images"), DisplayName("仅清除数据库")]
        public bool Excludingimages { get => _Excludingimages; set { _Excludingimages = value; NotifyPropertyChanged(); } }
        private bool _Excludingimages;

        [Column("del_local_file"), DisplayName("删除本地文件"),]
        public bool DellocalFile { get => _DellocalFile; set { _DellocalFile = value; NotifyPropertyChanged(); } }
        private bool _DellocalFile;

    }

    public class ConfigArchivedDao : BaseTableDao<ConfigArchivedModel>
    {
        public static ConfigArchivedDao Instance { get; set; } = new ConfigArchivedDao();

    }
    public class GlobleCfgdDao : BaseTableDao<GlobleCfgdModel>
    {
        public static GlobleCfgdDao Instance { get; set; } = new GlobleCfgdDao();

        public GlobleCfgdModel? GetArchDB()
        {
            return GetByParam(new System.Collections.Generic.Dictionary<string, object>() { { "cfg_type", 10 } });
        }


        public MySqlConfig GetArchMySqlConfig()
        {
            MySqlConfig mySqlConfig = JsonConvert.DeserializeObject<MySqlConfig>(GetArchDB().CfgValue);
            return mySqlConfig;
        }


    }


    [DisplayName("数据库配置"),Table("t_scgd_sys_globle_cfg")]
    public class GlobleCfgdModel : ViewModelBase, IPKModel
    {
        [Column("id"), Browsable(false)]
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        [Column("code"), DisplayName("code")]
        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code;


        [Column("name"), DisplayName("名称")]
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;


        [Column("cfg_type"), Browsable(false)]
        public int CfgType { get => _CfgType; set { _CfgType = value; NotifyPropertyChanged(); } }
        private int _CfgType;

        [Column("cfg_value"), DisplayName(nameof(CfgValue)),PropertyEditorType(PropertyEditorType.TextJson)]
        public string CfgValue { get => _CfgValue; set { _CfgValue = value; NotifyPropertyChanged(); } }
        private string _CfgValue;

        [Column("is_deleted")]
        public bool IsDeleted { get => _IsDeleted; set { _IsDeleted = value ; NotifyPropertyChanged(); } }
        private bool _IsDeleted;

        [Column("is_enabled")]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; NotifyPropertyChanged(); } }
        private bool _IsEnabled;

        [Column("remark")]
        public string Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string _Remark;

        [Column("tenant_id")]
        public int? TenantId { get; set; }

    }


}
