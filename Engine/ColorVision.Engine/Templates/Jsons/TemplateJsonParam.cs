using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI.Utilities;
using Newtonsoft.Json;
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
        public TemplateJsonModel TemplateJsonModel { get; set; }
        [Browsable(false)]
        [JsonIgnore]
        public RelayCommand ResetCommand { get; set; }

        [Browsable(false)]
        [JsonIgnore]
        public RelayCommand CheckCommand { get; set; }

        [Browsable(false)]
        public DicTemplateJsonModel? DicTemplateJsonModel => DicTemplateJsonDao.Instance.GetById(TemplateJsonModel.DicId);

        public RelayCommand OpenEditToolCommand { get; set; }

        public string Description { get;  }

        public TemplateJsonParam()
        {
            TemplateJsonModel = new TemplateJsonModel();
            ResetCommand = new RelayCommand((a) => ResetValue());
            OpenEditToolCommand = new RelayCommand(a => OpenEditTool());
            CheckCommand = new RelayCommand(a => Check());
            Description = "Json配置";
        }

        public TemplateJsonParam(TemplateJsonModel templateJsonModel)
        {
            TemplateJsonModel = templateJsonModel;
            ResetCommand = new RelayCommand((a) => ResetValue());
            OpenEditToolCommand = new RelayCommand(a => OpenEditTool());
            CheckCommand = new RelayCommand(a => Check());
        }

        public void Check()
        {
            JsonValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void OpenEditTool()
        {
            Common.NativeMethods.Clipboard.SetText(JsonValue);
            // 获取程序运行路径
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            // 相对文件路径
            string relativePath = @"Assets/Tool/EditJson/Editjson.html";

            // 合并路径并获取绝对路径
            string absolutePath = Path.Combine(basePath, relativePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = absolutePath,
                UseShellExecute = true // 使用默认应用程序打开
            });
        }

        public void ResetValue()
        {
            if (DicTemplateJsonModel is DicTemplateJsonModel dicnmodel && DicTemplateJsonModel.JsonVal is string str)
            {
                JsonValue = str;
                JsonValueChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show("无法重置，请检查数据库相关配置");
            }
        }

        public override int Id { get => TemplateJsonModel.Id; set { TemplateJsonModel.Id = value; NotifyPropertyChanged(); } }
        public override string Name { get => TemplateJsonModel.Name ?? string.Empty; set { TemplateJsonModel.Name = value; NotifyPropertyChanged(); } }

        public event EventHandler JsonValueChanged;

        public string JsonValue
        {
            get => JsonHelper.BeautifyJson(TemplateJsonModel.JsonVal); set
            {
                if (JsonHelper.IsValidJson(value))
                {
                    TemplateJsonModel.JsonVal = value;
                    NotifyPropertyChanged();
                }
            }
        }
    }
}
