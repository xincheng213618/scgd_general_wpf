using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms.Mtf;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Algorithm.LocalMtf;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Nodes;

internal sealed record LocalMtfPersistenceRequest(CVStartCFC Action, string TemplateName, string? ImageFilePath,
    int ZIndex, int TotalTime, object Parameters, JObject Result, string ResultDirectory);
internal sealed record LocalMtfPersistenceResult(int MasterId, string ResultFilePath);
internal sealed record LocalMtfNodeResultData(int MasterId, int SourceMasterId, int PoiSourceMasterId,
    string? ImageFilePath, string ResultFilePath, int TotalTime)
{
    public int MasterResultType => (int)ViewResultAlgType.MTF;
}

internal interface ILocalMtfNodeServices
{
    LocalRealPoiParameters LoadPoi(int masterId, int resultType, string templateName);
    LocalFlowFrame LoadFrame(string path);
    JObject Calculate(HImage image, StripeMtfParameters parameters, MtfRoi[] regions);
    LocalMtfPersistenceResult Persist(LocalMtfPersistenceRequest request);
    void Publish(string serialNumber, string nodeId, int zIndex, int masterId);
}

internal sealed class LocalMtfNodeServices : ILocalMtfNodeServices
{
    public static LocalMtfNodeServices Instance { get; } = new();
    public LocalRealPoiParameters LoadPoi(int masterId, int resultType, string templateName)
    {
        LocalRealPoiParameters parameters = LocalRealPoiInputResolver.Resolve(masterId, resultType, "IN_IMG", templateName,
            FlowEngineLib.Node.POI.POIPointTypes.None, 0, 0);
        if (parameters.Poi.PoiPoints.Count == 0 && parameters.Poi.Id > 0) PoiParam.LoadPoiDetailFromDB(parameters.Poi);
        return parameters;
    }
    public LocalFlowFrame LoadFrame(string path) => LocalFrameFileService.Load(path);
    public JObject Calculate(HImage image, StripeMtfParameters parameters, MtfRoi[] regions) => StripeMtfAnalyzer.Calculate(image, parameters, regions);
    public LocalMtfPersistenceResult Persist(LocalMtfPersistenceRequest request) => LocalMtfResultPersistence.Save(request);
    public void Publish(string serialNumber, string nodeId, int zIndex, int masterId) => ResultMessageBus.Default.PublishPersisted(
        ResultRoutes.LocalFlow, ResultKinds.Algorithm, string.Empty, "MTF", serialNumber, nodeId, zIndex, masterId, (int)ViewResultAlgType.MTF);
}

internal static class LocalMtfResultPersistence
{
    public static LocalMtfPersistenceResult Save(LocalMtfPersistenceRequest request)
    {
        MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(request.Action.SerialNumber)
            ?? throw new InvalidOperationException($"找不到流程批次：{request.Action.SerialNumber}");
        AlgResultMasterModel master = CreateMaster(request, batch.Id);
        string directory = string.IsNullOrWhiteSpace(request.ResultDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Results", "MTF")
            : Path.GetFullPath(request.ResultDirectory.Trim());
        return SaveCore(master, request.Result, directory, static () => new SqlSugarLocalFlowResultTransaction<DetailCommonModel>());
    }

    internal static AlgResultMasterModel CreateMaster(LocalMtfPersistenceRequest request, int batchId)
    {
        if (batchId <= 0) throw new ArgumentOutOfRangeException(nameof(batchId));
        return new AlgResultMasterModel
        {
            TName = request.TemplateName, ImgFile = request.ImageFilePath, ImgFileType = ViewResultAlgType.MTF,
            version = "2.0", BatchId = batchId, Zindex = request.ZIndex, DeviceCode = null,
            Params = JsonConvert.SerializeObject(request.Parameters), ResultCode = 0, Result = "ok",
            TotalTime = request.TotalTime, CreateDate = DateTime.Now
        };
    }

    internal static LocalMtfPersistenceResult SaveCore(AlgResultMasterModel master, JObject result, string directory,
        Func<ILocalFlowResultTransaction<DetailCommonModel>> transactionFactory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"MTF_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.json");
        string temporary = path + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream, new UTF8Encoding(false))) writer.Write(result.ToString(Formatting.Indented));
            File.Move(temporary, path);
            int masterId = LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore(master, ViewResultAlgType.MTF,
                id => new[] { new DetailCommonModel { PId = id, ResultJson = JsonConvert.SerializeObject(new { ResultFileName = path }) } }, transactionFactory);
            return new(masterId, path);
        }
        catch (Exception saveException)
        {
            try { File.Delete(temporary); File.Delete(path); }
            catch (Exception cleanupException) { throw new AggregateException("保存 MTF 结果失败，且清理结果文件失败。", saveException, cleanupException); }
            throw;
        }
    }
}

[STNode("Flow_CustomNodes", "MTF计算(V2)")]
public sealed class LocalMtfNode : LocalFlowNodeBase
{
    private readonly ILocalMtfNodeServices services;
    private string resultName = "MTF_HV";
    private string parameterJson = "{}";
    private Int32Rect searchRegion = Int32Rect.Empty;
    private string resultDirectory = string.Empty;

    [Category("本地 MTF")]
    [PropertyEditorType(typeof(LocalMtfConfigurationEditor))]
    [STNodeProperty("算法参数", "与 ImageView 共用的本地条纹算法参数，不使用数据库模板。", true)]
    public string ParameterJson { get => parameterJson; set { parameterJson = value ?? "{}"; OnPropertyChanged(); } }

