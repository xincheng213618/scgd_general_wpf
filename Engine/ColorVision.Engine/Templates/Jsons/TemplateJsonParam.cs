using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI.Utilities;
using Newtonsoft.Json;
using System.ComponentModel;
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
        public DicTemplateJsonModel? DicTemplateJsonModel => DicTemplateJsonDao.Instance.GetById(TemplateJsonModel.DicId);

        public TemplateJsonParam()
        {
            TemplateJsonModel = new TemplateJsonModel();
            ResetCommand = new RelayCommand((a) => ResetValue());
        }

        public TemplateJsonParam(TemplateJsonModel templateJsonModel)
        {
            TemplateJsonModel = templateJsonModel;
            ResetCommand = new RelayCommand((a) => ResetValue());
        }

        public void ResetValue()
        {
            if (DicTemplateJsonModel is DicTemplateJsonModel dicnmodel && DicTemplateJsonModel.JsonVal is string str)
            {
                JsonValue = str;
            }
            else
            {
                MessageBox.Show("无法重置，请检查数据库相关配置");
            }
        }

        public override int Id { get => TemplateJsonModel.Id; set { TemplateJsonModel.Id = value; NotifyPropertyChanged(); } }
        public override string Name { get => TemplateJsonModel.Name ?? string.Empty; set { TemplateJsonModel.Name = value; NotifyPropertyChanged(); } }

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
