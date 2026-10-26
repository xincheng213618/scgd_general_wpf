using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Nodes;

internal sealed record LocalTemplateMatchingNodeResult(string TemplateName, string TemplateFingerprint, string FrameId,
    string? ImageFilePath, TemplateMatchingOptions Options, TemplateMatchingResult Result, long ElapsedMilliseconds);

[STNode("Flow_LocalAlgorithms", "模板匹配", CategoryOrder = 9910, NodeOrder = 70)]
[FlowEngineLib.FlowNodeDocumentation("使用节点内的参考图，在输入图像中搜索旋转、缩放后的相似目标，返回中心、角度、尺度、分数和四角。",
    Usage = "在匹配配置中设置参考图与参数并应用到节点，连接提供本地内存图像的上游节点；需要时设置搜索区域。",
    Notes = "无匹配或无效配置按节点失败处理；分数不是命中概率。匹配不创建 AOI 数据库结果，源图继续交给下游。",
    TopicId = "algorithms.matching")]
public sealed class LocalTemplateMatchingNode : LocalFlowNodeBase
{
    private readonly ILocalTemplateMatchingServices services;
    private string parameterJson = string.Empty;
    private Int32Rect searchRegion = Int32Rect.Empty;

    [Category("本地模板匹配"), PropertyEditorType(typeof(LocalMatchingTemplatePropertiesEditor))]
    [STNodeProperty("匹配配置", "参考图与参数保存在节点内，随流程保存；可单独导入或导出 .cvtm。", true)]
    public string ParameterJson { get => parameterJson; set { parameterJson = value ?? string.Empty; OnPropertyChanged(); } }

    [Category("本地模板匹配")]
    [STNodeProperty("搜索区域", "像素 ROI（X,Y,Width,Height），0,0,0,0 表示整图；输出为整图坐标。", true, DescriptorType = typeof(Int32RectNodePropertyDescriptor))]
    public Int32Rect SearchRegion { get => searchRegion; set { searchRegion = value; OnPropertyChanged(); } }

    public LocalTemplateMatchingNode() : this(LocalTemplateMatchingService.Instance) { }
    internal LocalTemplateMatchingNode(ILocalTemplateMatchingServices services) : base("模板匹配", "LocalTemplateMatching", "LocalTemplateMatching") =>
        this.services = services ?? throw new ArgumentNullException(nameof(services));

    protected override string GetCompactSummaryValue() => string.IsNullOrWhiteSpace(ParameterJson) ? "未配置" : "节点内置配置";
    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action) => new() { Data = ExecuteSynchronously(action) };
    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
    {
        ServiceName = NodeName, EventName = OperatorCode, action.SerialNumber, SearchRegion
    });

    internal LocalTemplateMatchingNodeResult ExecuteSynchronously(CVStartCFC action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action.Data.Remove("LocalTemplateMatchingResult");
        action.Data.Remove("LocalTemplateMatchingMatches");
        action.Data.Remove("LocalTemplateMatchingBestMatch");
        if (string.IsNullOrWhiteSpace(ParameterJson)) throw new InvalidOperationException("请先编辑节点的匹配配置，设置参考图与参数。");
        // Parsing immutable node JSON isolates this invocation from later edits.
        TemplateMatchingDocument document = TemplateMatchingDocument.FromJson(ParameterJson);
        string fingerprint = document.Fingerprint();
        Int32Rect configuredRegion = SearchRegion;
        if (!action.TryGetCurrentFrame(out LocalFlowFrame? frame) || frame == null)
            throw new InvalidOperationException("请连接提供本地内存图像的相机取图、校正或“加载图片”节点；本地模板匹配不查询数据库图像记录。");
        using LocalFlowFrameLease lease = frame.Acquire();
        if (!lease.IsFlipApplied) throw new InvalidOperationException("当前图像方向变换尚未完成，无法匹配。");
        RoiRect roi = LocalFindLuminousAreaNode.ResolveRoi(configuredRegion, lease.Metadata.Width, lease.Metadata.Height);
        HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
        Stopwatch stopwatch = Stopwatch.StartNew();
        TemplateMatchingResult result = FlowNodeTiming.Run("Algorithm", () => services.Detect(image, document, roi));
        stopwatch.Stop();
        TemplateMatching.ValidateResult(result, image.cols, image.rows, document.Options);
        if (!result.Success) throw new InvalidOperationException($"模板匹配失败：{result.StatusCode}；{result.Message}");
        action.Data["LocalTemplateMatchingResult"] = result;
        action.Data["LocalTemplateMatchingMatches"] = result.Matches;
        action.Data["LocalTemplateMatchingBestMatch"] = result.Matches.MaxBy(match => match.Score)!;
        // Pass source identity through; no database master ID or legacy result event is created.
        return new(document.Name, fingerprint, lease.FrameId.ToString("N"), frame.ResolveResultImageFilePath(), document.Options, result, stopwatch.ElapsedMilliseconds);
    }
}
