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
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Nodes;

internal sealed record LocalTemplateMatchingNodeResult(string TemplateName, string TemplateFingerprint, string FrameId,
    string? ImageFilePath, TemplateMatchingOptions Options, TemplateMatchingResult Result, long ElapsedMilliseconds);

[STNode("Flow_CustomNodes", "本地模板匹配", CategoryOrder = 9900)]
public sealed class LocalTemplateMatchingNode : LocalFlowNodeBase
{
    private readonly ILocalTemplateMatchingServices services;
    private string parameterJson = string.Empty;
    private string imageFilePath = string.Empty;
    private Int32Rect searchRegion = Int32Rect.Empty;

    [Category("本地模板匹配"), PropertyEditorType(typeof(LocalMatchingTemplatePropertiesEditor))]
    [STNodeProperty("匹配配置", "参考图与参数保存在节点内，随流程保存；可单独导入或导出 .cvtm。", true)]
    public string ParameterJson { get => parameterJson; set { parameterJson = value ?? string.Empty; OnPropertyChanged(); } }

    [Category("本地模板匹配"), PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
    [STNodeProperty("图像文件", "可选后备输入；优先使用上游内存帧，无内存帧时读取此文件。", true)]
    public string ImageFilePath { get => imageFilePath; set { imageFilePath = value ?? string.Empty; OnPropertyChanged(); } }

    [Category("本地模板匹配")]
    [STNodeProperty("搜索区域", "像素 ROI（X,Y,Width,Height），0,0,0,0 表示整图；输出为整图坐标。", true, DescriptorType = typeof(Int32RectNodePropertyDescriptor))]
    public Int32Rect SearchRegion { get => searchRegion; set { searchRegion = value; OnPropertyChanged(); } }

    public LocalTemplateMatchingNode() : this(LocalTemplateMatchingService.Instance) { }
    internal LocalTemplateMatchingNode(ILocalTemplateMatchingServices services) : base("本地模板匹配", "LocalTemplateMatching", "LocalTemplateMatching") =>
        this.services = services ?? throw new ArgumentNullException(nameof(services));

    protected override string GetCompactSummaryValue() => string.IsNullOrWhiteSpace(ParameterJson) ? "未配置" : "节点内置配置";
    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action) => new() { Data = ExecuteSynchronously(action) };
    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
    {
        ServiceName = NodeName, EventName = OperatorCode, action.SerialNumber, ImageFilePath, SearchRegion
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
        string configuredImage = ImageFilePath;
        LocalFlowFrame frame = ResolveFrame(action, configuredImage, out LocalFlowFrame? ownedFrame);
        try
        {
            using LocalFlowFrameLease lease = frame.Acquire();
            if (!lease.IsFlipApplied) throw new InvalidOperationException("当前图像方向变换尚未完成，无法匹配。");
            RoiRect roi = LocalFindLuminousAreaNode.ResolveRoi(configuredRegion, lease.Metadata.Width, lease.Metadata.Height);
            HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
            Stopwatch stopwatch = Stopwatch.StartNew();
            TemplateMatchingResult result = FlowNodeTiming.Run("Algorithm", () => services.Detect(image, document, roi));
            stopwatch.Stop();
            TemplateMatching.ValidateResult(result, image.cols, image.rows, document.Options);
            if (!result.Success) throw new InvalidOperationException($"模板匹配失败：{result.StatusCode}；{result.Message}");
            if (ownedFrame != null) { action.SetCurrentFrame(ownedFrame); ownedFrame = null; }
            action.Data["LocalTemplateMatchingResult"] = result;
            action.Data["LocalTemplateMatchingMatches"] = result.Matches;
            action.Data["LocalTemplateMatchingBestMatch"] = result.Matches.MaxBy(match => match.Score)!;
            // Pass source identity through; no database master ID or legacy result event is created.
            return new(document.Name, fingerprint, lease.FrameId.ToString("N"), frame.ResolveResultImageFilePath(), document.Options, result, stopwatch.ElapsedMilliseconds);
        }
        finally { ownedFrame?.Dispose(); }
    }

    private LocalFlowFrame ResolveFrame(CVStartCFC action, string configuredFile, out LocalFlowFrame? ownedFrame)
    {
        ownedFrame = null;
        if (action.TryGetCurrentFrame(out LocalFlowFrame? current) && current != null) return current;
        if (string.IsNullOrWhiteSpace(configuredFile)) throw new InvalidOperationException("请连接提供本地内存帧的节点，或选择图像文件；本地模板匹配不查询数据库图像记录。");
        string path = Path.GetFullPath(configuredFile.Trim());
        if (!File.Exists(path)) throw new FileNotFoundException("匹配输入图像文件不存在。", path);
        ownedFrame = services.LoadFrame(path);
        return ownedFrame;
    }
}
