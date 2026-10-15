using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.BuildPOIAA
{

    public class TAAFindPointsParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TAAFindPointsParam));


        public TAAFindPointsParam() : base()
        {
        }

        public TAAFindPointsParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateBuildPOIAA : ITemplateJson<TAAFindPointsParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TAAFindPointsParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TAAFindPointsParam>>();

        public TemplateBuildPOIAA()
        {
            Title = ColorVision.Engine.Properties.Resources.FindAaRegionTemplateManagement;
            Code = "BuildPOI";
            Name = "AAPointLayout";
            TemplateDicId = 41;
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
