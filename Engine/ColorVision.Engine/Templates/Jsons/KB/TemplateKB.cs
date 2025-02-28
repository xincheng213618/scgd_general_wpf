using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class TemplateJsonKBParamCoveretConfig:IConfig
    {
        public static TemplateJsonKBParamCoveretConfig Instance => ConfigService.Instance.GetRequiredService<TemplateJsonKBParamCoveretConfig>();
        public bool DoKey { get; set; } = true;
        public bool DoHalo { get; set; } = true;

    }

    public class TemplateJsonKBParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TemplateJsonKBParam));
        public RelayCommand EditTemplatePoiCommand { get; set; }
        public RelayCommand EditCommand { get; set; }

        public KBJson  KBJson { get => JsonConvert.DeserializeObject<KBJson>(JsonValue); set { JsonValue = JsonConvert.SerializeObject(value); NotifyPropertyChanged(); } }

        public TemplateJsonKBParam() : base()
        {
            EditCommand = new RelayCommand(a => Edit());
        }

        public TemplateJsonKBParam(TemplateJsonModel templateJsonModel):base(templateJsonModel) 
        {
            EditCommand = new RelayCommand(a => Edit());
        }

        public void Edit()
        {
            KBJson kBJson = KBJson;
            var EditWindow = new EditPoiParam1(kBJson) { Owner = Application.Current.GetActiveWindow() };
            EditWindow.ShowDialog();
            JsonValue = JsonConvert.SerializeObject(kBJson);
        }
    }



    public class TemplateKB : ITemplateJson<TemplateJsonKBParam>,IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonKBParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonKBParam>>();

        public TemplateKB()
        {
            Title = "KB模板管理";
            Code = "KB";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditKBTemplateJson EditTemplateJson { get; set; } = new EditKBTemplateJson();

        public override UserControl GetUserControl()
        {
            return EditTemplateJson;
        }
        public override UserControl CreateUserControl() => new EditKBTemplateJson();
        public override IMysqlCommand? GetMysqlCommand() => new MysqKB();


    }




}
