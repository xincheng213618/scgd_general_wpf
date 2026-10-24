using ColorVision.Algorithms;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.RingGhost;
using ColorVision.ImageEditor.Algorithms;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes;

internal sealed record LocalRingGhostSaveRequest(CVStartCFC Action, LocalRingGhostResult Measurement,
    string? ImageFile, string Directory, int ZIndex, int TotalTime, object Parameters);
internal sealed record LocalRingGhostSavedResult(int MasterId, string ResultFilePath);
internal interface ILocalRingGhostNodeServices
{
    LocalFlowFrame LoadFrame(string path);
    MeasureResultImgModel? GetImageResult(int id);
    AlgorithmResult Detect(LocalFlowFrameLease frame, AlgorithmInvocation invocation);
    LocalRingGhostSavedResult Save(LocalRingGhostSaveRequest request);
    void Publish(string serialNumber, string nodeId, int zIndex, int masterId);
}

internal sealed class LocalRingGhostNodeServices : ILocalRingGhostNodeServices
{
    public LocalFlowFrame LoadFrame(string path) => LocalFrameFileService.Load(path);
    public MeasureResultImgModel? GetImageResult(int id) => MeasureImgResultDao.Instance.GetById(id);
    public AlgorithmResult Detect(LocalFlowFrameLease frame, AlgorithmInvocation invocation) =>
        LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(frame, invocation).AsTask().GetAwaiter().GetResult();
    public LocalRingGhostSavedResult Save(LocalRingGhostSaveRequest request) => LocalRingGhostResultPersistence.Save(request);
    public void Publish(string serialNumber, string nodeId, int zIndex, int masterId) => ResultMessageBus.Default.PublishPersisted(
        ResultRoutes.LocalFlow, ResultKinds.Algorithm, "", "Ghost", serialNumber, nodeId, zIndex, masterId, (int)ViewResultAlgType.Ghost);
}

internal static class LocalRingGhostResultPersistence
{
    internal static AlgResultMasterModel CreateMaster(LocalRingGhostSaveRequest request, int batchId)
    {
        if (batchId <= 0) throw new ArgumentOutOfRangeException(nameof(batchId));
        request.Measurement.Validate();
        return new()
        {
            TId = null, TName = "LocalRingGhost", ImgFileType = ViewResultAlgType.Ghost,
            version = LocalRingGhostResult.ResultVersion, BatchId = batchId, Zindex = request.ZIndex,
            ImgFile = request.ImageFile, Params = JsonConvert.SerializeObject(request.Parameters), ResultCode = 0,
            Result = request.Measurement.MeasurementStatus, TotalTime = request.TotalTime, CreateDate = DateTime.Now
        };
    }

    internal static DetailCommonModel CreateDetail(int masterId, string file) => new()
    {
        PId = masterId, ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = file })
    };

    internal static LocalRingGhostSavedResult Save(LocalRingGhostSaveRequest request)
    {
        var batch = BatchResultMasterDao.Instance.GetByNameOrCode(request.Action.SerialNumber)
            ?? throw new InvalidOperationException("找不到鬼影流程批次。");
        AlgResultMasterModel master = CreateMaster(request, batch.Id);
        string directory = Path.GetFullPath(request.Directory);
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"RingGhost_{batch.Id}_{Guid.NewGuid():N}.json");
        bool created = false;
        try
        {
            using (FileStream stream = new(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                System.Text.Json.JsonSerializer.Serialize(stream, request.Measurement);
            }
            int id = SaveDatabaseCore(master, file, static () => new SqlSugarLocalFlowResultTransaction<DetailCommonModel>());
            return new(id, file);
        }
        catch (Exception error)
        {
            try { if (created) File.Delete(file); }
            catch (Exception cleanup) { throw new AggregateException("鬼影结果入库失败，清理结果文件也失败。", error, cleanup); }
            throw;
        }
    }

    internal static int SaveDatabaseCore(AlgResultMasterModel master, string file,
        Func<ILocalFlowResultTransaction<DetailCommonModel>> transactions) => LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore(
            master, ViewResultAlgType.Ghost, id => new[] { CreateDetail(id, file) }, transactions);
}

[STNode("Flow_LocalAlgorithms", "圆环鬼影", CategoryOrder = 9910, NodeOrder = 80)]
public sealed class LocalRingGhostNode : LocalFlowNodeBase
{
    private readonly ILocalRingGhostNodeServices services;
    private string resultDirectory = "";
    private RingGhostOrderMapping orderMapping = RingGhostOrderMapping.OuterIsFirst;

