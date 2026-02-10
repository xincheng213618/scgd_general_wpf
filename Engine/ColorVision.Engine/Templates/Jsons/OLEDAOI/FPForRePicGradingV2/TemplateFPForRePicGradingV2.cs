using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForRePicGradingV2
{
    public class TJFPForRePicGradingV2Param : TemplateJsonParam
    {
        public TJFPForRePicGradingV2Param() : base()
        {
        }

        public TJFPForRePicGradingV2Param(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {
        }
    }

    public class TemplateFPForRePicGradingV2 : ITemplateJson<TJFPForRePicGradingV2Param>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJFPForRePicGradingV2Param>> Params { get; set; } = new ObservableCollection<TemplateModel<TJFPForRePicGradingV2Param>>();

        public TemplateFPForRePicGradingV2()
        {
            Title = "缺陷检测V2模板管理";
            Code = "OLED.AOI.FPForRePicGradingV2";
            Name = "FPForRePicGradingV2";
            TemplateDicId = 56;
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
        public string Description { get; set; } = "{\r\n  \"width\": 12,\r\n  \"height\": 12,\r\n  \"radius\": 6,\r\n  \"dark_lvs\": [1, 2, 3, 4, 6],\r\n  \"lowRatio\": 10,\r\n  \"topRatio\": 10,\r\n  \"scan_type\": 1,\r\n  \"bright_lvs\": [1, 2, 3, 4, 6],\r\n  \"edge_area_h\": 120,\r\n  \"edge_area_w\": 160,\r\n  \"edge_area_x\": 239,\r\n  \"edge_area_y\": 179,\r\n  \"faultPixelRatio\": 4,\r\n  \"badPixelNumThreshold\": 102,\r\n  \"brightPixelNumThreshold\": 70,\r\n  \"darkPixelLvRatioThreshold\": 0.4,\r\n  \"brightPixelLvRatioThreshold\": 1.2,\r\n  \"connectedBadPixelNumThreshold\": 10\r\n}";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlFPForRePicGradingV2();
    }
}
