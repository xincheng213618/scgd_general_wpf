using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForBlackScreen
{
    public class TJFPForBlackScreenParam : TemplateJsonParam
    {
        public TJFPForBlackScreenParam() : base()
        {
        }

        public TJFPForBlackScreenParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {
        }
    }

    public class TemplateFPForBlackScreen : ITemplateJson<TJFPForBlackScreenParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJFPForBlackScreenParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJFPForBlackScreenParam>>();

        public TemplateFPForBlackScreen()
        {
            Title = "黑画面检测模板管理";
            Code = "OLED.AOI.FPForBlackScreen";
            Name = "FPForBlackScreen";
            TemplateDicId = 57;
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
            EditTemplateJson = new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "{\r\n  \"TimeStamp\": \"_20251231_145129\",\r\n  \"GradeLevel\": \"NG\"\r\n}";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlFPForBlackScreen();
    }
}
