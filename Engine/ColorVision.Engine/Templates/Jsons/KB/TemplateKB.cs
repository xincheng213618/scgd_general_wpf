#pragma warning disable CS8603
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.KB
{

    public class TemplateJsonKBParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TemplateJsonKBParam));
        public RelayCommand EditTemplatePoiCommand { get; set; }
        public RelayCommand EditCommand { get; set; }

        public KBJson  KBJson { get => JsonConvert.DeserializeObject<KBJson>(JsonValue); set { JsonValue = JsonConvert.SerializeObject(value); NotifyPropertyChanged(); } }

        public TemplateJsonKBParam() : base()
        {
        }

        public TemplateJsonKBParam(TemplateJsonModel templateJsonModel):base(templateJsonModel) 
        {
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
            IsSideHide = true;
        }
        public EditPoiParam1 EditWindow { get; set; }
        public override void PreviewMouseDoubleClick(int index)
        {
            EditWindow = new EditPoiParam1(Params[index].Value) { Owner = Application.Current.GetActiveWindow() };
            EditWindow.ShowDialog();
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
