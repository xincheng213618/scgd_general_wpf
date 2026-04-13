using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.UI.Utilities;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Templates.Jsons
{
    public class TemplateJsonParam : ParamBase, IEditTemplateJson
    {
        [Browsable(false)]
        [JsonIgnore]
        public ModMasterModel TemplateJsonModel { get; set; }
        [Browsable(false)]
        [JsonIgnore]
        public RelayCommand ResetCommand { get; set; }

        [Browsable(false)]
        [JsonIgnore]
        public RelayCommand CheckCommand { get; set; }

        public string Description { get;  }

        public TemplateJsonParam()
        {
            TemplateJsonModel = new ModMasterModel();
            ResetCommand = new RelayCommand((a) => ResetValue());
            CheckCommand = new RelayCommand(a => Check());
            Description = "Json配置";
        }

        public TemplateJsonParam(ModMasterModel templateJsonModel)
        {
            TemplateJsonModel = templateJsonModel;
            ResetCommand = new RelayCommand((a) => ResetValue());
            CheckCommand = new RelayCommand(a => Check());
        }

        public void Check()
        {
            JsonValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetValue()
        {
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            SysDictionaryModModel? DicTemplateJsonModel = Db.Queryable<SysDictionaryModModel>().Where(x => x.Id == TemplateJsonModel.Pid).First(); 
            if (DicTemplateJsonModel !=null && DicTemplateJsonModel.JsonVal is string str)
            {
                JsonValue = str;
                JsonValueChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show("无法重置，请检查数据库相关配置");
            }
        }

        public override int Id { get => TemplateJsonModel.Id; set { TemplateJsonModel.Id = value; OnPropertyChanged(); } }
        public override string Name { get => TemplateJsonModel.Name ?? string.Empty; set { TemplateJsonModel.Name = value; OnPropertyChanged(); } }

        public event EventHandler JsonValueChanged;

        public string JsonValue
        {
            get => JsonHelper.BeautifyJson(TemplateJsonModel.JsonVal); set
            {
                if (JsonHelper.IsValidJson(value))
                {
                    TemplateJsonModel.JsonVal = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
