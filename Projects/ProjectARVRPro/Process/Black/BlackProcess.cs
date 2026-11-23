#pragma warning disable CS8601
using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Process.Black
{
    public class BlackProcess : ProcessWithRecipeBase<BlackProcessConfig, BlackRecipeConfig>
    {
        internal Func<int, List<AlgResultMasterModel>>? ReadMasters { get; init; }
        internal Func<int, List<PoiPointResultModel>>? ReadPoints { get; init; }

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            var white = ctx.ObjectiveTestResult.W255TestResult;
            var testResult = new BlackViewTestResult();
            try
            {
                if (ctx.Batch == null)
                    throw new InvalidDataException("黑场缺少测量批次。");

                ctx.Log.Info("开始 Black 流程");
                var images = ctx.GetMeasureResults();
                if (images.Count > 0)
                    ctx.Result.FileName = images[0].FileUrl;

                foreach (var master in (ReadMasters?.Invoke(ctx.Batch.Id) ?? ctx.ReadMasterResults()).Where(master => master.ImgFileType == ViewResultAlgType.POI_XYZ))
                {
                    var poiPoints = ReadPoints?.Invoke(master.Id) ?? ctx.ReadDetails(PoiPointResultDao.Instance, master.Id);
                    if (poiPoints.Count == 0)
                        continue;

                    int id = 0;
                    foreach (var item in poiPoints)
                    {
                        var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                        testResult.ViewPoixyuvDatas.Add(poi);
                        testResult.PoixyuvDatas.Add(new PoixyuvData { Id = poi.Id, Name = poi.Name, X = poi.X, Y = poi.Y, Z = poi.Z, x = poi.x, y = poi.y, u = poi.u, v = poi.v, CCT = poi.CCT, Wave = poi.Wave });
                    }

                    if (white != null && Config.IsUsingNing)
                    {
                        double whiteLuminance = white.PoixyuvDatas.Sum(point => point.Y);
                        double blackLuminance = testResult.PoixyuvDatas.Sum(point => point.Y);
                        double contrast = whiteLuminance / blackLuminance;
                        ctx.Log.Info($"白画面Sum:{whiteLuminance},黑画面Sum:{blackLuminance},contrast{contrast}");
                        ApplyContrast(ctx, testResult, contrast);
                    }
                }

                if (white == null)
                    ctx.Log.Info("计算对比度前需要白画面亮度");
                else if (!Config.IsUsingNing)
                {
                    if (white.CenterLunimance == null || !double.IsFinite(white.CenterLunimance.Value))
                        throw new InvalidDataException("FOFO 缺少有效的白场中心亮度。");
                    double blackLuminance = ReadCenterLuminance(testResult.ViewPoixyuvDatas);
                    ApplyContrast(ctx, testResult, white.CenterLunimance.Value / blackLuminance);
                }

                string snapshot = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.BlackTestResult = JsonConvert.DeserializeObject<BlackTestResult>(snapshot)
                    ?? throw new JsonSerializationException("无法生成黑场测量结果。");
                ctx.Result.ViewResultJson = snapshot;
                return Task.FromResult(true);
            }
            catch (Exception error)
            {
                string message = $"黑场解析失败：{error.Message}";
                ctx.Log.Error(message, error);
                Reject(ctx, message);
                return Task.FromResult(false);
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "黑场流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private static void Reject(IProcessExecutionContext ctx, string message)
        {
            ctx.Result.Result = false;
            ctx.Result.Msg = message;
            ctx.Result.ViewResultJson = string.Empty;
            ctx.ObjectiveTestResult.TotalResult = false;
            ctx.ObjectiveTestResult.BlackTestResult = null!;
        }

        private double ReadCenterLuminance(List<PoiResultCIExyuvData> points)
        {
            string key = Config.Key_Center;
            var center = string.IsNullOrWhiteSpace(key) ? null : points.FirstOrDefault(point =>
                string.Equals(point.POIPointResultModel.PoiName, key, StringComparison.OrdinalIgnoreCase));
            if (center == null)
                throw new InvalidDataException($"缺少已配置黑场中心点 [{key}] 的实测亮度。");

            string? payload = center.POIPointResultModel.Value;
            var luminance = string.IsNullOrWhiteSpace(payload) ? null : JObject.Parse(payload).GetValue("Y", StringComparison.Ordinal);
            bool hasMeasurement = luminance?.Type switch
            {
                JTokenType.Integer or JTokenType.Float => double.IsFinite(luminance.Value<double>()),
                JTokenType.String => double.TryParse(luminance.Value<string>(), NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out double value) && double.IsFinite(value),
                _ => false,
            };
            if (!hasMeasurement || !double.IsFinite(center.Y))
                throw new InvalidDataException($"黑场中心点 [{key}] 缺少有限的实测亮度 Y。");

            return center.Y;
        }

        private void ApplyContrast(IProcessExecutionContext ctx, BlackTestResult testResult, double contrast)
        {
            var recipe = Config.RecipeConfig.FOFOContrast;
            double corrected = recipe.Apply(contrast);
            testResult.FOFOContrast.LowLimit = recipe.Min;
            testResult.FOFOContrast.UpLimit = recipe.Max;
            testResult.FOFOContrast.Value = corrected;
            testResult.FOFOContrast.TestValue = corrected.ToString(Config.FormatString);
            ctx.Result.Result &= testResult.FOFOContrast.TestResult;
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
                PoiOverlayRenderer.Add(ctx.ImageView, item, CVRawOpen.FormatMessage(Config.DisplayTemplate, poiResultCIExyuvData));
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
