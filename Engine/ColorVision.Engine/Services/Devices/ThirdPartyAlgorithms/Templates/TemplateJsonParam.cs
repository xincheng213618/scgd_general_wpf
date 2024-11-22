
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using ColorVision.UI.Utilities;
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{
    public interface IEditTemplateJson
    {
        public RelayCommand ResetCommand { get; set; }
        public string JsonValue { get; set; }
    }


    public class TemplateJsonParam : ParamModBase, IEditTemplateJson
    {
        public RelayCommand ResetCommand { get; set; }

        public ModThirdPartyAlgorithmsModel ModThirdPartyAlgorithmsModel { get; set; }

        public TemplateJsonParam() 
        {
            ResetCommand = new RelayCommand((a)=> ResetValue());
        }

        public TemplateJsonParam(ModThirdPartyAlgorithmsModel modThirdPartyAlgorithmsModel)
        {
            ModThirdPartyAlgorithmsModel = modThirdPartyAlgorithmsModel;
            ResetCommand = new RelayCommand((a) => ResetValue());
        }

        public void ResetValue()
        {
            if (ModThirdPartyAlgorithmsModel.PId is int pid && ThirdPartyAlgorithmsDao.Instance.GetById(pid)?.DefaultCfg is string str)
            {
                JsonValue = str;
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
            }
        }

    }
}
