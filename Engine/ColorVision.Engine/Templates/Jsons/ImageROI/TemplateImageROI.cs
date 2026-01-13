using ColorVision.Database;
using ColorVision.Engine.Templates.Menus;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.ImageROI
{
    public class ExportTemplateImageROI : MenuITemplateAlgorithmBase
    {
        public override string Header => "图像裁剪模板管理";
        public override int Order => 0;
        public override ITemplate Template => new TemplateImageROI();

    }

    public class TemplateImageROI : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateImageROI()
        {
            Title = "图像裁剪模板管理";
            Code = "Image.ROI";
            TemplateDicId = 52;
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
        public string Description { get; set; } = "//图像裁剪配置\r\nstruct ImageROIParam\r\n{\r\n    int RHO;                 //RHO参数      \r\n    struct center {          //中心点\r\n        int x;               //x坐标\r\n        int y;               //y坐标\r\n    };\r\n    float pixelToAngle;      //像素到角度转换系数\r\n};";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);

        public override IMysqlCommand? GetMysqlCommand() => new MysqlImageROI();

    }
}
