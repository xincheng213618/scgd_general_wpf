using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.Algorithms;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor.Algorithms;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Nodes;

internal sealed record LocalRgbCrossSaveRequest(CVStartCFC Action, JsonElement Measurement,
    string? ImageFile, string Directory, int ZIndex, int TotalTime, object Audit);
internal sealed record LocalRgbCrossSavedResult(int MasterId, string ResultFilePath);
internal interface ILocalRgbCrossNodeServices
{
    string ResolveResultDirectory(string configuredDirectory);
    Int32Rect LoadSearchRegionTemplate(string name);
    LocalFlowFrame LoadFrame(string path);
    MeasureResultImgModel? GetImageResult(int id);
    AlgorithmResult Detect(LocalFlowFrameLease frame, AlgorithmInvocation invocation);
    LocalRgbCrossSavedResult Save(LocalRgbCrossSaveRequest request);
    void Publish(string serialNumber, string nodeId, int zIndex, int masterId);
}

internal sealed class LocalRgbCrossNodeServices : ILocalRgbCrossNodeServices
{
    public string ResolveResultDirectory(string configuredDirectory) => LocalAlgorithmResultDirectory.Resolve(configuredDirectory);
    public Int32Rect LoadSearchRegionTemplate(string name) => LocalPoiSearchRegionResolver.Load(name);
    public LocalFlowFrame LoadFrame(string path) => LocalFrameFileService.Load(path);
    public MeasureResultImgModel? GetImageResult(int id) => MeasureImgResultDao.Instance.GetById(id);
    public AlgorithmResult Detect(LocalFlowFrameLease frame, AlgorithmInvocation invocation) => LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(frame, invocation).AsTask().GetAwaiter().GetResult();
    public LocalRgbCrossSavedResult Save(LocalRgbCrossSaveRequest request) => LocalRgbCrossResultPersistence.Save(request);
    public void Publish(string serialNumber, string nodeId, int zIndex, int masterId) => ResultMessageBus.Default.PublishPersisted(
        ResultRoutes.LocalFlow, ResultKinds.Algorithm, "", "FindCross", serialNumber, nodeId, zIndex, masterId, (int)ViewResultAlgType.FindCross);
}

internal static class LocalRgbCrossResultPersistence
{
    internal const string ResultVersion = "2.0";
    internal static AlgResultMasterModel CreateMaster(LocalRgbCrossSaveRequest request, int batchId) => new()
    {
        TId = null, TName = "LocalRgbCross",
        ImgFileType = ViewResultAlgType.FindCross, version = ResultVersion, BatchId = batchId, Zindex = request.ZIndex,
        ImgFile = request.ImageFile, Params = JsonConvert.SerializeObject(request.Audit), ResultCode = 0,
        Result = request.Measurement.GetProperty("summary").GetProperty("complete").GetBoolean() ? "MEASURED" : "INVALID",
        TotalTime = request.TotalTime, CreateDate = DateTime.Now
    };

    internal static LocalRgbCrossSavedResult Save(LocalRgbCrossSaveRequest request)
    {
        var batch = BatchResultMasterDao.Instance.GetByNameOrCode(request.Action.SerialNumber)
            ?? throw new InvalidOperationException("找不到十字流程批次。");
        string directory = Path.GetFullPath(request.Directory);
        string file = Path.Combine(directory, $"RgbCross_{batch.Id}_{Guid.NewGuid():N}.json");
        RgbCrossMeasurementExporter.Write(request.Measurement, file);
        try
        {
            int id = LocalFindCrossResultPersistence.SaveDatabaseCore(CreateMaster(request, batch.Id), file, static () => new SqlSugarLocalFindCrossResultTransaction());
            return new(id, file);
        }
        catch (Exception error)
        {
            try { File.Delete(file); }
            catch (Exception cleanup) { throw new AggregateException("十字结果入库失败，清理结果文件也失败。", error, cleanup); }
            throw;
        }
    }
}

[STNode("Flow_CustomNodes", "十字 RGB 分离")]
public sealed class LocalRgbCrossNode : LocalFlowNodeBase
{
    private readonly ILocalRgbCrossNodeServices services;
    private string searchTemplate = "", imageFilePath = "", resultDirectory = "";
    private string parameterJson = "{}";
    private Int32Rect searchRegion = Int32Rect.Empty;

