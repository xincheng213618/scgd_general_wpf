using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.SFR2
{

    public class TJSFRParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJSFRParam));



        public TJSFRParam() : base()
        {
        }

        public TJSFRParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateSFR2 : ITemplateJson<TJSFRParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJSFRParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJSFRParam>>();

        public TemplateSFR2()
        {
            Title = ColorVision.Engine.Properties.Resources.SfrV2TemplateManagement;
            Code = "SFR";
            Name = "SFR_V2";
            TemplateDicId = 49;
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
