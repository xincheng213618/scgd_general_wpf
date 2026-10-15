using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.RingGhost;
using ColorVision.ImageEditor.Algorithms;
using Newtonsoft.Json;
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Recipe;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Ghost;

public sealed class RingGhostRecipeConfig : ViewModelBase, IRecipeConfig
{
    [Category("鬼影"), DisplayName("GMR1 (%)")]
    [Description("一级鬼影亮度比，默认上限 1%。")]
    public RecipeBase GMR1 { get => gmr1; set { gmr1 = value; OnPropertyChanged(); } }
    private RecipeBase gmr1 = new(0, 1);
    [Category("鬼影"), DisplayName("GMR2 (%)")]
    [Description("二级鬼影亮度比，默认上限 0.5%。正常未检出的 0 不应用 K/B 修正；测量异常不能用 0 代替。")]
    public RecipeBase GMR2 { get => gmr2; set { gmr2 = value; OnPropertyChanged(); } }
    private RecipeBase gmr2 = new(0, .5);
}

public sealed class RingGhostProcessConfig : ProcessConfigBase<RingGhostRecipeConfig>
{
    [Category("导出配置"), DisplayName("输出 Key")]
    public string OutputKey { get => outputKey; set { outputKey = value ?? ""; OnPropertyChanged(); } }
    private string outputKey = "RingGhost";
    [Category("显示配置"), DisplayName("绘制测量区域")]
    public bool DrawRegions { get => drawRegions; set { drawRegions = value; OnPropertyChanged(); } }
    private bool drawRegions = true;
}

/// <summary>The customer model contains exactly two measurement parameters.</summary>
public class RingGhostTestResult
{
    public ObjectiveTestItem? GMR1 { get; set; }
    public ObjectiveTestItem? GMR2 { get; set; }
}

public sealed class RingGhostViewTestResult : RingGhostTestResult
{
    public string OutputKey { get; set; } = "RingGhost";
    public string Status { get; set; } = "DATA_ERROR";
    public string Error { get; set; } = "";
    public int SourceMasterId { get; set; }
    public LocalRingGhostResult? Measurement { get; set; }
}

internal sealed record RingGhostBatchInput(string JsonFile, string ImageFile, int MasterId);
internal interface IRingGhostBatchResults { RingGhostBatchInput Resolve(int batchId); }
internal sealed class RingGhostBatchResults : IRingGhostBatchResults
{
    public RingGhostBatchInput Resolve(int batchId) => Select(AlgResultMasterDao.Instance.GetAllByBatchId(batchId), batchId,
        id => DeatilCommonDao.Instance.GetAllByPid(id));
    internal static RingGhostBatchInput Select(IEnumerable<AlgResultMasterModel> masters, int batchId, Func<int, List<DetailCommonModel>> loadDetails)
    {
        if (batchId <= 0) throw new InvalidDataException("读取鬼影需要有效批次。");
        var matches = masters.Where(m => m.BatchId == batchId && m.ImgFileType == ViewResultAlgType.Ghost
            && m.version == LocalRingGhostResult.ResultVersion).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("当前批次必须有且仅有一条本地圆环鬼影结果。");
        var master = matches[0];
        if (master.Id <= 0 || master.ResultCode != 0) throw new InvalidDataException("鬼影计算失败或状态未知。");
        var details = loadDetails(master.Id);
        if (details.Count != 1 || details[0].PId != master.Id) throw new InvalidDataException("鬼影结果需要一条对应的文件路径明细。");
        var file = JsonConvert.DeserializeObject<ResultFile>(details[0].ResultJson)?.ResultFileName;
        if (string.IsNullOrWhiteSpace(file) || !Path.IsPathFullyQualified(file)) throw new InvalidDataException("鬼影结果缺少绝对路径。");
        return new(Path.GetFullPath(file), master.ImgFile ?? "", master.Id);
    }
}

public sealed class RingGhostProcess : ProcessWithRecipeBase<RingGhostProcessConfig, RingGhostRecipeConfig>
{
    private readonly IRingGhostBatchResults results;
    public RingGhostProcess() : this(new RingGhostBatchResults()) { }
    internal RingGhostProcess(IRingGhostBatchResults results) => this.results = results;

