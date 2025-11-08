using ColorVision.Database;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.PoiAnalysis
{

    public class TJPoiAnalysisParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJPoiAnalysisParam));



        public TJPoiAnalysisParam() : base()
        {
        }

        public TJPoiAnalysisParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplatePoiAnalysis : ITemplateJson<TJPoiAnalysisParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJPoiAnalysisParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJPoiAnalysisParam>>();

        public TemplatePoiAnalysis()
        {
            Title = ColorVision.Engine.Properties.Resources.PoiAnalysisTemplateManagement;
            Code = "PoiAnalysis";
            Name = "PoiAnalysis";
            TemplateDicId = 44;
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
        public string Description { get; set; } = "{\r\n\"analysisType\": 0,\t\t\t//数据分析类型如下：\r\n\r\n/*-----------------poi分析类型-----------------------*/\r\n CheckerboardContrast = 0,\t\t\t\t//棋盘格对比度\r\n LumUniformity_max = 1,\t\t\t\t//亮度均匀性1\r\n LumUniformity_average = 2,\t\t\t\t//亮度均匀性2\r\nLumUniformity_averageUN = 3,\t\t\t//亮度均匀性3\r\n ChromaUniformity_normal = 7,        //点集中任意两点uv方差，取最大的\r\n ChromaUniformity_xyRange = 8,       //点集中cie_x及cie_y的极差\r\n/*-----------------poi分析类型-----------------------*/\r\n\r\n    \"CheckerboardContrast\": {\t\t//当analysisType== CheckerboardContrast会寻找此字段\r\n        \"numX\": 4,\t\t\t//棋盘格横向数量\r\n        \"numY\": 4,\t\t\t//棋盘格纵向数量\r\n        \"firstIsBlack\": true\t\t//棋盘格首个方格是否为黑\r\n    },\r\n    \"OpticsPoi\": [{\t\t\t//关注点数据list\r\n            \"shape\": 1,\t//关注点类型，0代表圆，1代表矩形\r\n            \"px\": 3274,\t//关注点中心x\r\n            \"py\": 1522,\t//关注点中心y\r\n            \"w_radius\": 288,\t\t\t//关注点长或者半径\r\n            \"h\": 202,\t\t\t\t\t//关注点高（shape为矩形时生效\r\n            \"X\": 14220.9,\t\t\t\t//光学三刺激值X\r\n            \"Y_LUM\": 15680.362,\t\t//光学三刺激值Y（亮度）\r\n            \"Z\": 10611.174,\t\t\t//光学三刺激值Z\r\n            \"cie_x\": 0.35102555,\t\t//色度坐标x\r\n            \"cie_y\": 0.38705057,\t\t//色度坐标y\r\n            \"cie_u\": 0.2022457,\t\t//色度坐标u\r\n            \"cie_v\": 0.501754,\t\t\t//色度坐标v\r\n            \"CCT\": 4903.0337,\t\t\t//色温\r\n            \"dominantWave\": 565.9722\t//主波长\r\n        }\r\n\t\t…………………….\r\n\t\t………………………\r\n\t\t………………………\r\n, {\r\n            \"shape\": 1,\r\n            \"px\": 6268,\r\n            \"py\": 4627,\r\n            \"w_radius\": 647,\r\n            \"h\": 292,\r\n            \"X\": 16066.727,\r\n            \"Y_LUM\": 17721.072,\r\n            \"Z\": 10956.619,\r\n            \"cie_x\": 0.3590778,\r\n            \"cie_y\": 0.39605105,\r\n            \"cie_u\": 0.20418224,\r\n            \"cie_v\": 0.5067142,\r\n            \"CCT\": 4696.043,\r\n            \"dominantWave\": 568.0045\r\n        }\r\n    ]\r\n}\r\n";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlPoiAnalysis();

    }




}
