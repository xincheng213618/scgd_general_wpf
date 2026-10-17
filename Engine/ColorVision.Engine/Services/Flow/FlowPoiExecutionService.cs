using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Flow;

public sealed class FlowPoiExecutionRequest
{
    public string? FlowName { get; set; }
    public int? FlowId { get; set; }
}

public sealed record FlowPoiPoint(
    int AlgorithmResultId,
    string? AlgorithmName,
    int? PoiId,
    string Name,
    int PixelX,
    int PixelY,
    int Width,
    int Height,
    double X,
    double Y,
    double Z,
    double x,
    double y,
    double u,
    double v,
    double CCT,
    double Wave);

public sealed record FlowPoiExecutionData(
    int FlowId,
    string FlowName,
    int BatchId,
    string ExecutionSerialNumber,
    string Status,
    long TotalTime,
    IReadOnlyList<FlowPoiPoint> Poi);

public sealed record FlowPoiExecutionResult(int Code, string Message, FlowPoiExecutionData? Data = null);

internal sealed record FlowPoiRun(int FlowId, string FlowName, int BatchId, FlowRunFinalizedData Finalized);

internal sealed class FlowPoiExecutionException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}

/// <summary>
/// Runs a named main-workspace flow through its normal lifecycle, then reads
/// POI_XYZ results belonging exclusively to that execution's captured batch.
/// </summary>
public sealed class FlowPoiExecutionService
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FlowPoiExecutionService));
    public static FlowPoiExecutionService Shared { get; } = new();
    private readonly Func<FlowPoiExecutionRequest, Task<FlowPoiRun>> runFlow;
    private readonly Func<int, IReadOnlyList<FlowPoiPoint>> readPoi;

    public FlowPoiExecutionService() : this(RunMainFlowAsync, ReadPoi)
    {
    }

    internal FlowPoiExecutionService(
        Func<FlowPoiExecutionRequest, Task<FlowPoiRun>> runFlow,
        Func<int, IReadOnlyList<FlowPoiPoint>> readPoi)
    {
        this.runFlow = runFlow;
        this.readPoi = readPoi;
    }

    public async Task<FlowPoiExecutionResult> RunAsync(FlowPoiExecutionRequest request)
    {
        FlowPoiExecutionData? data = null;
        try
        {
            if (request == null)
                return new(400, "必须提供流程参数。");
            // Detach caller-owned input before crossing the UI dispatcher.
            var input = new FlowPoiExecutionRequest { FlowName = request.FlowName?.Trim(), FlowId = request.FlowId };
            Validate(input);
            FlowPoiRun run = await runFlow(input).ConfigureAwait(false);
            data = new FlowPoiExecutionData(
                run.FlowId, run.FlowName, run.BatchId,
                run.Finalized.EngineResult.SerialNumber,
                run.Finalized.FinalOutcome.ToString(),
                run.Finalized.EngineResult.TotalTime,
                Array.Empty<FlowPoiPoint>());

            if (run.Finalized.FinalOutcome is not (FlowFinalOutcome.Succeeded or FlowFinalOutcome.SucceededWithWarnings))
            {
                int code = run.Finalized.FinalOutcome switch
                {
                    FlowFinalOutcome.Canceled => 499,
                    FlowFinalOutcome.TimedOut => 504,
                    _ => 500
                };
                return new(code, $"流程执行未成功：{data.Status}。{run.Finalized.EngineResult.Message}", data);
            }

            if (run.BatchId <= 0)
                return new(503, "本次流程没有可查询的 MySQL 结果批次。", data);

            IReadOnlyList<FlowPoiPoint> points = await Task.Run(() => readPoi(run.BatchId)).ConfigureAwait(false);
            data = data with { Poi = points.ToArray() };
            return points.Count == 0
                ? new(422, "流程已完成，本次批次没有 POI_XYZ 点位结果。", data)
                : new(200, "流程执行完成", data);
        }
        catch (FlowPoiExecutionException ex)
        {
            return new(ex.Code, ex.Message, data);
        }
        catch (Exception ex)
        {
            log.Error("执行流程并读取 POI 失败。", ex);
            return new(500, $"执行流程或读取 POI 失败：{ex.GetBaseException().Message}", data);
        }
    }

    private static void Validate(FlowPoiExecutionRequest request)
    {
        bool hasName = !string.IsNullOrWhiteSpace(request.FlowName);
        if (hasName == request.FlowId.HasValue || request.FlowId == 0)
            throw new FlowPoiExecutionException(400, "FlowName 与 FlowId 必须且只能填写一项；FlowId 不能为 0。");
    }

    internal static TemplateModel<FlowParam> ResolveTemplate(
        IEnumerable<TemplateModel<FlowParam>> templates, FlowPoiExecutionRequest request)
    {
        Validate(request);
        TemplateModel<FlowParam>[] matches = templates.Where(item => request.FlowId.HasValue
            ? item.Id == request.FlowId.Value
            : string.Equals(item.Key, request.FlowName?.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 0)
            throw new FlowPoiExecutionException(404, "找不到指定的流程模板。");
        if (matches.Length != 1)
            throw new FlowPoiExecutionException(409, "流程名称或 ID 不唯一，请使用唯一的流程模板 ID。");
        return matches[0];
    }

    private static async Task<FlowPoiRun> RunMainFlowAsync(FlowPoiExecutionRequest request)
    {
        Application application = Application.Current
            ?? throw new FlowPoiExecutionException(503, "主程序尚未就绪。");
        if (application.Dispatcher.CheckAccess())
            return await RunOnUiAsync(request);

        Task<FlowPoiRun> run = await application.Dispatcher.InvokeAsync(() => RunOnUiAsync(request));
        return await run.ConfigureAwait(false);
    }

    private static async Task<FlowPoiRun> RunOnUiAsync(FlowPoiExecutionRequest request)
    {
        TemplateModel<FlowParam> template = ResolveTemplate(TemplateFlow.Params, request);
        if (!MySqlSetting.IsConnect)
            throw new FlowPoiExecutionException(503, "MySQL 未连接，无法返回流程的 POI 结果。");

        FlowEngineManager manager = FlowEngineManager.GetInstance();
        int flowId = template.Id;
        string flowName = template.Key;
        MeasureBatchModel? batch = null;
        void CaptureBatch(object? sender, MeasureBatchModel value) => batch ??= value;
        manager.BatchRecord += CaptureBatch;
        try
        {
            FlowRunFinalizedData? finalized = await manager.RunFlowAndWaitForFinalizationAsync(template);
            if (finalized == null)
                throw new FlowPoiExecutionException(409, "流程未能启动：请检查流程占用、未保存的画布、起始节点及设备/服务连接。");

            // Do not read manager.Batch after awaiting: another caller may
            // already have replaced it. Reject any mismatched captured identity.
            if (batch == null || !string.Equals(batch.Code, finalized.EngineResult.SerialNumber, StringComparison.Ordinal))
                throw new InvalidOperationException("流程完成结果与本次批次身份不一致。");
            return new FlowPoiRun(flowId, flowName, batch.Id, finalized);
        }
        finally
        {
            manager.BatchRecord -= CaptureBatch;
        }
    }

    private static IReadOnlyList<FlowPoiPoint> ReadPoi(int batchId)
    {
        // Query directly so a database failure cannot masquerade as an empty
        // result through the legacy DAO's safe-empty fallback.
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = MySqlControl.GetConnectionString(),
            DbType = SqlSugar.DbType.MySql,
            IsAutoCloseConnection = true
        });
        List<AlgResultMasterModel> masters = db.Queryable<AlgResultMasterModel>()
            .Where(item => item.BatchId == batchId && item.ImgFileType == ViewResultAlgType.POI_XYZ)
            .OrderBy(item => item.Id).ToList();
        var points = new List<FlowPoiPoint>();
        foreach (AlgResultMasterModel master in masters)
        {
            List<PoiPointResultModel> details = db.Queryable<PoiPointResultModel>()
                .Where(item => item.Pid == master.Id).OrderBy(item => item.Id).ToList();
            points.AddRange(details.Select(item => CreatePoint(master, item)));
        }
        return points;
    }

    internal static FlowPoiPoint CreatePoint(AlgResultMasterModel master, PoiPointResultModel detail)
    {
        if (string.IsNullOrWhiteSpace(detail.Value))
            throw new InvalidOperationException($"POI 结果 {detail.Id} 没有点位数值。");
        var poi = new PoiResultCIExyuvData(detail);
        return new FlowPoiPoint(master.Id, master.TName, detail.PoiId, poi.Name,
            detail.PoiX ?? 0, detail.PoiY ?? 0, detail.PoiWidth ?? 0, detail.PoiHeight ?? 0,
            poi.X, poi.Y, poi.Z, poi.x, poi.y, poi.u, poi.v, poi.CCT, poi.Wave);
    }
}
