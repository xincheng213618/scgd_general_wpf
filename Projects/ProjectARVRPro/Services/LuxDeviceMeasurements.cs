using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using log4net;
using Newtonsoft.Json.Linq;
using SqlSugar;

namespace ProjectARVRPro.Services;

internal static class LuxDeviceMeasurements
{
    private static readonly ILog log = LogManager.GetLogger(typeof(LuxDeviceMeasurements));

    internal static async Task<(bool Succeeded, ObjectiveTestItem Item)> MeasureVidResultAsync(CancellationToken cancellationToken)
    {
        var config = ProjectARVRProConfig.Instance;
        double min = config.LuxVidMin, max = config.LuxVidMax;
        double factor = config.LuxVidFix, offset = config.LuxVidB;
        double? measured = await MeasureVidAsync(cancellationToken);
        double value = measured.HasValue ? measured.Value * factor + offset : 0;
        if (!double.IsFinite(value)) throw new InvalidOperationException("VID 结果不是有效数值。");

        return (measured.HasValue, new ObjectiveTestItem
        {
            Name = "VID", Unit = "cd/m^2", Value = value, LowLimit = min, UpLimit = max
        });
    }

    internal static async Task<double?> MeasureVidAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = ProjectARVRProConfig.Instance;
        string code = config.LuxVidCameraCode;
        var cameras = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>();
        DeviceCamera camera;
        if (string.IsNullOrEmpty(code))
        {
            camera = cameras.FirstOrDefault() ?? throw new InvalidOperationException("当前没有可用于 VID 测量的相机。");
        }
        else
        {
            var matches = cameras.Where(device => string.Equals(device.Code, code, StringComparison.Ordinal)).Take(2).ToArray();
            if (matches.Length == 0) throw new InvalidOperationException($"找不到 VID 相机“{code}”，请在“其他配置”重新选择。");
            if (matches.Length > 1) throw new InvalidOperationException($"VID 相机设备代码“{code}”重复，请检查设备配置。");
            camera = matches[0];
        }
        LuxVidMeasurementMode mode = config.LuxVidMode;
        if (!Enum.IsDefined(mode)) throw new InvalidOperationException("请选择有效的 VID 测量方式。");
        bool fineFocus = mode == LuxVidMeasurementMode.FineFocus;
        LocalVidFocus? localFocus = fineFocus ? LocalVidFocus.ForCamera(camera) : null;
        AutoFocusParam? template = null;
        if (mode != LuxVidMeasurementMode.GetPosition)
        {
            DisplayCameraConfig display = camera.DisplayConfig;
            if (display.AutoFocusTemplateIndex < 0 || display.AutoFocusTemplateIndex >= TemplateAutoFocus.Params.Count)
                throw new InvalidOperationException($"请先在 VID 相机“{camera.Name}”上选择自动对焦模板。");
            template = TemplateAutoFocus.Params[display.AutoFocusTemplateIndex].Value;
        }
        if (camera.DService.DeviceStatus <= DeviceStatusType.Closed)
        {
            MsgRecord open = localFocus?.Open(camera) ?? camera.DService.Open(camera.Config.CameraID,
                camera.Config.TakeImageMode, (int)camera.Config.ImageBpp);
            MsgReturn? opened = await WaitForResultAsync(open, cancellationToken);
            if (opened == null) return null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        MsgRecord command = template == null ? camera.DService.GetPosition()
            : localFocus?.Measure(camera, template, cancellationToken) ?? camera.DService.AutoFocus(template);
        MsgReturn? response = await WaitForResultAsync(command, cancellationToken);
        return response == null ? null : ReadVid(response);
    }

    internal static double? ReadVid(MsgReturn response)
    {
        JObject data = NormalizeData(response);
        return response.EventName switch
        {
            "AutoFocus" => data.Value<double?>("VidPosition"),
            "GetPosition" => data.Value<double?>("VidPos"),
            _ => null
        };
    }

    internal static async Task<(bool Succeeded, SpectumResultEntity? Result)> MeasureFluxAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeviceSpectrum spectrum = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault()
            ?? throw new InvalidOperationException("未配置光谱仪设备。");
        // Keep the existing device API: its backend decides between local SDK and service execution.
        bool openLocal = spectrum.IsLocalSpectrumBackend && spectrum.DService.DeviceStatus == DeviceStatusType.Closed;
        MsgReturn? response = await RequestFluxMeasurementAsync(openLocal, () => spectrum.DService.Open(),
            () => spectrum.DService.GetData(), cancellationToken);
        if (response == null) return (false, null);
        JObject data = NormalizeData(response);
        SpectumResultEntity? model = ReadLocalSpectrumModel(data);
        if (model == null && data.Value<int?>("MasterId") is > 0 and var masterId)
        {
            model = await Task.Run(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = MySqlControl.GetConnectionString(), DbType = DbType.MySql, IsAutoCloseConnection = true
                });
                return db.Queryable<SpectumResultEntity>().Where(item => item.Id == masterId).First();
            }, cancellationToken);
        }
        return (true, model);
    }

    internal static async Task<MsgReturn?> RequestFluxMeasurementAsync(bool openLocal, Func<MsgRecord> open,
        Func<MsgRecord> measure, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (openLocal)
        {
            log.Info("LUX 本地光谱仪打开开始");
            if (await WaitForResultAsync(open(), cancellationToken) == null) return null;
            log.Info("LUX 本地光谱仪打开完成");
        }
        cancellationToken.ThrowIfCancellationRequested();
        log.Info("LUX 光通量采集开始");
        MsgReturn? result = await WaitForResultAsync(measure(), cancellationToken);
        log.Info($"LUX 光通量采集结束: Success={result != null}");
        return result;
    }

    internal static SpectumResultEntity? ReadLocalSpectrumModel(JObject data) => data["Model"]?.ToObject<SpectumResultEntity>();

    private static JObject NormalizeData(MsgReturn response) => response.Data is JObject json
        ? json : response.Data == null ? new JObject() : JObject.FromObject((object)response.Data);

    internal static async Task<MsgReturn?> WaitForResultAsync(MsgRecord record, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<MsgReturn?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnStateChanged(object? sender, MsgRecordState state)
        {
            if (state == MsgRecordState.Success) completion.TrySetResult(record.MsgReturn);
            else if ((state is MsgRecordState.Fail or MsgRecordState.Timeout) && completion.TrySetResult(null))
                log.Warn($"LUX 设备命令结束: Event={record.MsgSend?.EventName}, State={state}, Code={record.MsgReturn?.Code}, Message={record.MsgReturn?.Message}");
        }
        record.MsgRecordStateChanged += OnStateChanged;
        try
        {
            // Local operations can complete before the caller attaches the event handler.
            OnStateChanged(record, record.MsgRecordState);
            return await completion.Task.WaitAsync(cancellationToken);
        }
        finally { record.MsgRecordStateChanged -= OnStateChanged; }
    }
}