    [Category("本地 MTF")]
    [STNodeProperty("结果名称", "仅用于原 MTF 2.0 主记录的名称，不查询模板。", true)]
    public string ResultName { get => resultName; set { resultName = value ?? string.Empty; OnPropertyChanged(); } }

    [Category("本地 MTF")]
    [STNodeProperty("测量区域", "IN_POI 有结果时使用布点矩形；否则使用本矩形，0,0,0,0 表示整图。单向为测量框，四部为一组图案搜索框。", true, DescriptorType = typeof(Int32RectNodePropertyDescriptor))]
    public Int32Rect SearchRegion { get => searchRegion; set { searchRegion = value; OnPropertyChanged(); } }

    [Category("本地 MTF")]
    [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
    [STNodeProperty("结果目录", "留空保存到当前用户 LocalAppData 下 ColorVision\\Results\\MTF。", true)]
    public string ResultDirectory { get => resultDirectory; set { resultDirectory = value ?? string.Empty; OnPropertyChanged(); } }

    public LocalMtfNode() : this(LocalMtfNodeServices.Instance) { }
    internal LocalMtfNode(ILocalMtfNodeServices services) : base("MTF计算(V2)", "LocalMTF", "MTF", "IN_IMG", "IN_POI")
        => this.services = services ?? throw new ArgumentNullException(nameof(services));

    protected override string GetCompactSummaryValue() => CompactValueOrDash(ResultName);
    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action) => new() { Data = ExecuteSynchronously(action) };

    internal LocalMtfNodeResultData ExecuteSynchronously(CVStartCFC action)
    {
        string templateName = ResultName.Trim();
        string outputDirectory = ResultDirectory, nodeId = NodeID;
        Int32Rect configuredRegion = SearchRegion;
        int zIndex = ZIndex;
        if (templateName.Length == 0) throw new InvalidOperationException("请填写 MTF 结果名称。");
        StripeMtfParameters parameters = StripeMtfParameters.FromJson(ParameterJson);
        _ = TryGetInputMasterResult(action, 1, out int poiMasterId, out int poiResultType, out _);
        LocalRealPoiParameters? poi = poiMasterId > 0 ? services.LoadPoi(poiMasterId, poiResultType, string.Empty) : null;
        LocalFlowFrame? ownedFrame = null;
        try
        {
            if (!action.TryGetCurrentFrame(out LocalFlowFrame? frame) || frame == null)
            {
                string path = ResolveInputImageFilePath(action, 0, string.Empty);
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("IN_IMG 没有本地图像帧或已保存的图像结果。");
                frame = ownedFrame = FlowNodeTiming.Run("OpenImage", () => services.LoadFrame(path));
                _ = TryGetInputMasterResult(action, 0, out int imageMasterId, out _, out _);
                frame.MasterId = imageMasterId;
            }
            using LocalFlowFrameLease lease = frame.Acquire();
            if (!lease.IsFlipApplied) throw new InvalidOperationException("MTF 输入图像的方向变换尚未完成。");
            if (lease.Metadata.PrimaryBufferKind != LocalFrameBufferKind.CvRaw)
                throw new NotSupportedException("本地 MTF 2.0 当前只接收 RAW 图像；请连接相机或本地图片输出。");
            HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
            MtfRoi[] regions;
            if (poi != null) regions = LocalMtfRoiAdapter.BuildRegions(poi.Poi.PoiPoints, image.cols, image.rows);
            else
            {
                RoiRect configured = LocalFindLuminousAreaNode.ResolveRoi(configuredRegion, image.cols, image.rows);
                regions = [new("Center", configured.X, configured.Y,
                    configured.Width == 0 ? image.cols : configured.Width, configured.Height == 0 ? image.rows : configured.Height)];
            }
            string? imageFile = frame.ResolveResultImageFilePath();
            Stopwatch timer = Stopwatch.StartNew();
            JObject result = FlowNodeTiming.Run("CalculateMTF", () => services.Calculate(image, parameters, regions));
            timer.Stop();
            int elapsed = checked((int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue));
            var audit = new
            {
                Algorithm = "StripeMtf", StripeMtfAnalyzer.FormulaVersion, SourceMasterId = lease.MasterId,
                POISourceMasterId = poi?.SourceMasterId ?? 0, Parameters = parameters.ToJson(), RoiRects = regions,
                ImageWidth = image.cols, ImageHeight = image.rows, Bpp = image.depth, Channels = image.channels,
                ImageRead = ownedFrame != null, MemoryOnly = string.IsNullOrWhiteSpace(imageFile)
            };
            LocalMtfPersistenceResult saved = FlowNodeTiming.Run("SaveResult", () => services.Persist(
                new(action, templateName, imageFile, zIndex, elapsed, audit, result, outputDirectory)));
            if (saved.MasterId <= 0 || string.IsNullOrWhiteSpace(saved.ResultFilePath)) throw new InvalidOperationException("MTF 结果持久化未返回有效记录。");
            FlowNodeTiming.Run("PublishResult", () => services.Publish(action.SerialNumber, nodeId, zIndex, saved.MasterId));
            action.Data["LocalMtfResultFile"] = saved.ResultFilePath;
            action.MasterValue(null, saved.MasterId, (int)ViewResultAlgType.MTF);
            return new(saved.MasterId, lease.MasterId, poi?.SourceMasterId ?? 0, imageFile, saved.ResultFilePath, elapsed);
        }
        finally { ownedFrame?.Dispose(); }
    }

    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
    {
        ServiceName = NodeName, EventName = OperatorCode, action.SerialNumber, ResultName, ParameterJson, SearchRegion, ResultDirectory
    });
}
