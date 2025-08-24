using ColorVision.Database;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.MTF2
{

    public class TJMTFParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJMTFParam));



        public TJMTFParam() : base()
        {
        }

        public TJMTFParam(TemplateJsonModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateMTF2 : ITemplateJson<TJMTFParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJMTFParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJMTFParam>>();

        public TemplateMTF2()
        {
            Title = "MTFV2模板管理";
            Code = "MTF";
            Name = "MTF_V2";
            TemplateDicId = 48;
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
        public string Description { get; set; } = "{\r\n  \"debugCfg\": {\t\t// Debug配置\r\n    \"Debug\": false,\r\n    \"debugPath\": \"Result\\\\\",\r\n    \"debugImgResize\": 2\r\n  },\r\n  \"mathMaskRect\": {\t\t// Mask配置\r\n    \"enable\": false,\r\n    \"x\": 0,\r\n    \"y\": 0,\r\n    \"w\": 0,\r\n    \"h\": 0\r\n  },\r\n  \"nV1\": {\t\t// 水平垂直四部，九个条状图案参数，当pattern为5生效\r\n    \"AAminSize\": 20,\t\t\t// 最小尺寸\r\n    \"offsetX\": 0,\t\t\t\t// 质心偏移X\r\n    \"offsetY\": 0,\t\t\t\t// 质心偏移Y\r\n    \"distanceToRect\": 20,\t\t// 四个小矩形到质心像素距离\r\n    \"rectWidth\": 15,\t\t\t// 小矩形w\r\n    \"rectHeight\": 10,\t\t\t//小矩形h\r\n    \"firstIsHor\": true,\t\t\t// 左上角第一个是否为水平\r\n    \"lineWidth\": 4\t\t\t\t// 暂不生效\r\n  },\r\n  \"threshold\": 10000,\t\t\t// 二值化阈值\r\n  \"dRatio\": 0.1,\t\t\t\t// 传统算法比例\r\n  \"pattern\": 2,\t\t\t\t// 图案类型，5代表水平垂直四条\r\n  \"CalcMethod\": 1\t\t\t\t\t// MTF计算方式：0代表比率，1代表最亮/最暗\r\n}\r\n";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlMTF2();

    }




}
