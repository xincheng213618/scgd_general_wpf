using ColorVision.Common.MVVM;
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

    [DisplayName("最大边缘分离上限(px)")]
    [Description("启用判定时必填，非负有限数值。九点全部有效且各点均不超过此值才通过。")]
    public double? MaximumEdgeSeparationPixels { get => maximum; set { maximum = value; OnPropertyChanged(); } }
    private double? maximum;
}

public sealed class RgbCrossProcessConfig : ProcessConfigBase<RgbCrossRecipeConfig>
{
    [Category("输入")]
    [DisplayName("结果JSON文件")]
    [Description("留空按当前批次从 FindCross 2.0 记录读取；填写路径则进行离线复核，可包含 {BatchId}。")]
    public string ResultJsonFile { get => jsonFile; set { jsonFile = value ?? ""; OnPropertyChanged(); } }
    private string jsonFile = "";

    [Category("输入"), DisplayName("FindCross 模板名称")]
    [Description("按批次读取时可指定模板。留空要求当前批次只有一条九点结果；不自动选择最新文件。")]
    public string FindCrossTemplateName { get => templateName; set { templateName = value ?? ""; OnPropertyChanged(); } }
    private string templateName = "";


    [Category("输入")]
    [DisplayName("对应原图文件")]
    [Description("显式绑定与 JSON 对应的原图，可包含 {BatchId}。留空时使用批次记录的原图；离线模式无原图时按 JSON 尺寸绘制，旧导出无尺寸时需指定原图。")]
    public string SourceImageFile { get => imageFile; set { imageFile = value ?? ""; OnPropertyChanged(); } }
    private string imageFile = "";

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
        var result = new RgbCrossViewResult();
        try
        {
            double? limit = Config.RecipeConfig.EnableJudgment ? Config.RecipeConfig.MaximumEdgeSeparationPixels
                ?? throw new InvalidDataException("启用分离判定时必须配置上限。") : null;
            if (limit.HasValue && (!double.IsFinite(limit.Value) || limit.Value < 0)) throw new InvalidDataException("分离上限必须为非负有限数值。");
            RgbCrossBatchInput? batchInput = string.IsNullOrWhiteSpace(Config.ResultJsonFile)
                ? batchResults.Resolve(ctx.Batch?.Id ?? 0, Config.FindCrossTemplateName) : null;
            string path = batchInput?.JsonFile ?? ResolvePath(Config.ResultJsonFile, ctx.Batch?.Id ?? 0);
            string imagePath = string.IsNullOrWhiteSpace(Config.SourceImageFile) ? batchInput?.ImageFile ?? "" : ResolvePath(Config.SourceImageFile, ctx.Batch?.Id ?? 0);
            byte[] bytes;
            await using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length > RgbCrossResultParser.MaximumJsonBytes) throw new InvalidDataException("结果 JSON 超过 32 MiB。");
                bytes = new byte[checked((int)stream.Length)];
                await stream.ReadExactlyAsync(bytes);
            }
            result = RgbCrossResultParser.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
            result.SourceMasterId = batchInput?.MasterId;
            result.JsonFile = path; result.JsonSha256 = Convert.ToHexString(SHA256.HashData(bytes));
            result.SourceImageFile = imagePath;
            if (imagePath.Length == 0 && !result.ImageWidth.HasValue) throw new InvalidDataException("此旧版 JSON 不含原图尺寸，请明确指定对应原图文件。");
            if (imagePath.Length > 0)
            {
                await using FileStream source = File.OpenRead(imagePath);
                if (result.SourceSha256 != null && !string.Equals(result.SourceSha256, Convert.ToHexString(await SHA256.HashDataAsync(source)), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("原图 SHA-256 与测量来源不一致。");
            }
            RgbCrossResultParser.Evaluate(result, limit);
            ctx.Result.FileName = imagePath.Length == 0 ? null : imagePath;
            ctx.Result.ImageWidth = result.ImageWidth; ctx.Result.ImageHeight = result.ImageHeight;
            ctx.Result.Msg = result.Status == "MEASURED" ? "九点 RGB 已测量，未判定" : $"九点 RGB：{result.Status}";
            if (result.Status is "INVALID" or "FAIL") { ctx.Result.Result = false; ctx.ObjectiveTestResult.TotalResult = false; }
            ctx.Result.ViewResultJson = JsonConvert.SerializeObject(result);
            return true;
        }
        catch (Exception ex)
        {
            ctx.Log.Error("九点十字 RGB 解析失败", ex);
            result.Status = "DATA_ERROR"; result.Error = ex.Message; result.Points.Clear();
            ctx.Result.ViewResultJson = JsonConvert.SerializeObject(result);
            ctx.Result.FileName = null; ctx.Result.ImageWidth = null; ctx.Result.ImageHeight = null;
            ctx.Result.Result = false; ctx.Result.Msg = ex.Message; ctx.ObjectiveTestResult.TotalResult = false;
            return false;
        }
    }

    internal static string ResolvePath(string configured, int batchId)
    {
        if (string.IsNullOrWhiteSpace(configured)) throw new InvalidDataException("请指定九点十字结果 JSON 文件。");
        if (configured.Contains("{BatchId}", StringComparison.Ordinal) && batchId <= 0) throw new InvalidDataException("缺少有效批次 ID。");
        return Path.GetFullPath(configured.Replace("{BatchId}", batchId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
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
        { ctx.Log.Warn("九点十字原图尺寸不匹配，跳过叠图。"); return; }
        double width = bitmap?.PixelWidth ?? result.ImageWidth ?? 0, height = bitmap?.PixelHeight ?? result.ImageHeight ?? 0;
        if (width <= 0 || height <= 0) return;
        var bounds = new RgbCrossRectangle(0, 0, width, height);
        if (result.Points.Any(p => p.Region != null && !RgbCrossResultParser.Inside(p.Region, bounds)))
        { ctx.Log.Warn("九点十字坐标超出底图，跳过叠图。"); return; }
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
                string value = p.MaximumEdgeSeparation?.ToString("F3", CultureInfo.InvariantCulture) ?? "—";
                Add(new DVRectangleText(new RectangleTextProperties { Rect = ToRect(region), Brush = Brushes.Transparent, Pen = new Pen(color, 1), Msg = $"{p.Id} {value} px {p.Judgment}" }));
            }
        }
        void Add(DrawingVisual visual) { ctx.ImageView.AddVisual(visual); previous.Add(visual); }
    }

    private static Rect ToRect(RgbCrossRectangle r) => new(r.X, r.Y, r.Width, r.Height);
    private static RgbCrossViewResult? ReadSaved(ProjectARVRReuslt result) => string.IsNullOrWhiteSpace(result.ViewResultJson) ? null : JsonConvert.DeserializeObject<RgbCrossViewResult>(result.ViewResultJson);

    public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result)
    {
        var saved = ReadSaved(result);
        if (saved == null) return [];
        if (saved.Status == "DATA_ERROR") return [new("RgbCross", "DataError", saved.Error, "", "", "", "", "DATA_ERROR")];
        return saved.Points.Select(p => new ObjectiveTestCsvRow("RgbCross", p.Id + "_MaximumEdgeSeparation", p.MaximumEdgeSeparation?.ToString("F3", CultureInfo.InvariantCulture) ?? "", p.MaximumEdgeSeparation?.ToString("R", CultureInfo.InvariantCulture) ?? "", "px", "", saved.AppliedLimit?.ToString("R", CultureInfo.InvariantCulture) ?? "", p.Judgment)).ToArray();
    }

    public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
    {
        var result = ReadSaved(ctx.Result);
        var text = new StringBuilder("九点十字 RGB 分离\n");
        if (result != null)
        {
            text.AppendLine(result.Status == "MEASURED" ? "已测量，未判定" : result.Status);
            if (result.Error.Length > 0) text.AppendLine(result.Error);
            foreach (var p in result.Points) text.AppendLine($"{p.Id}: {p.MaximumEdgeSeparation?.ToString("F3", CultureInfo.InvariantCulture) ?? "—"} px  {p.Judgment}  {p.Reason}  {p.Warning}");
        }
        AppendPlainText(paragraph, text.ToString(), foreground, fontSize);
    }
}
