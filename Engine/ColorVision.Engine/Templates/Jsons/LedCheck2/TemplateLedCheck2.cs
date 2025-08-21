using ColorVision.Engine.MySql;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
    public class TemplateLedCheck2 : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateLedCheck2()
        {
            Title = "亚像素级灯珠检测模板管理";
            Code = "FindLED";
            TemplateDicId = 18;
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
            EditTemplateJson =  new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);


        public override IMysqlCommand? GetMysqlCommand() => new MysqlLedCheck2();

    }




}
