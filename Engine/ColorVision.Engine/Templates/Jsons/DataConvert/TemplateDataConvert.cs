using ColorVision.Engine.MySql;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.DataConvert
{
    public class TemplateDataConvert : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateDataConvert()
        {
            Title = "数据转换";
            Code = "Math.DataConvert";
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
        public override IMysqlCommand? GetMysqlCommand() => new MysqlDataConvert();

    }




}
