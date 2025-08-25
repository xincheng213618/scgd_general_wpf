using ColorVision.Database;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.BuildPOIAA
{

    public class TAAFindPointsParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TAAFindPointsParam));


        public TAAFindPointsParam() : base()
        {
        }

        public TAAFindPointsParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateBuildPOIAA : ITemplateJson<TAAFindPointsParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TAAFindPointsParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TAAFindPointsParam>>();

        public TemplateBuildPOIAA()
        {
            Title = "寻找AA区模板管理";
            Code = "BuildPOI";
            Name = "AA布点";
            TemplateDicId = 41;
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
        public string Description { get; set; } = "{\r\n    \"pattern\": 1,\t\t//图案类型，1代表矩形发光区，0代表圆\r\n    \"threshold\": 6000,\t\t//二值化阈值\r\n    \"brightRate\": 0.3,\t\t//发光区占画幅比例下限\r\n    \"aaLocationWay\": 1,\t//角点定位方式，0代表精定位方式，1代表拟合法\r\n    \"RectArea\": {\r\n        \"nummber_x\": 5,\t//布点横向数量\r\n        \"nummber_y\": 5,\t//布点纵向数量\r\n        \"offset_left\": 0.0,\t\t//左边缩进\r\n        \"offset_right\": 0.0,\t\t//右边缩进\r\n        \"offset_top\": 0.0,\t\t//上方缩进\r\n        \"offset_bottom\": 0.0\t//底部缩进\r\n    },\r\n    \"CircleArea\": {\r\n        \"turnNum\": 1,\t\t\t//布点环数\r\n        \"distance\": 400,\t\t//距中心点像素\r\n        \"singleAngle\": 36,\t\t//间隔角度\r\n        \"roateAngle\": 0\t\t//起始偏移角度\r\n    },\r\n     \"erodeAndDiate\": {\r\n        \"erodeKernel\": 3,\t//腐蚀的核\r\n        \"erodeTime\": 0,\t//腐蚀的次数\r\n        \"dilateKernel\": 3,\t//膨胀的核\r\n        \"dilateTime\": 0,\t//膨胀的次数\r\n        \"erodeFirst\": true\t\t//true代表先腐蚀后膨胀\r\n    },\r\n    \"debugCfg\": {\r\n        \"Debug\": false,\t\t//debug相关配置\r\n        \"debugPath\": \"Result\\\\\",\r\n        \"debugImgResize\": 2\r\n    },\r\n    \"MaskRect\": {\r\n        \"enable\": false,\t\t//mask相关配置\r\n        \"x\": 0,\r\n        \"y\": 0,\r\n        \"w\": 0,\r\n        \"h\": 0\r\n    },\r\n    \"ExactCorner\": {//精定位\r\n        \"qualityLevel\": 0.04,\t//一般取0.04或0.06\r\n        \"cutWidth\": 200,\t\t//精定位裁剪的框\r\n        \"edge\": 5,\t\t\t//10-20\r\n        \"active\": true\t//是否启用\r\n    }\r\n}\r\n";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlAAFindPoints();

    }




}
