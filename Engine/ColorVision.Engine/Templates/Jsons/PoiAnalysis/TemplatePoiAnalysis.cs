using ColorVision.Engine.MySql;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.PoiAnalysis
{

    public class TJPoiAnalysisParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJPoiAnalysisParam));



        public TJPoiAnalysisParam() : base()
        {
        }

        public TJPoiAnalysisParam(TemplateJsonModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplatePoiAnalysis : ITemplateJson<TJPoiAnalysisParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJPoiAnalysisParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJPoiAnalysisParam>>();

        public TemplatePoiAnalysis()
        {
            Title = "POI分析模板管理";
            Code = "PoiAnalysis";
            Name = "PoiAnalysis";
            TemplateDicId = 44;
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
            EditTemplateJson = EditTemplateJson ?? new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlPoiAnalysis();

    }




}
