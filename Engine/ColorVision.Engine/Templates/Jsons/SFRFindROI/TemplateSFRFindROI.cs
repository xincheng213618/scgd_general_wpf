using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.SFRFindROI
{
    public class TemplateSFRFindROI : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateSFRFindROI()
        {
            Title = "SFR寻边模板管理";
            Code = "ARVR.SFR.FindROI";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateJson { get; set; } = new EditTemplateJson();

        public override UserControl GetUserControl()
        {
            return EditTemplateJson;
        }
        public override UserControl CreateUserControl() => new EditTemplateJson();
        public override IMysqlCommand? GetMysqlCommand() => new MysqlSFRFindROI();

    }




}
