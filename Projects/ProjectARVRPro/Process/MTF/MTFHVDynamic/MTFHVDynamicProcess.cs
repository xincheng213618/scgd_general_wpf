using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Recipe;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace ProjectARVRPro.Process.MTF.MTFHVDynamic
{
    public class MTFHVDynamicProcess : ProcessBase<MTFHVDynamicProcessConfig>
    {
        internal Func<int, List<AlgResultMasterModel>>? ReadMasters { get; init; }
        internal Func<int, List<DetailCommonModel>>? ReadDetails { get; init; }

        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            string outputName = GetOutputName();
            try
            {
                if (ctx.Batch == null)
                    throw new InvalidDataException("MTF-HV 缺少测量批次。");

                var values = ctx.GetMeasureResults();
                if (values.Count > 0)
                {
                    string? fileUrl = values[0].FileUrl;
                    if (!string.IsNullOrWhiteSpace(fileUrl))
                        ctx.Result.FileName = fileUrl;
                }

                var testResult = new MTFHVDynamicViewTestResult();
                var recipe = Config.RecipeConfig;
                Dictionary<string, ObjectiveTestItem> itemMap = new(StringComparer.OrdinalIgnoreCase);
                bool allPassed = true;
                foreach (var master in (ReadMasters?.Invoke(ctx.Batch.Id) ?? ctx.ReadMasterResults()).Where(m => m.ImgFileType == ViewResultAlgType.MTF && m.version == "2.0"))
                {
                    var details = ReadDetails?.Invoke(master.Id) ?? ctx.ReadDetails(DeatilCommonDao.Instance, master.Id);
                    if (details.Count != 1)
                        continue;

                    string? path = JsonConvert.DeserializeObject<ResultFile>(details[0].ResultJson)?.ResultFileName;
                    if (!File.Exists(path))
                        continue;

                    var document = JObject.Parse(File.ReadAllText(path));
                    var groupData = document.GetValue(nameof(MTFResult.resultChild), StringComparison.OrdinalIgnoreCase);
                    if (groupData == null || groupData.Type == JTokenType.Null)
                        continue;
                    if (groupData is not JArray groups)
                        throw new InvalidDataException($"MTF 结果 ID {master.Id} 的 resultChild 不是分组列表。");

                    var correction = new MtfRecipeCorrection(master.Params);
                    bool hasMeasuredGroup = false;
                    foreach (var entry in groups)
                    {
                        if (entry is not JObject group)
                            throw new InvalidDataException($"MTF 结果 ID {master.Id} 包含无效分组。");
                        string? name = group.GetValue(nameof(ResultChildItem.name), StringComparison.OrdinalIgnoreCase)?.Value<string>();
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        double horizontal = ReadAverage(group, nameof(ResultChildItem.horizontalAverage), master.Id, name);
                        double vertical = ReadAverage(group, nameof(ResultChildItem.verticalAverage), master.Id, name);
                        var hItem = BuildItem("H", name, horizontal, recipe.HRecipe, correction);
                        var vItem = BuildItem("V", name, vertical, recipe.VRecipe, correction);
                        itemMap[hItem.Name] = hItem;
                        itemMap[vItem.Name] = vItem;
                        allPassed &= hItem.TestResult && vItem.TestResult;
                        hasMeasuredGroup = true;
                    }

                    if (hasMeasuredGroup)
                    {
                        testResult.MTFDetailViewReslut = new MTFDetailViewReslut
                        {
                            DetailCommonModel = details[0],
                            ResultFileName = path,
                            MTFResult = document.ToObject<MTFResult>(),
                        };
                    }
                }

                if (itemMap.Count == 0)
                    throw new InvalidDataException($"批次 {ctx.Batch.Id} 没有可判定的 MTF-HV 分组测量。");

                testResult.Items = new ObservableCollection<ObjectiveTestItem>(itemMap.Values);
                string snapshot = JsonConvert.SerializeObject(testResult);
                (ctx.ObjectiveTestResult.DynamicTestResults ??= new())[outputName] = testResult.Items;
                ctx.Result.ViewResultJson = snapshot;
                ctx.Result.Result &= allPassed;
                return Task.FromResult(true);
            }
            catch (Exception error)
            {
                string message = $"MTF-HV 解析失败 [{outputName}]：{error.Message}";
                ctx.Log.Error(message, error);
                Reject(ctx, outputName, message);
                return Task.FromResult(false);
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, GetOutputName(), string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "MTF-HV 流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private static void Reject(IProcessExecutionContext ctx, string outputName, string message)
        {
            ctx.Result.Result = false;
            ctx.Result.Msg = message;
            ctx.Result.ViewResultJson = string.Empty;
            ctx.ObjectiveTestResult.TotalResult = false;
            ctx.ObjectiveTestResult.DynamicTestResults?.Remove(outputName);
        }

        private static double ReadAverage(JObject group, string field, int resultId, string name)
        {
            var token = group.GetValue(field, StringComparison.OrdinalIgnoreCase);
            if (token?.Type is JTokenType.Float or JTokenType.Integer)
            {
                double value = token.Value<double>();
                if (double.IsFinite(value)) return value;
            }
            else if (token?.Type == JTokenType.String && double.TryParse(token.Value<string>(), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out double parsed) && double.IsFinite(parsed))
            {
                return parsed;
            }

            throw new InvalidDataException($"MTF 结果 ID {resultId} 的分组 [{name}] 缺少有限的实测字段 {field}。");
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<MTFHVDynamicTestResult>(result, string.IsNullOrWhiteSpace(Config.Name) ? "MTFHV" : Config.Name.Trim());

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            MTFHVDynamicViewTestResult? testResult = JsonConvert.DeserializeObject<MTFHVDynamicViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult?.MTFDetailViewReslut?.MTFResult?.result == null)
                return;

            int id = 0;
            foreach (var item in testResult.MTFDetailViewReslut.MTFResult.result)
            {
                id++;
                if (!ProcessExtensions.TryCreateMtfOverlay(item, id, Config.ShowConfig, out DVRectangleText rectangle))
                    continue;

                ctx.ImageView.AddVisual(rectangle);
            }
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{GetOutputName()} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            MTFHVDynamicTestResult? testResult = JsonConvert.DeserializeObject<MTFHVDynamicTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");
            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return;
        }

        private ObjectiveTestItem BuildItem(string axis, string pointName, double value, RecipeBase recipe, MtfRecipeCorrection correction)
        {
            string itemName = $"MTF_HV_{axis}_{NormalizePointName(pointName)}";
            double fixedValue = correction.Apply(value, recipe, itemName);
            return new ObjectiveTestItem
            {
                Name = itemName,
                Unit = Config.Unit,
                Value = fixedValue,
                TestValue = fixedValue.ToString(Config.ShowConfig),
                LowLimit = recipe.Min,
                UpLimit = recipe.Max
            };
        }

        private string GetOutputName()
        {
            return string.IsNullOrWhiteSpace(Config.Name) ? "MTFHV" : Config.Name.Trim();
        }

        private static string NormalizePointName(string pointName)
        {
            string name = pointName.Trim();
            const string marker = "_MTF_HV_";
            int markerIndex = name.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex > 0 && markerIndex + marker.Length < name.Length)
            {
                string frequency = name[..markerIndex];
                string position = name[(markerIndex + marker.Length)..];
                name = $"{position}_{frequency}";
            }

            return name.Replace('.', '_');
        }
    }
}
