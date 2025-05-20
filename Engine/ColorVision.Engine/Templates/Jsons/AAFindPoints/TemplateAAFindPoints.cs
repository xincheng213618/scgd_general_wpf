using ColorVision.Engine.MySql;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.AAFindPoints
{

    public class TAAFindPointsParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TAAFindPointsParam));


        public TAAFindPointsParam() : base()
        {
        }

        public TAAFindPointsParam(TemplateJsonModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateAAFindPoints : ITemplateJson<TAAFindPointsParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TAAFindPointsParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TAAFindPointsParam>>();

        public TemplateAAFindPoints()
        {
            Title = "寻找AA区模板管理";
            Code = "BuildPOI";
            Name = "寻找AA区";
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
            EditTemplateJson = EditTemplateJson ?? new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlAAFindPoints();

    }




}
