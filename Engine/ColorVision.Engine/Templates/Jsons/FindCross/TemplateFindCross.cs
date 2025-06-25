using ColorVision.Engine.MySql;
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

        public TJFindCrossParam(TemplateJsonModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateFindCross : ITemplateJson<TJFindCrossParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJFindCrossParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJFindCrossParam>>();

        public TemplateFindCross()
        {
            Title = "MTFV2模板管理";
            Code = "MTF";
            Name = "MTF_V2";
            TemplateDicId = 48;
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
        public override IMysqlCommand? GetMysqlCommand() => new MysqlFindCross();

    }




}
