using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.FindCross
{

    public class TJFindCrossParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJFindCrossParam));



        public TJFindCrossParam() : base()
        {
        }

        public TJFindCrossParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateFindCross : ITemplateJson<TJFindCrossParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJFindCrossParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJFindCrossParam>>();

        public TemplateFindCross()
        {
            Title = ColorVision.Engine.Properties.Resources.CrossCalculationTemplateManagement;
            Code = "FindCross";
            Name = ColorVision.Engine.Properties.Resources.FindCross;
            TemplateDicId = 45;
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
