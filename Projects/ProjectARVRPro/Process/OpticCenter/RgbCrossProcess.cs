using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectARVRPro.Process.OpticCenter;

public sealed class RgbCrossRecipeConfig : ViewModelBase, IRecipeConfig
{
    [DisplayName("在本项目判定")]
    [Description("关闭时仅保存测量与有效性；此选项是项目业务配置，不授予对外 SDK 判定权限。")]
    public bool EnableJudgment { get => enableJudgment; set { enableJudgment = value; OnPropertyChanged(); } }
    private bool enableJudgment;

    [Category("十字 RGB 分离"), DisplayName("最大边缘分离 (px)")]
    [Description("各点 R–G、B–G 分别按原值 × K + B 后判定；下限或上限为 0 时不限制该侧，与其他 Recipe 一致。无效点不能通过。")]
    [PropertyEditorType(typeof(RecipeBasePropertiesEditor))]
    public RecipeBase EdgeSeparation { get => edgeSeparation; set { edgeSeparation = value; OnPropertyChanged(); } }
    private RecipeBase edgeSeparation = new(0, 0);

}

public sealed class RgbCrossProcessConfig : ProcessConfigBase<RgbCrossRecipeConfig>
{
    [Category("导出配置"), DisplayName("导出名称")]
    [Description("CSV 分组及 DynamicRgbCrossResults 字典的 Key；不同画面使用不同名称，留空使用 RgbCross。")]
    public string Name { get => name; set { name = value ?? ""; OnPropertyChanged(); } }
    private string name = "RgbCross";

    [Category("显示配置")]
    [DisplayName("绘制RGB边缘")]
    public bool DrawRgbEdges { get => drawEdges; set { drawEdges = value; OnPropertyChanged(); } }
    private bool drawEdges = true;

    [Category("显示配置")]
    [DisplayName("绘制点号和数值")]
    public bool DrawPointLabels { get => drawLabels; set { drawLabels = value; OnPropertyChanged(); } }
    private bool drawLabels = true;
}

public sealed class RgbCrossProcess : ProcessWithRecipeBase<RgbCrossProcessConfig, RgbCrossRecipeConfig>
{
    private static readonly ConditionalWeakTable<ImageView, List<DrawingVisual>> Overlays = new();
    private readonly IRgbCrossBatchResults batchResults;
    public RgbCrossProcess() : this(new RgbCrossBatchResults()) { }
    internal RgbCrossProcess(IRgbCrossBatchResults batchResults) => this.batchResults = batchResults;


    public override async Task<bool> Execute(IProcessExecutionContext ctx)
    {
        if (ctx?.Result == null || ctx.ObjectiveTestResult == null) return false;
        ctx.ObjectiveTestResult.DynamicRgbCrossResults ??= new();
        string outputName = string.IsNullOrWhiteSpace(Config.Name) ? "RgbCross" : Config.Name.Trim();
        var result = new RgbCrossViewResult { ExportName = outputName };
        try
        {
            RecipeBase? judgment = Config.RecipeConfig.EnableJudgment ? Config.RecipeConfig.EdgeSeparation
                ?? throw new InvalidDataException("启用判定时必须配置 Recipe。") : null;
            RgbCrossBatchInput batchInput = batchResults.Resolve(ctx.Batch?.Id ?? 0);
            string path = batchInput.JsonFile;
            string imagePath = batchInput.ImageFile;
            byte[] bytes;
            await using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length > RgbCrossResultParser.MaximumJsonBytes) throw new InvalidDataException("结果 JSON 超过 32 MiB。");
                bytes = new byte[checked((int)stream.Length)];
                await stream.ReadExactlyAsync(bytes);
            }
            result = RgbCrossResultParser.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
            result.ExportName = outputName;
            result.SourceMasterId = batchInput.MasterId;
            result.JsonFile = path; result.JsonSha256 = Convert.ToHexString(SHA256.HashData(bytes));
            result.SourceImageFile = imagePath;
            if (imagePath.Length == 0 && !result.ImageWidth.HasValue) throw new InvalidDataException("此 JSON 不含原图尺寸，且当前批次结果未关联原图。");
            if (imagePath.Length > 0)
            {
                await using FileStream source = File.OpenRead(imagePath);
                if (result.SourceSha256 != null && !string.Equals(result.SourceSha256, Convert.ToHexString(await SHA256.HashDataAsync(source)), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("原图 SHA-256 与测量来源不一致。");
            }
            RgbCrossResultParser.Evaluate(result, judgment);
            ctx.Result.FileName = imagePath.Length == 0 ? null : imagePath;
            ctx.Result.ImageWidth = result.ImageWidth; ctx.Result.ImageHeight = result.ImageHeight;
            ctx.Result.Msg = "Completed";
            if (judgment != null && (result.Status is "INVALID" or "FAIL")) { ctx.Result.Result = false; ctx.ObjectiveTestResult.TotalResult = false; }
            ctx.Result.ViewResultJson = JsonConvert.SerializeObject(result);
            ctx.ObjectiveTestResult.DynamicRgbCrossResults[outputName] = result;
            return true;
        }
        catch (Exception ex)
        {
            ctx.Log.Error("十字 RGB 解析失败", ex);
            result.Status = "DATA_ERROR"; result.Error = ex.Message; result.Points.Clear();
            ctx.Result.ViewResultJson = JsonConvert.SerializeObject(result);
            ctx.ObjectiveTestResult.DynamicRgbCrossResults[outputName] = result;
            ctx.Result.FileName = null; ctx.Result.ImageWidth = null; ctx.Result.ImageHeight = null;
            ctx.Result.Result = false; ctx.Result.Msg = ex.Message; ctx.ObjectiveTestResult.TotalResult = false;
            return false;
        }
    }

