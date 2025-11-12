#pragma warning disable CS8603,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ColorVision.Engine.Archive.Dao
{

    [DisplayName("ArchiveConfiguration"),SugarTable("t_scgd_sys_config_archived")]
    public class ConfigArchivedModel : ViewModelBase, IEntity
    {
        [SugarColumn(ColumnName ="id"), Browsable(false)]
        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;
        [SugarColumn(ColumnName ="path"), DisplayName("DataDirectory"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string Path { get => Regex.Replace(_Path, @"(?<!\\)\\(?!\\)", @"\\"); set { _Path = value; OnPropertyChanged(); } }
        private string _Path;
        [SugarColumn(ColumnName ="cron_expression"), DisplayName("Cron表达式"), PropertyEditorType(typeof(CronExpressionPropertiesEditor))]
        public string CronExpression { get => _CronExpression; set { _CronExpression = value; OnPropertyChanged(); } }
        private string _CronExpression;
        [SugarColumn(ColumnName ="data_save_days"), DisplayName("DataRetentionDays"),]
        public int DataSaveDays { get => _DataSaveDays; set { _DataSaveDays = value; OnPropertyChanged(); } }
        private int _DataSaveDays;

        [SugarColumn(ColumnName ="data_save_hours"), DisplayName("DataRetentionHours"),]
        public int DataSaveHours { get => _DataSaveHours; set { _DataSaveHours = value; OnPropertyChanged(); } }
        private int _DataSaveHours;

        [SugarColumn(ColumnName ="excluding_images"), DisplayName("ClearDatabaseOnly")]
        public bool Excludingimages { get => _Excludingimages; set { _Excludingimages = value; OnPropertyChanged(); } }
        private bool _Excludingimages;

        [SugarColumn(ColumnName ="del_local_file"), DisplayName("DeleteLocalFiles"),]
        public bool DellocalFile { get => _DellocalFile; set { _DellocalFile = value; OnPropertyChanged(); } }
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
            return this.GetByParam(new System.Collections.Generic.Dictionary<string, object>() { { "cfg_type", 10 } });
        }


        public MySqlConfig GetArchMySqlConfig()
        {
            MySqlConfig mySqlConfig = JsonConvert.DeserializeObject<MySqlConfig>(GetArchDB().CfgValue);
            return mySqlConfig;
        }


    }


    [DisplayName("DataBaseConfig"),SugarTable("t_scgd_sys_globle_cfg")]
    public class GlobleCfgdModel : ViewModelBase, IEntity
    {
        [SugarColumn(ColumnName ="id"), Browsable(false)]
        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;

        [SugarColumn(ColumnName ="code"), DisplayName("code")]
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code;


        [SugarColumn(ColumnName ="name"), DisplayName("Name")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;


        [SugarColumn(ColumnName ="cfg_type"), Browsable(false)]
        public int CfgType { get => _CfgType; set { _CfgType = value; OnPropertyChanged(); } }
        private int _CfgType;

        [SugarColumn(ColumnName ="cfg_value"), DisplayName(nameof(CfgValue)), PropertyEditorType(typeof(TextJsonPropertiesEditor))]
        public string CfgValue { get => _CfgValue; set { _CfgValue = value; OnPropertyChanged(); } }
        private string _CfgValue;

        [SugarColumn(ColumnName ="is_deleted")]
        public bool IsDeleted { get => _IsDeleted; set { _IsDeleted = value ; OnPropertyChanged(); } }
        private bool _IsDeleted;

        [SugarColumn(ColumnName ="is_enabled")]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled;

        [SugarColumn(ColumnName ="remark")]
        public string Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string _Remark;

        [SugarColumn(ColumnName ="tenant_id")]
        public int? TenantId { get; set; }

    }


}
