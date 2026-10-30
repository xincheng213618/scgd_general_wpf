using ColorVision.Database;
using ColorVision.Common.NativeMethods;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectARVRPro.Process.KeyedResults;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProjectARVRPro.Process.MTF.VRPartition;

public sealed class VRMtfPartitionProcess : ProcessWithRecipeBase<VRMtfPartitionProcessConfig, VRMtfPartitionRecipeConfig>
{
    public override Task<bool> Execute(IProcessExecutionContext ctx)
    {
        if (ctx?.ObjectiveTestResult == null || ctx.Result == null) return Task.FromResult(false);
        string key = Config.GetOutputKey();
        ctx.ObjectiveTestResult.DynamicTestResults ??= new();
        foreach (string existingKey in ctx.ObjectiveTestResult.DynamicTestResults.Keys
            .Where(existingKey => string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase)).ToArray())
            ctx.ObjectiveTestResult.DynamicTestResults.Remove(existingKey);
        ctx.Result.ViewResultJson = string.Empty;
        if (ctx.Batch == null)
        {
            ctx.Result.Result = false;
            ctx.Result.Msg = "VR分区缺少测量批次。";
            return Task.FromResult(false);
        }

        try
        {
            var images = ctx.GetMeasureResults();
            if (images.Count > 0) ctx.Result.FileName = images[0].FileUrl;
            var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id)
                .Where(master => master.ImgFileType == ViewResultAlgType.MTF && master.version == "2.0"
                    && (string.IsNullOrWhiteSpace(Config.ResultName)
                        || string.Equals(master.TName, Config.ResultName.Trim(), StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (masters.Count != 1)
                throw new ArgumentException($"VR分区需要一份MTF 2.0结果，实际匹配{masters.Count}份，请配置MTF结果名称。");
            var details = DeatilCommonDao.Instance.GetAllByPid(masters[0].Id);
            if (details.Count != 1)
                throw new ArgumentException($"VR分区结果详情数量应为1，实际为{details.Count}。");

            var detail = new MTFDetailViewReslut(details[0]);
            VRMtfPartitionViewTestResult result = VRMtfPartitionCalculator.Calculate(detail.MTFResult?.result, Config);
            result.MTFDetailViewReslut = detail;
            ctx.Result.ViewResultJson = JsonConvert.SerializeObject(result);
            KeyedTestResultDictionary.Set(ctx.ObjectiveTestResult.DynamicTestResults, key, result.Items);
            ctx.Result.Result &= result.Items.Skip(result.PointCount).All(item => item.TestResult);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            ctx.Result.Result = false;
            ctx.Result.Msg = $"VR分区解析失败: {ex.Message}";
            ctx.Log?.Error(ctx.Result.Msg, ex);
            return Task.FromResult(false);
        }
    }

    public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
        GetObjectiveCsvRows<VRMtfPartitionTestResult>(result, Config.GetOutputKey());

    public override void Render(IProcessExecutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
        var result = JsonConvert.DeserializeObject<VRMtfPartitionViewTestResult>(ctx.Result.ViewResultJson);
        if (result?.MTFDetailViewReslut?.MTFResult?.result == null) return;
        int index = 0;
        foreach (MTFItem point in result.MTFDetailViewReslut.MTFResult.result.OrderBy(point => point.name, Comparer<string>.Create(Shlwapi.CompareLogical)))
        {
            if (ProcessExtensions.TryCreateMtfOverlay(point, ++index, Config.ShowConfig, out DVRectangleText rectangle))
            {
                int region = index <= result.PointRegions.Count ? result.PointRegions[index - 1] : -1;
                rectangle.Pen = new Pen(GetRegionBrush(region), 1);
                ctx.ImageView.AddVisual(rectangle);
            }
        }
    }

    internal static Brush GetRegionBrush(int region) => region switch
    {
        0 => Brushes.Gray,
        1 => Brushes.Green,
        2 => Brushes.Blue,
        3 => Brushes.Yellow,
        _ => Brushes.Red
    };

    public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
    {
        var output = new StringBuilder().AppendLine($"{Config.GetOutputKey()} VR MTF分区");
        if (!string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
        {
            var result = JsonConvert.DeserializeObject<VRMtfPartitionTestResult>(ctx.Result.ViewResultJson);
            if (result != null)
            {
                output.AppendLine($"原始点数: {result.PointCount}");
                foreach (var region in result.RegionPointCounts)
                    output.AppendLine($"{region.Key}区点数: {region.Value}");
                foreach (ObjectiveTestItem item in result.Items.Skip(result.PointCount))
                    output.AppendLine($"{item.Name}: {item.TestValue} {item.Unit}, {item.LowLimit} ~ {item.UpLimit}, {(item.TestResult ? "PASS" : "Fail")}");
            }
        }
        AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
    }
}