    public override void Render(IProcessExecutionContext ctx)
    {
        if (ctx.ImageView == null) return;
        List<DrawingVisual> previous = Overlays.GetOrCreateValue(ctx.ImageView);
        foreach (var visual in previous) ctx.ImageView.ImageShow.RemoveVisual(visual);
        previous.Clear();
        var result = ReadSaved(ctx.Result);
        if (result == null || result.Status == "DATA_ERROR") return;
        BitmapSource? bitmap = ctx.ImageView.ViewBitmapSource as BitmapSource;
        if (bitmap != null && result.ImageWidth.HasValue && (bitmap.PixelWidth != result.ImageWidth || bitmap.PixelHeight != result.ImageHeight))
        { ctx.Log.Warn("十字原图尺寸不匹配，跳过叠图。"); return; }
        double width = bitmap?.PixelWidth ?? result.ImageWidth ?? 0, height = bitmap?.PixelHeight ?? result.ImageHeight ?? 0;
        if (width <= 0 || height <= 0) return;
        var bounds = new RgbCrossRectangle(0, 0, width, height);
        if (result.Points.Any(p => p.Region != null && !RgbCrossResultParser.Inside(p.Region, bounds)))
        { ctx.Log.Warn("十字坐标超出底图，跳过叠图。"); return; }
        foreach (var p in result.Points)
        {
            if (Config.DrawRgbEdges)
                foreach (var c in p.Channels)
                {
                    Brush color = c.Name == "R" ? Brushes.Red : c.Name == "G" ? Brushes.LimeGreen : Brushes.DodgerBlue;
                    foreach (var rect in new[] { c.Horizontal, c.Vertical })
                        Add(new DVRectangle(new RectangleProperties { Rect = ToRect(rect), Brush = Brushes.Transparent, Pen = new Pen(color, 1) }));
                }
            if (Config.DrawPointLabels && p.Region is RgbCrossRectangle region)
            {
                Brush color = !p.Valid ? Brushes.Orange : p.Judgment == "FAIL" ? Brushes.Red : p.Judgment == "PASS" ? Brushes.LimeGreen : Brushes.DeepSkyBlue;
                string value = PairText(p, "\n");
                Add(new DVRectangleText(new RectangleTextProperties { Rect = ToRect(region), Brush = Brushes.Transparent, Pen = new Pen(color, 1), Msg = $"{p.Id} {value}" }));
            }
        }
        void Add(DrawingVisual visual) { ctx.ImageView.AddVisual(visual); previous.Add(visual); }
    }

    private static Rect ToRect(RgbCrossRectangle r) => new(r.X, r.Y, r.Width, r.Height);
    private static RgbCrossViewResult? ReadSaved(ProjectARVRReuslt result)
    {
        var saved = string.IsNullOrWhiteSpace(result.ViewResultJson) ? null : JsonConvert.DeserializeObject<RgbCrossViewResult>(result.ViewResultJson);
        if (saved != null) foreach (var point in saved.Points) RgbCrossResultParser.PopulateComparisons(point);
        return saved;
    }
    private static string PairText(RgbCrossPoint point, string separator = "  ") => string.Join(separator, new[] { "R-G", "B-G" }.Select(key =>
        $"{key} {(point.Comparisons[key].JudgedValue ?? point.Comparisons[key].Value)?.ToString("F3", CultureInfo.InvariantCulture) ?? "—"} px"));

    public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result)
    {
        var saved = ReadSaved(result);
        if (saved == null) return [];
        if (saved.Status == "DATA_ERROR") return [new(saved.ExportName, "DataError", saved.Error, "", "", "", "", "DATA_ERROR")];
        return saved.Points.SelectMany(p => new[] { "R-G", "B-G" }.Select(key =>
        {
            var c = p.Comparisons[key]; var value = c.JudgedValue ?? c.Value;
            return new ObjectiveTestCsvRow(saved.ExportName, p.Id + "_" + key, value?.ToString("F3", CultureInfo.InvariantCulture) ?? "", value?.ToString("R", CultureInfo.InvariantCulture) ?? "", "px",
                saved.AppliedRecipe?.Min.ToString("R", CultureInfo.InvariantCulture) ?? "", saved.AppliedRecipe?.Max.ToString("R", CultureInfo.InvariantCulture) ?? "", c.Judgment);
        })).ToArray();
    }

    public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
    {
        var result = ReadSaved(ctx.Result);
        var text = new StringBuilder("十字 RGB 分离\n");
        if (result != null)
        {
            text.AppendLine(result.Status == "DATA_ERROR" ? "数据读取失败" : result.AppliedRecipe == null ? "完成" : result.Status);
            if (result.Error.Length > 0) text.AppendLine(result.Error);
            foreach (var p in result.Points) text.AppendLine($"{p.Id}: {PairText(p)}{(result.AppliedRecipe == null ? "" : "  " + p.Judgment)}");
        }
        AppendPlainText(paragraph, text.ToString(), foreground, fontSize);
    }
}
