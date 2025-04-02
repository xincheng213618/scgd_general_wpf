using ColorVision.Engine.MySql;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{
    public class TemplateBlackMura : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateBlackMura()
        {
            Title = "BlackMura模板管理";
            Code = "BlackMura.Caculate";
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
        public override IMysqlCommand? GetMysqlCommand() => new MysqlBlackMura();

    }




}
