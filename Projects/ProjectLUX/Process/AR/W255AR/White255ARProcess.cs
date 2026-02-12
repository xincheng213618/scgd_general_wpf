using ColorVision.Database;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao, DeatilCommonDao
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using ProjectLUX.Fix;
using ProjectLUX.Process.Distortion;
using ProjectLUX.Process.W255;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ProjectLUX.Process.W255AR
{
    public class White255ARPProcessConfig : ProcessConfigBase
    {
        [Category("解析配置")]
        [DisplayName("Center解析Key")]
        [Description("用于解析Center数据的Key")]
        public string Key_Center { get => _Key_Center; set { _Key_Center = value; OnPropertyChanged(); } }
        private string _Key_Center = "P_5";

        [Category("解析配置")]
        [DisplayName("LuminanceUniformityTempName")]
        [Description("Luminance_uniformity")]
        public string LuminanceUniformityTempName { get => _LuminanceUniformityTempName; set { _LuminanceUniformityTempName = value; OnPropertyChanged(); } }
        private string _LuminanceUniformityTempName = "Luminance_uniformity";


        [Category("解析配置")]
        [DisplayName("ColorUniformityTempName")]
        [Description("Color_uniformity")]
        public string ColorUniformityTempName { get => _ColorUniformityTempName; set { _ColorUniformityTempName = value; OnPropertyChanged(); } }
        private string _ColorUniformityTempName = "Color_uniformity";
    }
    public class White255ARProcess : ProcessBase<White255ARPProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            W255ARRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<W255ARRecipeConfig>();
            W255ARFixConfig fixConfig = ctx.FixConfig.GetRequiredService<W255ARFixConfig>();
            W255ARViewTestResult testResult = new W255ARViewTestResult();

            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        testResult.PoixyuvDatas.Clear();

                        if(poiPoints.Count == 9)
                        {
                            // ======================= P1 =======================
                            var poi1 = new PoiResultCIExyuvData(poiPoints[0]);

                            // P1 Lv
                            testResult.P1Lv.Value = poi1.Y;
                            testResult.P1Lv.Value *= fixConfig.P1Lv;
                            testResult.P1Lv.LowLimit = recipeConfig.P1Lv.Min;
                            testResult.P1Lv.UpLimit = recipeConfig.P1Lv.Max;
                            testResult.P1Lv.TestValue = testResult.P1Lv.Value.ToString();
                            ctx.Result.Result &= testResult.P1Lv.TestResult;

                            // P1 Cx
                            testResult.P1Cx.Value = poi1.x;
                            testResult.P1Cx.Value *= fixConfig.P1Cx;
                            testResult.P1Cx.LowLimit = recipeConfig.P1Cx.Min;
                            testResult.P1Cx.UpLimit = recipeConfig.P1Cx.Max;
                            testResult.P1Cx.TestValue = testResult.P1Cx.Value.ToString();
                            ctx.Result.Result &= testResult.P1Cx.TestResult;

                            // P1 Cy
                            testResult.P1Cy.Value = poi1.y;
                            testResult.P1Cy.Value *= fixConfig.P1Cy;
                            testResult.P1Cy.LowLimit = recipeConfig.P1Cy.Min;
                            testResult.P1Cy.UpLimit = recipeConfig.P1Cy.Max;
                            testResult.P1Cy.TestValue = testResult.P1Cy.Value.ToString();
                            ctx.Result.Result &= testResult.P1Cy.TestResult;

                            // P1 Cu
                            testResult.P1Cu.Value = poi1.u;
                            testResult.P1Cu.Value *= fixConfig.P1Cu;
                            testResult.P1Cu.LowLimit = recipeConfig.P1Cu.Min;
                            testResult.P1Cu.UpLimit = recipeConfig.P1Cu.Max;
                            testResult.P1Cu.TestValue = testResult.P1Cu.Value.ToString();
                            ctx.Result.Result &= testResult.P1Cu.TestResult;

                            // P1 Cv
                            testResult.P1Cv.Value = poi1.v;
                            testResult.P1Cv.Value *= fixConfig.P1Cv;
                            testResult.P1Cv.LowLimit = recipeConfig.P1Cv.Min;
                            testResult.P1Cv.UpLimit = recipeConfig.P1Cv.Max;
                            testResult.P1Cv.TestValue = testResult.P1Cv.Value.ToString();
                            ctx.Result.Result &= testResult.P1Cv.TestResult;


                            // ======================= P2 =======================
                            var poi2 = new PoiResultCIExyuvData(poiPoints[1]);

                            testResult.P2Lv.Value = poi2.Y;
                            testResult.P2Lv.Value *= fixConfig.P2Lv;
                            testResult.P2Lv.LowLimit = recipeConfig.P2Lv.Min;
                            testResult.P2Lv.UpLimit = recipeConfig.P2Lv.Max;
                            testResult.P2Lv.TestValue = testResult.P2Lv.Value.ToString();
                            ctx.Result.Result &= testResult.P2Lv.TestResult;

                            testResult.P2Cx.Value = poi2.x;
                            testResult.P2Cx.Value *= fixConfig.P2Cx;
                            testResult.P2Cx.LowLimit = recipeConfig.P2Cx.Min;
                            testResult.P2Cx.UpLimit = recipeConfig.P2Cx.Max;
                            testResult.P2Cx.TestValue = testResult.P2Cx.Value.ToString();
                            ctx.Result.Result &= testResult.P2Cx.TestResult;

                            testResult.P2Cy.Value = poi2.y;
                            testResult.P2Cy.Value *= fixConfig.P2Cy;
                            testResult.P2Cy.LowLimit = recipeConfig.P2Cy.Min;
                            testResult.P2Cy.UpLimit = recipeConfig.P2Cy.Max;
                            testResult.P2Cy.TestValue = testResult.P2Cy.Value.ToString();
                            ctx.Result.Result &= testResult.P2Cy.TestResult;

                            testResult.P2Cu.Value = poi2.u;
                            testResult.P2Cu.Value *= fixConfig.P2Cu;
                            testResult.P2Cu.LowLimit = recipeConfig.P2Cu.Min;
                            testResult.P2Cu.UpLimit = recipeConfig.P2Cu.Max;
                            testResult.P2Cu.TestValue = testResult.P2Cu.Value.ToString();
                            ctx.Result.Result &= testResult.P2Cu.TestResult;

                            testResult.P2Cv.Value = poi2.v;
                            testResult.P2Cv.Value *= fixConfig.P2Cv;
                            testResult.P2Cv.LowLimit = recipeConfig.P2Cv.Min;
                            testResult.P2Cv.UpLimit = recipeConfig.P2Cv.Max;
                            testResult.P2Cv.TestValue = testResult.P2Cv.Value.ToString();
                            ctx.Result.Result &= testResult.P2Cv.TestResult;


                            // ======================= P3 =======================
                            var poi3 = new PoiResultCIExyuvData(poiPoints[2]);

                            testResult.P3Lv.Value = poi3.Y;
                            testResult.P3Lv.Value *= fixConfig.P3Lv;
                            testResult.P3Lv.LowLimit = recipeConfig.P3Lv.Min;
                            testResult.P3Lv.UpLimit = recipeConfig.P3Lv.Max;
                            testResult.P3Lv.TestValue = testResult.P3Lv.Value.ToString();
                            ctx.Result.Result &= testResult.P3Lv.TestResult;

                            testResult.P3Cx.Value = poi3.x;
                            testResult.P3Cx.Value *= fixConfig.P3Cx;
                            testResult.P3Cx.LowLimit = recipeConfig.P3Cx.Min;
                            testResult.P3Cx.UpLimit = recipeConfig.P3Cx.Max;
                            testResult.P3Cx.TestValue = testResult.P3Cx.Value.ToString();
                            ctx.Result.Result &= testResult.P3Cx.TestResult;

                            testResult.P3Cy.Value = poi3.y;
                            testResult.P3Cy.Value *= fixConfig.P3Cy;
                            testResult.P3Cy.LowLimit = recipeConfig.P3Cy.Min;
                            testResult.P3Cy.UpLimit = recipeConfig.P3Cy.Max;
                            testResult.P3Cy.TestValue = testResult.P3Cy.Value.ToString();
                            ctx.Result.Result &= testResult.P3Cy.TestResult;

                            testResult.P3Cu.Value = poi3.u;
                            testResult.P3Cu.Value *= fixConfig.P3Cu;
                            testResult.P3Cu.LowLimit = recipeConfig.P3Cu.Min;
                            testResult.P3Cu.UpLimit = recipeConfig.P3Cu.Max;
                            testResult.P3Cu.TestValue = testResult.P3Cu.Value.ToString();
                            ctx.Result.Result &= testResult.P3Cu.TestResult;

                            testResult.P3Cv.Value = poi3.v;
                            testResult.P3Cv.Value *= fixConfig.P3Cv;
                            testResult.P3Cv.LowLimit = recipeConfig.P3Cv.Min;
                            testResult.P3Cv.UpLimit = recipeConfig.P3Cv.Max;
                            testResult.P3Cv.TestValue = testResult.P3Cv.Value.ToString();
                            ctx.Result.Result &= testResult.P3Cv.TestResult;


                            // ======================= P4 =======================
                            var poi4 = new PoiResultCIExyuvData(poiPoints[3]);

                            testResult.P4Lv.Value = poi4.Y;
                            testResult.P4Lv.Value *= fixConfig.P4Lv;
                            testResult.P4Lv.LowLimit = recipeConfig.P4Lv.Min;
                            testResult.P4Lv.UpLimit = recipeConfig.P4Lv.Max;
                            testResult.P4Lv.TestValue = testResult.P4Lv.Value.ToString();
                            ctx.Result.Result &= testResult.P4Lv.TestResult;

                            testResult.P4Cx.Value = poi4.x;
                            testResult.P4Cx.Value *= fixConfig.P4Cx;
                            testResult.P4Cx.LowLimit = recipeConfig.P4Cx.Min;
                            testResult.P4Cx.UpLimit = recipeConfig.P4Cx.Max;
                            testResult.P4Cx.TestValue = testResult.P4Cx.Value.ToString();
                            ctx.Result.Result &= testResult.P4Cx.TestResult;

                            testResult.P4Cy.Value = poi4.y;
                            testResult.P4Cy.Value *= fixConfig.P4Cy;
                            testResult.P4Cy.LowLimit = recipeConfig.P4Cy.Min;
                            testResult.P4Cy.UpLimit = recipeConfig.P4Cy.Max;
                            testResult.P4Cy.TestValue = testResult.P4Cy.Value.ToString();
                            ctx.Result.Result &= testResult.P4Cy.TestResult;

                            testResult.P4Cu.Value = poi4.u;
                            testResult.P4Cu.Value *= fixConfig.P4Cu;
                            testResult.P4Cu.LowLimit = recipeConfig.P4Cu.Min;
                            testResult.P4Cu.UpLimit = recipeConfig.P4Cu.Max;
                            testResult.P4Cu.TestValue = testResult.P4Cu.Value.ToString();
                            ctx.Result.Result &= testResult.P4Cu.TestResult;

                            testResult.P4Cv.Value = poi4.v;
                            testResult.P4Cv.Value *= fixConfig.P4Cv;
                            testResult.P4Cv.LowLimit = recipeConfig.P4Cv.Min;
                            testResult.P4Cv.UpLimit = recipeConfig.P4Cv.Max;
                            testResult.P4Cv.TestValue = testResult.P4Cv.Value.ToString();
                            ctx.Result.Result &= testResult.P4Cv.TestResult;


                            // ======================= P5 =======================
                            var poi5 = new PoiResultCIExyuvData(poiPoints[4]);

                            testResult.P5Lv.Value = poi5.Y;
                            testResult.P5Lv.Value *= fixConfig.P5Lv;
                            testResult.P5Lv.LowLimit = recipeConfig.P5Lv.Min;
                            testResult.P5Lv.UpLimit = recipeConfig.P5Lv.Max;
                            testResult.P5Lv.TestValue = testResult.P5Lv.Value.ToString();
                            ctx.Result.Result &= testResult.P5Lv.TestResult;

                            testResult.P5Cx.Value = poi5.x;
                            testResult.P5Cx.Value *= fixConfig.P5Cx;
                            testResult.P5Cx.LowLimit = recipeConfig.P5Cx.Min;
                            testResult.P5Cx.UpLimit = recipeConfig.P5Cx.Max;
                            testResult.P5Cx.TestValue = testResult.P5Cx.Value.ToString();
                            ctx.Result.Result &= testResult.P5Cx.TestResult;

                            testResult.P5Cy.Value = poi5.y;
                            testResult.P5Cy.Value *= fixConfig.P5Cy;
                            testResult.P5Cy.LowLimit = recipeConfig.P5Cy.Min;
                            testResult.P5Cy.UpLimit = recipeConfig.P5Cy.Max;
                            testResult.P5Cy.TestValue = testResult.P5Cy.Value.ToString();
                            ctx.Result.Result &= testResult.P5Cy.TestResult;

                            testResult.P5Cu.Value = poi5.u;
                            testResult.P5Cu.Value *= fixConfig.P5Cu;
                            testResult.P5Cu.LowLimit = recipeConfig.P5Cu.Min;
                            testResult.P5Cu.UpLimit = recipeConfig.P5Cu.Max;
                            testResult.P5Cu.TestValue = testResult.P5Cu.Value.ToString();
                            ctx.Result.Result &= testResult.P5Cu.TestResult;

                            testResult.P5Cv.Value = poi5.v;
                            testResult.P5Cv.Value *= fixConfig.P5Cv;
                            testResult.P5Cv.LowLimit = recipeConfig.P5Cv.Min;
                            testResult.P5Cv.UpLimit = recipeConfig.P5Cv.Max;
                            testResult.P5Cv.TestValue = testResult.P5Cv.Value.ToString();
                            ctx.Result.Result &= testResult.P5Cv.TestResult;


                            // ======================= P6 =======================
                            var poi6 = new PoiResultCIExyuvData(poiPoints[5]);

                            testResult.P6Lv.Value = poi6.Y;
                            testResult.P6Lv.Value *= fixConfig.P6Lv;
                            testResult.P6Lv.LowLimit = recipeConfig.P6Lv.Min;
                            testResult.P6Lv.UpLimit = recipeConfig.P6Lv.Max;
                            testResult.P6Lv.TestValue = testResult.P6Lv.Value.ToString();
                            ctx.Result.Result &= testResult.P6Lv.TestResult;

                            testResult.P6Cx.Value = poi6.x;
                            testResult.P6Cx.Value *= fixConfig.P6Cx;
                            testResult.P6Cx.LowLimit = recipeConfig.P6Cx.Min;
                            testResult.P6Cx.UpLimit = recipeConfig.P6Cx.Max;
                            testResult.P6Cx.TestValue = testResult.P6Cx.Value.ToString();
                            ctx.Result.Result &= testResult.P6Cx.TestResult;

                            testResult.P6Cy.Value = poi6.y;
                            testResult.P6Cy.Value *= fixConfig.P6Cy;
                            testResult.P6Cy.LowLimit = recipeConfig.P6Cy.Min;
                            testResult.P6Cy.UpLimit = recipeConfig.P6Cy.Max;
                            testResult.P6Cy.TestValue = testResult.P6Cy.Value.ToString();
                            ctx.Result.Result &= testResult.P6Cy.TestResult;

                            testResult.P6Cu.Value = poi6.u;
                            testResult.P6Cu.Value *= fixConfig.P6Cu;
                            testResult.P6Cu.LowLimit = recipeConfig.P6Cu.Min;
                            testResult.P6Cu.UpLimit = recipeConfig.P6Cu.Max;
                            testResult.P6Cu.TestValue = testResult.P6Cu.Value.ToString();
                            ctx.Result.Result &= testResult.P6Cu.TestResult;

                            testResult.P6Cv.Value = poi6.v;
                            testResult.P6Cv.Value *= fixConfig.P6Cv;
                            testResult.P6Cv.LowLimit = recipeConfig.P6Cv.Min;
                            testResult.P6Cv.UpLimit = recipeConfig.P6Cv.Max;
                            testResult.P6Cv.TestValue = testResult.P6Cv.Value.ToString();
                            ctx.Result.Result &= testResult.P6Cv.TestResult;


                            // ======================= P7 =======================
                            var poi7 = new PoiResultCIExyuvData(poiPoints[6]);

                            testResult.P7Lv.Value = poi7.Y;
                            testResult.P7Lv.Value *= fixConfig.P7Lv;
                            testResult.P7Lv.LowLimit = recipeConfig.P7Lv.Min;
                            testResult.P7Lv.UpLimit = recipeConfig.P7Lv.Max;
                            testResult.P7Lv.TestValue = testResult.P7Lv.Value.ToString();
                            ctx.Result.Result &= testResult.P7Lv.TestResult;

                            testResult.P7Cx.Value = poi7.x;
                            testResult.P7Cx.Value *= fixConfig.P7Cx;
                            testResult.P7Cx.LowLimit = recipeConfig.P7Cx.Min;
                            testResult.P7Cx.UpLimit = recipeConfig.P7Cx.Max;
                            testResult.P7Cx.TestValue = testResult.P7Cx.Value.ToString();
                            ctx.Result.Result &= testResult.P7Cx.TestResult;

                            testResult.P7Cy.Value = poi7.y;
                            testResult.P7Cy.Value *= fixConfig.P7Cy;
                            testResult.P7Cy.LowLimit = recipeConfig.P7Cy.Min;
                            testResult.P7Cy.UpLimit = recipeConfig.P7Cy.Max;
                            testResult.P7Cy.TestValue = testResult.P7Cy.Value.ToString();
                            ctx.Result.Result &= testResult.P7Cy.TestResult;

                            testResult.P7Cu.Value = poi7.u;
                            testResult.P7Cu.Value *= fixConfig.P7Cu;
                            testResult.P7Cu.LowLimit = recipeConfig.P7Cu.Min;
                            testResult.P7Cu.UpLimit = recipeConfig.P7Cu.Max;
                            testResult.P7Cu.TestValue = testResult.P7Cu.Value.ToString();
                            ctx.Result.Result &= testResult.P7Cu.TestResult;

                            testResult.P7Cv.Value = poi7.v;
                            testResult.P7Cv.Value *= fixConfig.P7Cv;
                            testResult.P7Cv.LowLimit = recipeConfig.P7Cv.Min;
                            testResult.P7Cv.UpLimit = recipeConfig.P7Cv.Max;
                            testResult.P7Cv.TestValue = testResult.P7Cv.Value.ToString();
                            ctx.Result.Result &= testResult.P7Cv.TestResult;


                            // ======================= P8 =======================
                            var poi8 = new PoiResultCIExyuvData(poiPoints[7]);

                            testResult.P8Lv.Value = poi8.Y;
                            testResult.P8Lv.Value *= fixConfig.P8Lv;
                            testResult.P8Lv.LowLimit = recipeConfig.P8Lv.Min;
                            testResult.P8Lv.UpLimit = recipeConfig.P8Lv.Max;
                            testResult.P8Lv.TestValue = testResult.P8Lv.Value.ToString();
                            ctx.Result.Result &= testResult.P8Lv.TestResult;

                            testResult.P8Cx.Value = poi8.x;
                            testResult.P8Cx.Value *= fixConfig.P8Cx;
                            testResult.P8Cx.LowLimit = recipeConfig.P8Cx.Min;
                            testResult.P8Cx.UpLimit = recipeConfig.P8Cx.Max;
                            testResult.P8Cx.TestValue = testResult.P8Cx.Value.ToString();
                            ctx.Result.Result &= testResult.P8Cx.TestResult;

                            testResult.P8Cy.Value = poi8.y;
                            testResult.P8Cy.Value *= fixConfig.P8Cy;
                            testResult.P8Cy.LowLimit = recipeConfig.P8Cy.Min;
                            testResult.P8Cy.UpLimit = recipeConfig.P8Cy.Max;
                            testResult.P8Cy.TestValue = testResult.P8Cy.Value.ToString();
                            ctx.Result.Result &= testResult.P8Cy.TestResult;

                            testResult.P8Cu.Value = poi8.u;
                            testResult.P8Cu.Value *= fixConfig.P8Cu;
                            testResult.P8Cu.LowLimit = recipeConfig.P8Cu.Min;
                            testResult.P8Cu.UpLimit = recipeConfig.P8Cu.Max;
                            testResult.P8Cu.TestValue = testResult.P8Cu.Value.ToString();
                            ctx.Result.Result &= testResult.P8Cu.TestResult;

                            testResult.P8Cv.Value = poi8.v;
                            testResult.P8Cv.Value *= fixConfig.P8Cv;
                            testResult.P8Cv.LowLimit = recipeConfig.P8Cv.Min;
                            testResult.P8Cv.UpLimit = recipeConfig.P8Cv.Max;
                            testResult.P8Cv.TestValue = testResult.P8Cv.Value.ToString();
                            ctx.Result.Result &= testResult.P8Cv.TestResult;


                            // ======================= P9 =======================
                            var poi9 = new PoiResultCIExyuvData(poiPoints[8]);

                            testResult.P9Lv.Value = poi9.Y;
                            testResult.P9Lv.Value *= fixConfig.P9Lv;
                            testResult.P9Lv.LowLimit = recipeConfig.P9Lv.Min;
                            testResult.P9Lv.UpLimit = recipeConfig.P9Lv.Max;
                            testResult.P9Lv.TestValue = testResult.P9Lv.Value.ToString();
                            ctx.Result.Result &= testResult.P9Lv.TestResult;

                            testResult.P9Cx.Value = poi9.x;
                            testResult.P9Cx.Value *= fixConfig.P9Cx;
                            testResult.P9Cx.LowLimit = recipeConfig.P9Cx.Min;
                            testResult.P9Cx.UpLimit = recipeConfig.P9Cx.Max;
                            testResult.P9Cx.TestValue = testResult.P9Cx.Value.ToString();
                            ctx.Result.Result &= testResult.P9Cx.TestResult;

                            testResult.P9Cy.Value = poi9.y;
                            testResult.P9Cy.Value *= fixConfig.P9Cy;
                            testResult.P9Cy.LowLimit = recipeConfig.P9Cy.Min;
                            testResult.P9Cy.UpLimit = recipeConfig.P9Cy.Max;
                            testResult.P9Cy.TestValue = testResult.P9Cy.Value.ToString();
                            ctx.Result.Result &= testResult.P9Cy.TestResult;

                            testResult.P9Cu.Value = poi9.u;
                            testResult.P9Cu.Value *= fixConfig.P9Cu;
                            testResult.P9Cu.LowLimit = recipeConfig.P9Cu.Min;
                            testResult.P9Cu.UpLimit = recipeConfig.P9Cu.Max;
                            testResult.P9Cu.TestValue = testResult.P9Cu.Value.ToString();
                            ctx.Result.Result &= testResult.P9Cu.TestResult;

                            testResult.P9Cv.Value = poi9.v;
                            testResult.P9Cv.Value *= fixConfig.P9Cv;
                            testResult.P9Cv.LowLimit = recipeConfig.P9Cv.Min;
                            testResult.P9Cv.UpLimit = recipeConfig.P9Cv.Max;
                            testResult.P9Cv.TestValue = testResult.P9Cv.Value.ToString();
                            ctx.Result.Result &= testResult.P9Cv.TestResult;
                        }


                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            poi.CCT *= fixConfig.CenterCorrelatedColorTemperature;
                            poi.Y *= fixConfig.CenterLunimance;
                            poi.x *= fixConfig.CenterCIE1931ChromaticCoordinatesx;
                            poi.y *= fixConfig.CenterCIE1931ChromaticCoordinatesy;
                            poi.u *= fixConfig.CenterCIE1976ChromaticCoordinatesu;
                            poi.v *= fixConfig.CenterCIE1976ChromaticCoordinatesv;
                            testResult.PoixyuvDatas.Add(poi);


                            if (item.PoiName == Config.Key_Center)
                            {
                                testResult.CenterLunimance.Value = poi.Y;
                                testResult.CenterLunimance.Value *= fixConfig.CenterLunimance;
                                testResult.CenterLunimance.LowLimit = recipeConfig.CenterLunimance.Min;
                                testResult.CenterLunimance.UpLimit = recipeConfig.CenterLunimance.Max;
                                testResult.CenterLunimance.TestValue = testResult.CenterLunimance.Value.ToString("F3") + " nit";
                                ctx.Result.Result &= testResult.CenterLunimance.TestResult;


                                testResult.CenterCIE1931ChromaticCoordinatesx.Value = poi.x;
                                testResult.CenterCIE1931ChromaticCoordinatesx.Value *= fixConfig.CenterCIE1931ChromaticCoordinatesx;
                                testResult.CenterCIE1931ChromaticCoordinatesx.LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Min;
                                testResult.CenterCIE1931ChromaticCoordinatesx.UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Max;
                                testResult.CenterCIE1931ChromaticCoordinatesx.TestValue = testResult.CenterCIE1931ChromaticCoordinatesx.Value.ToString("F3");
                                ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesx.TestResult;

                                testResult.CenterCIE1931ChromaticCoordinatesy.Value = poi.x;
                                testResult.CenterCIE1931ChromaticCoordinatesy.Value *= fixConfig.CenterCIE1931ChromaticCoordinatesy;
                                testResult.CenterCIE1931ChromaticCoordinatesy.LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Min;
                                testResult.CenterCIE1931ChromaticCoordinatesy.UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Max;
                                testResult.CenterCIE1931ChromaticCoordinatesy.TestValue = testResult.CenterCIE1931ChromaticCoordinatesy.Value.ToString("F3");
                                ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesy.TestResult;

                                testResult.CenterCIE1976ChromaticCoordinatesu.Value = poi.u;
                                testResult.CenterCIE1976ChromaticCoordinatesu.Value *= fixConfig.CenterCIE1976ChromaticCoordinatesu;
                                testResult.CenterCIE1976ChromaticCoordinatesu.LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Min;
                                testResult.CenterCIE1976ChromaticCoordinatesu.UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Max;
                                testResult.CenterCIE1976ChromaticCoordinatesu.TestValue = testResult.CenterCIE1976ChromaticCoordinatesu.Value.ToString("F3");
                                ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesu.TestResult;



                                testResult.CenterCIE1976ChromaticCoordinatesv.Value = poi.v;
                                testResult.CenterCIE1976ChromaticCoordinatesv.Value *= fixConfig.CenterCIE1976ChromaticCoordinatesv;
                                testResult.CenterCIE1976ChromaticCoordinatesv.LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Min;
                                testResult.CenterCIE1976ChromaticCoordinatesv.UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Max;
                                testResult.CenterCIE1976ChromaticCoordinatesv.TestValue = testResult.CenterCIE1976ChromaticCoordinatesv.Value.ToString("F3");
                                ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesv.TestResult;
                            }
                        }
                    }

                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis)
                    {
                        if (master.TName.Contains(Config.LuminanceUniformityTempName))
                        {
                            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                            if (details.Count == 1)
                            {
                                var view = new PoiAnalysisDetailViewReslut(details[0]);

                                view.PoiAnalysisResult.result.Value *= fixConfig.LuminanceUniformity;
                                testResult.LuminanceUniformity.Value = view.PoiAnalysisResult.result.Value;
                                testResult.LuminanceUniformity.TestValue = (view.PoiAnalysisResult.result.Value * 100).ToString("F3") + "%";
                                testResult.LuminanceUniformity.LowLimit = recipeConfig.LuminanceUniformity.Min;
                                testResult.LuminanceUniformity.UpLimit = recipeConfig.LuminanceUniformity.Max;

                                ctx.Result.Result &= testResult.LuminanceUniformity.TestResult;

                            }
                        }

                        if (master.TName.Contains(Config.ColorUniformityTempName))
                        {
                            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                            if (details.Count == 1)
                            {
                                var view = new PoiAnalysisDetailViewReslut(details[0]);

                                view.PoiAnalysisResult.result.Value *= fixConfig.ColorUniformity;
                                testResult.ColorUniformity.Value = view.PoiAnalysisResult.result.Value;
                                testResult.ColorUniformity.TestValue = view.PoiAnalysisResult.result.Value.ToString("F5");
                                testResult.ColorUniformity.LowLimit = recipeConfig.ColorUniformity.Min;
                                testResult.ColorUniformity.UpLimit = recipeConfig.ColorUniformity.Max;
                                ctx.Result.Result &= testResult.ColorUniformity.TestResult;
                            }
                        }
                    }

                }
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.W255ARTestResult = JsonConvert.DeserializeObject<W255ARTestResult>(ctx.Result.ViewResultJson) ?? new W255ARTestResult();

                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }

        public override void Render (IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            W255ARViewTestResult testResult = JsonConvert.DeserializeObject<W255ARViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var poiResultCIExyuvData in testResult.PoixyuvDatas)
            {
                var item = poiResultCIExyuvData.Point;
                switch (item.PointType)
                {
                    case POIPointTypes.Circle:
                        DVCircleText Circle = new DVCircleText();
                        Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        Circle.Attribute.Radius = item.Radius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Circle.Attribute.Id = item.Id ?? -1;
                        Circle.Attribute.Text = item.Name;
                        Circle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                        Circle.Render();
                        ctx.ImageView.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        DVRectangleText Rectangle = new DVRectangleText();
                        Rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Rectangle.Attribute.Id = item.Id ?? -1;
                        Rectangle.Attribute.Text = item.Name;
                        Rectangle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                        Rectangle.Render();
                        ctx.ImageView.AddVisual(Rectangle);
                        break;
                    default:
                        break;
                }
            }

        }

        public override string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"W255 画面结果" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            W255ARViewTestResult testResult = JsonConvert.DeserializeObject<W255ARViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;

            foreach (var item in testResult.PoixyuvDatas)
            {
                outtext += $"X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
            }

            outtext += $"Luminance_uniformity:{testResult.LuminanceUniformity.TestValue} LowLimit:{testResult.LuminanceUniformity.LowLimit}  UpLimit:{testResult.LuminanceUniformity.UpLimit},Rsult{(testResult.LuminanceUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"Color_uniformity:{testResult.ColorUniformity.TestValue} LowLimit:{testResult.ColorUniformity.LowLimit} UpLimit:{testResult.ColorUniformity.UpLimit},Rsult{(testResult.ColorUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }

        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<W255ARRecipeConfig>();
        }

        public override IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<W255ARFixConfig>();
        }
    }
}
