
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.UI.Utilities;
using System.Diagnostics;
using System;
using System.Windows;
using System.IO;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{


    public class TemplateJsonParam : ParamModBase, IEditTemplateJson
    {
        public RelayCommand ResetCommand { get; set; }
        public RelayCommand OpenEditToolCommand { get; set; }

        public RelayCommand CheckCommand { get; set; }

        public ModThirdPartyAlgorithmsModel ModThirdPartyAlgorithmsModel { get; set; }

        public TemplateJsonParam() 
        {
            ResetCommand = new RelayCommand((a)=> ResetValue());
            OpenEditToolCommand = new RelayCommand(a => OpenEditTool());
            CheckCommand = new RelayCommand(a => Check());
        }

        public TemplateJsonParam(ModThirdPartyAlgorithmsModel modThirdPartyAlgorithmsModel)
        {
            ModThirdPartyAlgorithmsModel = modThirdPartyAlgorithmsModel;
            ResetCommand = new RelayCommand((a) => ResetValue());
            OpenEditToolCommand = new RelayCommand(a=> OpenEditTool());
            CheckCommand = new RelayCommand(a => Check());

        }

        public void Check()
        {
            JsonValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler JsonValueChanged;

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
            if (ModThirdPartyAlgorithmsModel.PId is int pid && ThirdPartyAlgorithmsDao.Instance.GetById(pid)?.DefaultCfg is string str)
            {
                JsonValue = str;
                JsonValueChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show("无法重置，请检查数据库相关配置");
            }
        }

        public override int Id { get => ModThirdPartyAlgorithmsModel.Id; set { ModThirdPartyAlgorithmsModel.Id = value; NotifyPropertyChanged(); } }
        public override string Name { get => ModThirdPartyAlgorithmsModel.Name ?? string.Empty; set { ModThirdPartyAlgorithmsModel.Name = value; NotifyPropertyChanged(); } }

        public string JsonValue
        {
            get => JsonHelper.BeautifyJson(ModThirdPartyAlgorithmsModel.JsonVal); set
            {
                if (JsonHelper.IsValidJson(value))
                {
                    ModThirdPartyAlgorithmsModel.JsonVal = value;
                    NotifyPropertyChanged();
                }
                else
                {
                    JsonValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

    }
}
