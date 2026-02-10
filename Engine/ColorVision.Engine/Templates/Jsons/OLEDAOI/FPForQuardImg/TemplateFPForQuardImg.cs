using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForQuardImg
{
    public class TJFPForQuardImgParam : TemplateJsonParam
    {
        public TJFPForQuardImgParam() : base()
        {
        }

        public TJFPForQuardImgParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {
        }
    }

    public class TemplateFPForQuardImg : ITemplateJson<TJFPForQuardImgParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJFPForQuardImgParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJFPForQuardImgParam>>();

        public TemplateFPForQuardImg()
        {
            Title = "亮点检测模板管理";
            Code = "OLED.AOI.FPForQuardImg";
            Name = "FPForQuardImg";
            TemplateDicId = 55;
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
        public string Description { get; set; } = "{\r\n  \"th\": 0.1,\r\n  \"index\": 1,\r\n  \"num_th\": 10\r\n}";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlFPForQuardImg();
    }
}