    [Category("鬼影"), STNodeProperty("一阶/二阶对应", "指定内外圈与一阶、二阶的对应关系；默认外圈一阶、内圈二阶。按当前一阶必有的方案，单环归一阶，正常未检出输出 0。", true)]
    public RingGhostOrderMapping OrderMapping { get => orderMapping; set { orderMapping = value; OnPropertyChanged(); } }
    [Category("保存"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
    [STNodeProperty("结果目录", "保存计算结果文件，供 ARVRPro 解析和历史回看。留空自动使用本机 LocalAppData 下的 ColorVision/Results/RingGhost，无需配置。", true)]
    public string ResultDirectory { get => resultDirectory; set { resultDirectory = value ?? ""; OnPropertyChanged(); } }

    public LocalRingGhostNode() : this(new LocalRingGhostNodeServices()) { }
    internal LocalRingGhostNode(ILocalRingGhostNodeServices services) : base("圆环鬼影", "LocalRingGhost", "Ghost") => this.services = services;
    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        var result = ExecuteSynchronously(action);
        return new() { Data = new { GMR1 = result.Measurement.GMR1, GMR2 = result.Measurement.GMR2 } };
    }

    internal (LocalRingGhostSavedResult Saved, LocalRingGhostResult Measurement) ExecuteSynchronously(CVStartCFC action)
    {
        action.Data.Remove("GMR1"); action.Data.Remove("GMR2");
        RingGhostOrderMapping mapping = OrderMapping;
        if (mapping is not RingGhostOrderMapping.OuterIsFirst and not RingGhostOrderMapping.InnerIsFirst)
            throw new InvalidOperationException("请确定鬼影内外位置对应关系。");
        string directory = string.IsNullOrWhiteSpace(ResultDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Results", "RingGhost")
            : Path.GetFullPath(ResultDirectory.Trim());
        string nodeId = NodeID; int zIndex = ZIndex;
        LocalFlowFrame? ownedFrame = null;
        LocalFlowFrame frame = ResolveFrame(action, out ownedFrame, out string? imageFile);
        try
        {
            using var lease = frame.Acquire();
            if (!lease.IsFlipApplied) throw new InvalidOperationException("当前图像方向变换尚未完成。");
            if (lease.Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie)
                throw new NotSupportedException("当前圆环鬼影结点使用线性 8/16 位 RAW 或 TIFF 信号；不能把 CVCIE 原图与 RAW 通道测量混用。");
            var parameters = new RingGhostMeasurementParameters { OrderMapping = mapping };
            var timer = Stopwatch.StartNew();
            using AlgorithmResult algorithm = FlowNodeTiming.Run("Algorithm", () => services.Detect(lease,
                AlgorithmInvocation.Create(DisplayMetrologyIds.RingGhost, parameters)));
            if (algorithm.Status != AlgorithmResultStatus.Succeeded)
                throw new InvalidOperationException(string.Join(";", algorithm.Failures.Select(f => f.Code + ":" + f.Message)));
            var measurement = LocalRingGhostResult.FromAlgorithm(algorithm, lease.Metadata.Width, lease.Metadata.Height, mapping);
            var request = new LocalRingGhostSaveRequest(action, measurement, imageFile, directory, zIndex,
                (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue), new
                {
                    Algorithm = DisplayMetrologyIds.RingGhost.Value, FrameId = lease.FrameId, SourceMasterId = lease.MasterId,
                    Parameters = parameters, FirstOrderAlwaysPresent = true, NormalNonDetectionValue = 0
                });
            var saved = FlowNodeTiming.Run("PersistResult", () => services.Save(request));
            if (saved.MasterId <= 0 || string.IsNullOrWhiteSpace(saved.ResultFilePath)) throw new InvalidOperationException("鬼影结果保存返回无效记录。");
            if (ownedFrame != null) { action.SetCurrentFrame(ownedFrame); ownedFrame = null; }
            action.Data["GMR1"] = measurement.GMR1;
            action.Data["GMR2"] = measurement.GMR2;
            action.MasterValue(null, saved.MasterId, (int)ViewResultAlgType.Ghost);
            FlowNodeTiming.Run("PublishResult", () => services.Publish(action.SerialNumber, nodeId, zIndex, saved.MasterId));
            return (saved, measurement);
        }
        finally { ownedFrame?.Dispose(); }
    }

    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
    { ServiceName = NodeName, EventName = "Ghost", action.SerialNumber, OrderMapping, ResultDirectory });

    private LocalFlowFrame ResolveFrame(CVStartCFC action, out LocalFlowFrame? ownedFrame, out string? imageFile)
    {
        ownedFrame = null; imageFile = null;
        if (action.TryGetCurrentFrame(out LocalFlowFrame? current) && current != null)
        {
            FlowNodeTiming.Skip("OpenImage");
            imageFile = current.ResolveResultImageFilePath();
            return current;
        }
        if (!TryGetInputMasterResult(action, 0, out int masterId, out int resultType, out _))
        {
            masterId = ReadInt(action, "MasterId"); resultType = ReadInt(action, "MasterResultType");
        }
        if (masterId <= 0 || resultType is not (int)CVCommCore.CVResultType.Camera_Img and not (int)CVCommCore.CVResultType.Algorithm_Calibration)
            throw new InvalidOperationException("流程中没有可用的上游图像；请连接相机取图、校正或“加载图片”节点。");
        var input = FlowNodeTiming.Run("ResolveImageResult", () => services.GetImageResult(masterId))
            ?? throw new InvalidOperationException($"找不到 IN 图像结果：{masterId}。");
        string[] files = new[] { input.FileUrl, input.RawFile }.OfType<string>().Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => Path.GetFullPath(f.Trim())).ToArray();
        string? file = files.FirstOrDefault(File.Exists) ?? files.FirstOrDefault();
        ownedFrame = FlowNodeTiming.Run("OpenImage", () =>
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) throw new FileNotFoundException("鬼影原图不存在。", file);
            return services.LoadFrame(file);
        });
        ownedFrame.MasterId = masterId;
        imageFile = file;
        return ownedFrame;
    }

    private static int ReadInt(CVStartCFC action, string key)
    {
        if (!action.Data.TryGetValue(key, out object? value) || value == null) return -1;
        try { return Convert.ToInt32(value); }
        catch (Exception error) when (error is FormatException or InvalidCastException or OverflowException) { return -1; }
    }
}
