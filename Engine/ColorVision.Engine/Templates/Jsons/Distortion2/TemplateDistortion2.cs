using ColorVision.Engine.MySql;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{

    public class TemplateJsonDistortion2 : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TemplateJsonDistortion2));

        public TemplateJsonDistortion2() : base()
        {
        }

        public TemplateJsonDistortion2(TemplateJsonModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateDistortion2 : ITemplateJson<TemplateJsonDistortion2>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonDistortion2>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonDistortion2>>();

        public TemplateDistortion2()
        {
            Title = "畸变2.0模板管理";
            Code = "distortion";
            Name = "distortion2.0";
            TemplateDicId = 40;
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
            EditTemplateJson = EditTemplateJson ?? new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "{\r\n  \"debugCfg\": {\r\n    \"Debug\": false,\t\t\t//是否debug\r\n    \"debugPath\": \"Result\\\\\",\t\t//debug存储路径\r\n    \"debugImgResize\": 2\t\t//debug图片压缩，2、3、4生效，否则不压缩\r\n  },\r\n  \"MaskRect\": {\r\n    \"enable\": false,\t\t\t//为true的话，则在图像左上角（x,y）为起点，w、h的范围计算\r\n    \"x\": 0,\r\n    \"y\": 0,\r\n    \"w\": 0,\r\n    \"h\": 0\r\n  },\r\n  \"CommonParams\": {\r\n    \"pattern\": 2,\t\t\t\t\t//1，矩形发光区；2点阵发光区；3棋盘格角点提取\r\n    \"brightNumX\": 3,\t\t\t\t//横向数量\r\n    \"brightNumY\": 3\t\t\t\t\t//纵向数量\r\n  },\r\n  \"Point9Params\": {\r\n    \"threshold\": 25000,\t\t\t\t//二值化阈值\r\n    \"outRectSizeMin\": 40,\t\t\t//光点最小外接矩形的尺寸，以最短的为准\r\n    \"outRectSizeMax\": 400,\t\t\t//光点最大外接矩形的尺寸\r\n    \"erodeKernel\": 3,\t\t\t\t//腐蚀的核，2-5之间，用于除底噪\r\n    \"erodeTime\": 0\t\t\t\t//腐蚀的次数\r\n  },\r\n  \"ClassicalParams\": {\r\n    \"slopeType\": 0,\t\t\t\t//斜率计算方法，0中心点九点取斜率；1 //去除方差较大的点后取斜率\r\n    \"layoutType\": 0,\t\t\t\t//理想点布点方法，0采用斜率布点；1不采用斜率布点\r\n    \"blobThreParams\": {\r\n      \"filterByColor\": true,\t\t\t// 是否使用颜色过滤\r\n      \"blobColor\": 0,\t\t\t\t// 亮斑255 暗斑0\r\n      \"minThreshold\": 10.0,\t\t\t// 斑点最小灰度\r\n      \"thresholdStep\": 10.0,\t\t// 阈值每次间隔值\r\n      \"maxThreshold\": 220.0,\t\t// 斑点最大灰度\r\n      \"darkRatio\": 0.01,\r\n      \"contrastRatio\": 0.1,\r\n      \"bgRadius\": 31,\r\n      \"minDistBetweenBlobs\": 50.0,\t\t// 斑点间隔距离\r\n      \"filterByArea\": true,\t\t\t\t// 是否使用面积过滤\r\n      \"minArea\": 200.0,\t\t\t\t// 斑点最小面积\r\n      \"maxArea\": 10000.0,\t\t\t\t// 斑点最大面积\r\n      \"minRepeatability\": 2,\t\t\t\t// 重复次数认定\r\n      \"filterByCircularity\": false,\t\t\t// 圆形度控制（圆，方）是否调用\r\n      \"minCircularity\": 0.9,\t\t\t\t\t//越接近1越接近圆\r\n      \"maxCircularity\": 3.4028235E+38,\t\t//越大越圆\r\n      \"filterByConvexity\": false,\t\t\t// 凸性控制形状控制（豁口）是否调用\r\n      \"minConvexity\": 0.9,\t\t\t\t\t//离1越近越没豁口\t\r\n      \"maxConvexity\": 3.4028235E+38,\t\t//越大越圆\r\n      \"filterByInertia\": false,\t\t\t// 椭圆度控制\r\n      \"minInertiaRatio\": 0.1,\t\t\t\t//0的话可以近似认为是直线，1的话基本是圆\r\n      \"maxInertiaRatio\": 3.4028235E+38\t\t//越大越圆\r\n    },\r\n    \"timeOut\": 50000\r\n  },\r\n  \"caclDistorType\": {\t\t\t\t\t//以下一般三选一，根据项目情况\r\n    \"Distortion9Point\": true,\t\t\t\t//是否计算九点畸变\r\n    \"DistortionTV\": true,\t\t\t\t//是否计算TV畸变\r\n    \"DistortionOptic\": true\t\t\t\t//是否计算光学畸变\r\n  },\r\n  \"rectCorner\": {//精定位\r\n    \"qualityLevel\": 0.04, //一般取0.04或0.06\r\n    \"cutWidth\": 200, //精定位裁剪的框\r\n    \"edge\": 10, //10-20\r\n    \"active\": true//是否启用\r\n  }\r\n}\r\n";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlDistortion2();

    }




}
