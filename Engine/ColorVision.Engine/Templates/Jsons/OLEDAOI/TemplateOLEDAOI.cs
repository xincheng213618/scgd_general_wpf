using LocalizedText = global::ColorVision.Engine.EngineLocalization;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI
{

    public class TJOLEDAOIParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJOLEDAOIParam));



        public TJOLEDAOIParam() : base()
        {
        }

        public TJOLEDAOIParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateOLEDAOI : ITemplateJson<TJOLEDAOIParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJOLEDAOIParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJOLEDAOIParam>>();

        public TemplateOLEDAOI()
        {
            Title = LocalizedText.Get("OLED AOI模板管理");
            Code = "OLED.AOI";
            Name = "OLED_AOI";
            TemplateDicId = 28;
            TemplateParams = Params;
            IsUserControl = true;
        }


        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateJson { get; set; }

        public override UserControl GetUserControl()
        {
            EditTemplateJson = new EditTemplateJson();
            return EditTemplateJson;
        }
        public override UserControl CreateUserControl() => new EditTemplateJson();
    }



}