    public override Task<bool> Execute(IProcessExecutionContext ctx)
    {
        if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null) return Task.FromResult(false);
        var saved = new RingGhostViewTestResult { OutputKey = KeyedTestResultDictionary.NormalizeKey(Config.OutputKey, "RingGhost") };
        try
        {
            var input = results.Resolve(ctx.Batch.Id);
            var measurement = LocalRingGhostResult.Read(input.JsonFile);
            saved.Measurement = measurement; saved.SourceMasterId = input.MasterId;
            saved.GMR1 = CreateItem("GMR1", measurement.GMR1, Config.RecipeConfig.GMR1);
            saved.GMR2 = CreateItem("GMR2", measurement.GMR2, Config.RecipeConfig.GMR2);
            bool pass = saved.GMR1.TestResult && saved.GMR2.TestResult;
            saved.Status = pass ? "PASS" : "FAIL";
            ctx.Result.Result &= pass; ctx.ObjectiveTestResult.TotalResult &= pass;
            ctx.Result.FileName = string.IsNullOrWhiteSpace(input.ImageFile) ? null : input.ImageFile;
            ctx.Result.ImageWidth = measurement.ImageWidth; ctx.Result.ImageHeight = measurement.ImageHeight;
            ctx.Result.Msg = measurement.MeasurementStatus;
            Save(ctx, saved);
            return Task.FromResult(true);
        }
        catch (Exception error)
        {
            ctx.Log.Error("鬼影解析失败", error);
            Fail(ctx, saved, error.Message);
            return Task.FromResult(false);
        }
    }

    public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
    {
        if (ctx?.Result == null || ctx.ObjectiveTestResult == null) return Task.FromResult(false);
        Fail(ctx, new() { OutputKey = KeyedTestResultDictionary.NormalizeKey(Config.OutputKey, "RingGhost") },
            string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "鬼影流程未成功完成。" : ctx.Result.Msg);
        return Task.FromResult(false);
    }

    private static ObjectiveTestItem CreateItem(string name, double value, RecipeBase recipe)
    {
        if (recipe == null || !double.IsFinite(recipe.Min) || !double.IsFinite(recipe.Max) || !double.IsFinite(recipe.Fix)
            || !double.IsFinite(recipe.B) || recipe.Min < 0 || recipe.Max < 0
            || recipe.Min != 0 && recipe.Max != 0 && recipe.Min > recipe.Max)
            throw new InvalidDataException($"{name} 配方无效。");
        double corrected = value == 0 ? 0 : recipe.Apply(value);
        if (!double.IsFinite(corrected) || corrected < 0) throw new InvalidDataException($"{name} 修正后数值无效。");
        return new() { Name = name, Unit = "%", Value = corrected, TestValue = corrected.ToString("F4", CultureInfo.InvariantCulture),
            LowLimit = recipe.Min, UpLimit = recipe.Max };
    }

    private static void Save(IProcessExecutionContext ctx, RingGhostViewTestResult saved)
    {
        ctx.Result.ViewResultJson = JsonConvert.SerializeObject(saved);
        ctx.ObjectiveTestResult.RingGhostTestResults ??= new();
        KeyedTestResultWriter.Write(ctx.ObjectiveTestResult.RingGhostTestResults, saved.OutputKey,
            new RingGhostTestResult { GMR1 = saved.GMR1, GMR2 = saved.GMR2 }, "RingGhost");
    }

    private static void Fail(IProcessExecutionContext ctx, RingGhostViewTestResult saved, string message)
    {
        saved.Status = "DATA_ERROR"; saved.Error = message;
        saved.GMR1 = null; saved.GMR2 = null; saved.Measurement = null;
        ctx.Result.Result = false; ctx.ObjectiveTestResult.TotalResult = false;
        ctx.Result.FileName = null; ctx.Result.ImageWidth = null; ctx.Result.ImageHeight = null;
        ctx.Result.Msg = message;
        Save(ctx, saved);
    }

    private static RingGhostViewTestResult? ReadSaved(ProjectARVRReuslt result) => string.IsNullOrWhiteSpace(result?.ViewResultJson)
        ? null : JsonConvert.DeserializeObject<RingGhostViewTestResult>(result.ViewResultJson);

    public override void Render(IProcessExecutionContext ctx)
    {
        if (ctx.ImageView == null) return;
        var saved = ReadSaved(ctx.Result);
        var measurement = saved?.Measurement;
        if (measurement != null) measurement.Validate();
        RingGhostOverlayRenderer.Render(ctx.ImageView, measurement?.ImageWidth ?? 0, measurement?.ImageHeight ?? 0,
            Config.DrawRegions && saved?.Status != "DATA_ERROR" ? measurement?.Regions ?? [] : []);
    }

    public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result)
    {
        var saved = ReadSaved(result);
        if (saved == null) return [];
        return new[] { ("GMR1", saved.GMR1), ("GMR2", saved.GMR2) }.Select(pair => new ObjectiveTestCsvRow(
            saved.OutputKey, pair.Item1, pair.Item2?.TestValue ?? "", pair.Item2?.Value.ToString("R", CultureInfo.InvariantCulture) ?? "", "%",
            pair.Item2?.LowLimit.ToString("R", CultureInfo.InvariantCulture) ?? "", pair.Item2?.UpLimit.ToString("R", CultureInfo.InvariantCulture) ?? "",
            pair.Item2 == null ? "DATA_ERROR" : pair.Item2.TestResult ? "PASS" : "FAIL")).ToArray();
    }

    public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
    {
        var saved = ReadSaved(ctx.Result);
        var text = new StringBuilder("圆环鬼影\n");
        if (saved != null)
        {
            text.AppendLine($"GMR1: {saved.GMR1?.TestValue ?? "—"}%");
            text.AppendLine($"GMR2: {saved.GMR2?.TestValue ?? "—"}%");
            text.AppendLine(saved.Status);
            if (saved.Measurement?.CandidateCount < 2) text.AppendLine("正常未检出项按 0 输出。");
            if (saved.Error.Length > 0) text.AppendLine(saved.Error);
        }
        AppendPlainText(paragraph, text.ToString(), foreground, fontSize);
    }
}
