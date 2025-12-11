using ColorVision.Database;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI
{

    public class TJOLEDAOIParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJOLEDAOIParam));



        public TJOLEDAOIParam() : base()
        {
        }

        public TJOLEDAOIParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateOLEDAOI : ITemplateJson<TJOLEDAOIParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJOLEDAOIParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJOLEDAOIParam>>();

        public TemplateOLEDAOI()
        {
            Title = "OLED AOI模板管理";
            Code = "OLED.AOI";
            Name = "OLED_AOI";
            TemplateDicId = 28;
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
        public string Description { get; set; } = "{\r\n  \"rebuiltImgPixelDefects\": {\r\n    \"dark_lvs\": [1, 2, 3, 4, 6],\r\n    \"bright_lvs\": [1, 2, 3, 4, 6],\r\n    \"edge_area_h\": 380,\r\n    \"edge_area_w\": 520,\r\n    \"edge_area_x\": 80,\r\n    \"edge_area_y\": 50,\r\n    \"gradingCfgs\": [\r\n      {\r\n        \"grade_name\": \"GOOD\",\r\n        \"dark_max_nums\": [2, 1, 0, 0, 0],\r\n        \"bright_max_nums\": [2, 1, 0, 0, 0]\r\n      },\r\n      {\r\n        \"grade_name\": \"WELL\",\r\n        \"dark_max_nums\": [2000, 100, 50, 10, 1],\r\n        \"bright_max_nums\": [2000, 100, 50, 10, 1]\r\n      },\r\n      {\r\n        \"grade_name\": \"SOSO\",\r\n        \"dark_max_nums\": [3000, 100, 50, 10, 1],\r\n        \"bright_max_nums\": [3000, 100, 50, 10, 1]\r\n      }\r\n    ],\r\n    \"enable_grading\": true,\r\n    \"darkThreshold_edge_area\": 0.15,\r\n    \"brightThreshold_edge_area\": 1,\r\n    \"darkPixelLvRatioThreshold\": 0.4,\r\n    \"brightPixelLvRatioThreshold\": 1,\r\n    \"averageMaskLvRatioThresholdDark\": 0.4,\r\n    \"averageMaskLvRatioThresholdBright\": 2\r\n  }\r\n}\r\n";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlOLEDAOI();

    }



}
