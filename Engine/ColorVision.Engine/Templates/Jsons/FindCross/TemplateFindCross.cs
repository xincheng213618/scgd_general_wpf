using ColorVision.Database;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.FindCross
{

    public class TJFindCrossParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJFindCrossParam));



        public TJFindCrossParam() : base()
        {
        }

        public TJFindCrossParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateFindCross : ITemplateJson<TJFindCrossParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJFindCrossParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJFindCrossParam>>();

        public TemplateFindCross()
        {
            Title = ColorVision.Engine.Properties.Resources.CrossCalculationTemplateManagement;
            Code = "FindCross";
            Name = "Json";
            TemplateDicId = 45;
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
        public string Description { get; set; } = "{\r\n    \"debugCfg\": {\t\t\t//debug相关配置\r\n        \"Debug\": true,\r\n        \"debugPath\": \"Result\\\\\",\r\n        \"debugImgResize\": 2\r\n    },\r\n    \"mathMaskRect\": {\t\t\t//mask相关配置\r\n        \"enable\": false,\r\n        \"x\": 0,\r\n        \"y\": 0,\r\n        \"w\": 0,\r\n        \"h\": 0\r\n    },\r\n    \"CheckLine\": {\t\t//Hough找线段相关参数\r\n        \"rho\": 5.0,\t\t\t//1-5之间\r\n        \"floAngle\": 10.0,\t\t//浮动角度\r\n        \"houghV\": 100\t\t\t//50-200\r\n    },\r\n    \"opticsParams\": {\r\n        \"stdCenter\": {\t\t\t//光学标定坐标\r\n            \"x\": 0,\r\n            \"y\": 0\r\n        },\r\n        \"objectDistance\": 500.0,\t\t//物距  单位: mm\r\n        \"focusLength\": 14.5,\t \t//焦距\t单位: mm\r\n        \"sensorPixSize\": 3.76\t\t//senser尺寸\t单位: um\r\n    },\r\n    \"erodeAndDiate\": {\t\t\t//膨胀腐蚀相关参数\r\n        \"erodeKernel\": 3,\t\t//腐蚀的核\r\n        \"erodeTime\": 1,\t\t//腐蚀的次数\r\n        \"dilateKernel\": 3,\t\t//膨胀的核\r\n        \"dilateTime\": 0,\t\t//膨胀的次数\r\n        \"erodeFirst\": true\t\t//true代表先腐蚀后膨胀\r\n    },\r\n    \"caclWay\": 1,\t\t\t//0代表使用hough方式；1代表二值化方式\r\n\"findEndPointWay\": 1,\t\t\t//0代表找极点方法；1代表骨架算法\r\n    \"threshold\": 80,\t\t//二值化阈值\r\n\"binaryByContours\": true,\t//是否激活先找轮廓，然后找轮廓内部灰度值，将这个值乘以\r\nbinaryRateInContours作为最终二值化阈值\r\n  \t\"binaryRateInContours\": 0.75,\t//轮廓内灰度与此系数相乘得到二值化阈值\r\n    \"minLineLength\": 40,\t//线条最小长度\r\n    \"maxLineGap\": 30\t,\t//线条允许的最大断连间隔\r\n \t\"blurKernel\": 3,\t\t\t//滤波的核\r\n  \t\"singleErodeKernel\": 15\t\t//水平或垂直单向腐蚀的核\r\n}\r\n";
        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlFindCross();

    }




}
