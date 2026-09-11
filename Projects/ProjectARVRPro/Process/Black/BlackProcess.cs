#pragma warning disable CS8601
using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Black
{
    public class BlackProcess : ProcessWithRecipeBase<BlackProcessConfig, BlackRecipeConfig>
    {
        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            BlackRecipeConfig recipeConfig = Config.RecipeConfig;
            BlackViewTestResult testResult = new BlackViewTestResult();

            try
            {
                log?.Info("开始 Black 流程");

                var values = ctx.GetMeasureResults();
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        if (poiPoints.Count == 0) continue;

                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            testResult.ViewPoixyuvDatas.Add(poi);
                            testResult.PoixyuvDatas.Add(new PoixyuvData() { Id = poi.Id, Name = poi.Name, X = poi.X, Y = poi.Y, Z = poi.Z, x = poi.x, y = poi.y, u = poi.u, v = poi.v, CCT = poi.CCT, Wave = poi.Wave });
                        }

                        // 需要白画面亮度才能计算对比度
                        if (ctx.ObjectiveTestResult.W255TestResult != null)
                        {
                            double contrast = 0;
                            if (Config.IsUsingNing)
                            {
                                double whiteLuminance = ctx.ObjectiveTestResult.W255TestResult.PoixyuvDatas.Sum(x => x.Y);
                                double blackLuminance = testResult.PoixyuvDatas.Sum(x => x.Y);
                                contrast = whiteLuminance / blackLuminance;
                                log?.Info($"白画面Sum:{whiteLuminance},黑画面Sum:{blackLuminance},contrast{contrast}");
                            }
                            else
                            {
                                if (ctx.ObjectiveTestResult.W255TestResult.CenterLunimance != null)
                                    contrast = ctx.ObjectiveTestResult.W255TestResult.CenterLunimance.Value / testResult.ViewPoixyuvDatas[0].Y;
                            }

                            contrast = recipeConfig.FOFOContrast.Apply(contrast);
                            testResult.FOFOContrast.LowLimit = recipeConfig.FOFOContrast.Min;
                            testResult.FOFOContrast.UpLimit = recipeConfig.FOFOContrast.Max;
                            testResult.FOFOContrast.Value = contrast;
                            testResult.FOFOContrast.TestValue = contrast.ToString(Config.FormatString);

                            ctx.Result.Result &= testResult.FOFOContrast.TestResult;
                        }
                        else
                        {
                            log?.Info("计算对比度前需要白画面亮度");
                        }
                    }


                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.BlackTestResult = JsonConvert.DeserializeObject<BlackTestResult>(ctx.Result.ViewResultJson) ?? new BlackTestResult();

                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<BlackTestResult>(result, "Black");

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            BlackViewTestResult testResult = JsonConvert.DeserializeObject<BlackViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;


            foreach (var poiResultCIExyuvData in testResult.ViewPoixyuvDatas)
            {
                var item = poiResultCIExyuvData.Point;
                PoiOverlayRenderer.Add(ctx.ImageView, item, CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData));
            }
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            var result = ctx.Result;
            string outtext = string.Empty;

            outtext += $"黑画面" + Environment.NewLine;
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            BlackViewTestResult testResult = JsonConvert.DeserializeObject<BlackViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            outtext += $"FOFOContrast:{testResult.FOFOContrast.TestValue}  LowLimit:{testResult.FOFOContrast.LowLimit} UpLimit:{testResult.FOFOContrast.UpLimit},Rsult{(testResult.FOFOContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            AppendPlainText(paragraph, outtext, foreground, fontSize); return;
        }

    }
}