    [Category("十字"), PropertyEditorType(typeof(LocalRgbCrossConfigurationEditor))]
    [STNodeProperty("算法参数", "独立的十字检测配置，不使用 FindCross 算法模板。", true)]
    public string ParameterJson { get => parameterJson; set { parameterJson = value ?? "{}"; OnPropertyChanged(); } }

    [Category("搜索区域"), PropertyEditorType(typeof(PoiTemplatePropertiesEditor))]
    [STNodeProperty("搜索区域关注点", "寻找发光区写入的 POI 模板，每次运行读取最新区域，优先于固定搜索区域。", true)]
    public string SearchRegionPoiTemplate { get => searchTemplate; set { searchTemplate = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasSearchRegionPoi)); } }
    [Browsable(false)] public bool HasSearchRegionPoi => !string.IsNullOrWhiteSpace(SearchRegionPoiTemplate);
    [PropertyVisibility(nameof(HasSearchRegionPoi), true)]
    [Category("搜索区域")]
    [STNodeProperty("搜索区域", "固定像素 ROI；0,0,0,0 使用整图。", true, DescriptorType = typeof(Int32RectNodePropertyDescriptor))]
    public Int32Rect SearchRegion { get => searchRegion; set { searchRegion = value; OnPropertyChanged(); } }
    [Category("十字"), PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
    [STNodeProperty("图像文件", "可选后备输入；优先上游内存帧，其次此文件，再从 IN 图像结果读取。", true)]
    public string ImageFilePath { get => imageFilePath; set { imageFilePath = value ?? ""; OnPropertyChanged(); } }
    [Category("十字"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
    [STNodeProperty("结果目录", "留空使用算法服务的数据基础路径/服务标识/Data/当天日期；填写则覆盖默认目录。", true)]
    public string ResultDirectory { get => resultDirectory; set { resultDirectory = value ?? ""; OnPropertyChanged(); } }

    public LocalRgbCrossNode() : this(new LocalRgbCrossNodeServices()) { }
    internal LocalRgbCrossNode(ILocalRgbCrossNodeServices services) : base("十字 RGB 分离", "LocalRgbCross", "FindCross") => this.services = services;
    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action) => new() { Data = ExecuteSynchronously(action) };
    internal LocalRgbCrossSavedResult ExecuteSynchronously(CVStartCFC action)
    {
        if (!RgbCrossConfigurationDraft.TryCreate(ParameterJson, out var configuration, out string error)
            || !configuration!.TryGetParameters(out var parameters, out error)) throw new InvalidOperationException(error);
        string poiTemplate = SearchRegionPoiTemplate.Trim(), directory = services.ResolveResultDirectory(ResultDirectory), file = ImageFilePath, nodeId = NodeID;
        Int32Rect region = SearchRegion; int zIndex = ZIndex;
        LocalFlowFrame? ownedFrame = null;
        LocalFlowFrame frame = ResolveFrame(action, file, out ownedFrame, out string? imageFile);
        try
        {
            using var lease = frame.Acquire();
            if (!lease.IsFlipApplied) throw new InvalidOperationException("当前图像方向变换尚未完成。");
            var roi = LocalFindLuminousAreaNode.ResolveRoi(poiTemplate.Length == 0 ? region : services.LoadSearchRegionTemplate(poiTemplate), lease.Metadata.Width, lease.Metadata.Height);
            var invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.RgbCrossRegistration, parameters,
                roi.Width == 0 ? null : new RectangleAlgorithmRoi(roi.X, roi.Y, roi.Width, roi.Height));
            var timer = Stopwatch.StartNew();
            using AlgorithmResult result = FlowNodeTiming.Run("Algorithm", () => services.Detect(lease, invocation));
            if (result.Status != AlgorithmResultStatus.Succeeded) throw new InvalidOperationException(string.Join(";", result.Failures.Select(f => f.Code + ":" + f.Message)));
            JsonElement measurement = result.Artifacts.OfType<AlgorithmStructuredDataArtifact>().Single(a => a.Schema == RgbCrossMeasurementExporter.SchemaId).Data;
            // The file association is taken only from the frame actually measured, never another batch image.
            var request = new LocalRgbCrossSaveRequest(action, measurement, imageFile, directory, zIndex,
                (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue), new { Algorithm = DisplayMetrologyIds.RgbCrossRegistration.ToString(), FrameId = lease.FrameId,
                    SourceMasterId = lease.MasterId, SearchRegionPoiTemplate = poiTemplate, SearchRegion = roi, Parameters = parameters, JsonSchema = RgbCrossMeasurementExporter.SchemaId });
            LocalRgbCrossSavedResult saved = FlowNodeTiming.Run("PersistResult", () => services.Save(request));
            if (saved.MasterId <= 0 || string.IsNullOrWhiteSpace(saved.ResultFilePath)) throw new InvalidOperationException("十字结果持久化返回无效记录。");
            if (ownedFrame != null) { action.SetCurrentFrame(ownedFrame); ownedFrame = null; }
            action.Data["LocalRgbCrossResultFile"] = saved.ResultFilePath;
            action.Data["LocalRgbCrossMeasurement"] = measurement.Clone();
            action.MasterValue(null, saved.MasterId, (int)ViewResultAlgType.FindCross);
            FlowNodeTiming.Run("PublishResult", () => services.Publish(action.SerialNumber, nodeId, zIndex, saved.MasterId));
            return saved;
        }
        finally { ownedFrame?.Dispose(); }
    }
    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new { ServiceName = NodeName, EventName = "FindCross", action.SerialNumber, ParameterJson, SearchRegionPoiTemplate, SearchRegion, ImageFilePath, ResultDirectory });
    private LocalFlowFrame ResolveFrame(CVStartCFC action, string configuredFile, out LocalFlowFrame? ownedFrame, out string? imageFile)
    {
        ownedFrame = null;
        imageFile = null;
        if (action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) && currentFrame != null)
        {
            FlowNodeTiming.Skip("OpenImage");
            string file = currentFrame.Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie ? currentFrame.CvCieFilePath : currentFrame.CvRawFilePath;
            imageFile = string.IsNullOrWhiteSpace(file) ? null : file;
            return currentFrame;
        }
        int sourceMasterId = -1;
        string? fallbackFile;
        if (!string.IsNullOrWhiteSpace(configuredFile)) fallbackFile = Path.GetFullPath(configuredFile.Trim());
        else
        {
            if (!TryGetInputMasterResult(action, 0, out int masterId, out int masterResultType, out _))
            {
                masterId = ReadActionInt(action, "MasterId");
                masterResultType = ReadActionInt(action, "MasterResultType");
            }
            if (masterId <= 0) throw new InvalidOperationException("流程中没有可用内存帧或图像结果；请连接本地取图/校正节点，或配置图像文件。");
            if (masterResultType is not (int)CVCommCore.CVResultType.Camera_Img and not (int)CVCommCore.CVResultType.Algorithm_Calibration)
                throw new InvalidOperationException($"IN 不是图像结果：MasterId={masterId}，ResultType={masterResultType}。");
            MeasureResultImgModel input = FlowNodeTiming.Run("ResolveImageResult", () => services.GetImageResult(masterId)) ?? throw new InvalidOperationException($"找不到 IN 图像结果：{masterId}。");
            sourceMasterId = masterId;
            string[] candidates = new[] { input.FileUrl, input.RawFile }.OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => Path.GetFullPath(value.Trim())).ToArray();
            fallbackFile = candidates.FirstOrDefault(File.Exists) ?? candidates.FirstOrDefault();
        }
        ownedFrame = FlowNodeTiming.Run("OpenImage", () =>
        {
            if (string.IsNullOrWhiteSpace(fallbackFile) || !File.Exists(fallbackFile))
                throw new FileNotFoundException("十字图像文件不存在。", fallbackFile);
            return services.LoadFrame(fallbackFile);
        });
        if (sourceMasterId > 0) ownedFrame.MasterId = sourceMasterId;
        imageFile = fallbackFile;
        return ownedFrame;
    }

    private static int ReadActionInt(CVStartCFC action, string key)
    {
        if (!action.Data.TryGetValue(key, out object? value) || value == null) return -1;
        try { return Convert.ToInt32(value); }
        catch { return -1; }
    }
}
