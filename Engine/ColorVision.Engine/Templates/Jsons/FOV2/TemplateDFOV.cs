using ColorVision.Database;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.FOV2
{

    public class TJDFOVParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJDFOVParam));

        public FovJson Json
        {
            get
            {
                try
                {
                    FovJson kBJson = JsonConvert.DeserializeObject<FovJson>(JsonValue);
                    if (kBJson == null)
                    {
                        kBJson = new FovJson();
                        JsonValue = JsonConvert.SerializeObject(kBJson);
                        return kBJson;
                    }
                    return kBJson;
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    FovJson kBJson = new FovJson();
                    JsonValue = JsonConvert.SerializeObject(kBJson);
                    return kBJson;
                }
            }
            set
            {
                JsonValue = JsonConvert.SerializeObject(value);
                NotifyPropertyChanged();
            }
        }



        public TJDFOVParam() : base()
        {
        }

        public TJDFOVParam(TemplateJsonModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateDFOV : ITemplateJson<TJDFOVParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJDFOVParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJDFOVParam>>();

        public TemplateDFOV()
        {
            Title = "FOV2.0模板管理";
            Code = "FOV";
            Name = "FOV2.0";
            TemplateDicId = 39;
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
        public string Description { get; set; } = "{\r\n  \"debugCfg\": {\t\t//debug相关配置\r\n    \"Debug\": false,\r\n    \"debugPath\": \"Result\\\\\",\r\n    \"debugImgResize\": 2\r\n  },\r\n  \"ExactCorner\": {\t//精定位\r\n    \"qualityLevel\": 0.04,\t\t\t//一般取0.04或0.06\r\n    \"cutWidth\": 200,\t\t\t\t//精定位裁剪的框\r\n    \"edge\": 10,\t\t\t\t\t//10-20\r\n    \"active\": true\t\t\t\t\t//是否启用\r\n  },\r\n  \"pattern\": 0,\t\t\t\t\t//图案类型  暂不生效\t\t\r\n  \"threshold\": 20000,\t\t\t\t//二值化阈值\r\n  \"DarkRatio\": 0.5,\t\t\t\t\t//黑区对比\r\n  \"FovDist\": 9576.0,\t\t\t\t//计算系数\r\n  \"cameraDegrees\": 137.0,\t\t\t//计算系数\r\n  \"HorizontalFov\": true,\t\t\t\t//是否计算水平FOV\r\n  \"VerticalFov\": true,\t\t\t\t//是否计算垂直FOV\r\n  \"AnglesFov\": true,\t\t\t\t//是否计算对角FOV\r\n  \"aaLocationWay\": 1\t\t\t\t//角点定位方式，0代表精定位方式，1代表拟合法\r\n}\r\n";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlFOV();

    }




}
