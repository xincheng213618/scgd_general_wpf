using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using log4net;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.ImageExport;

namespace ProjectARVRPro.Services;

internal static class LuxDeviceMeasurements
{
    private static readonly ILog log = LogManager.GetLogger(typeof(LuxDeviceMeasurements));
    // This project's existing version floor predates the host API that can prove result ownership.
    private static readonly Func<MQTTSpectrum, CancellationToken, Task<(bool Succeeded, SpectumResultEntity? Result)>>? MeasureOwnedSpectrum =
        typeof(MQTTSpectrum).GetMethod(nameof(MQTTSpectrum.GetDataResultAsync), [typeof(CancellationToken)])?
            .CreateDelegate<Func<MQTTSpectrum, CancellationToken, Task<(bool Succeeded, SpectumResultEntity? Result)>>>();

    internal static async Task<(bool Succeeded, ObjectiveTestItem Item)> MeasureVidResultAsync(CancellationToken cancellationToken, VidFocusImageExport? export = null)
    {
        var config = ProjectARVRProConfig.Instance;
        double min = config.LuxVidMin, max = config.LuxVidMax;
        double factor = config.LuxVidFix, offset = config.LuxVidB;
        LuxVidOutputUnit unit = config.LuxVidUnit;
        string unitLabel = unit switch
        {
            LuxVidOutputUnit.Millimeters => "mm",
            LuxVidOutputUnit.Meters => "m",
            LuxVidOutputUnit.Diopters => "D",
            _ => throw new InvalidOperationException("请选择有效的 VID 输出单位。")
        };
        double? measured = await MeasureVidAsync(cancellationToken, export);
        double value = measured.HasValue ? CorrectVid(measured.Value, factor, offset, unit) : 0;
        if (measured.HasValue && export != null) await export.SaveAsync(value, unitLabel, cancellationToken);

        return (measured.HasValue, new ObjectiveTestItem
        {
            Name = "VID", Unit = unitLabel, Value = value, LowLimit = min, UpLimit = max
        });
    }

    private static double CorrectVid(double measuredMm, double factor, double offset, LuxVidOutputUnit unit)
    {
        if (!double.IsFinite(measuredMm) || measuredMm == 0)
            throw new InvalidOperationException("原始 VID 必须为非零有限毫米值，才能换算为屈光度。");
        if (!double.IsFinite(factor) || !double.IsFinite(offset))
            throw new InvalidOperationException("VID 修正参数 K 和 B 必须为有限数值。");

        // Preserve the original distance exactly when no correction is configured.
        if (factor == 1 && offset == 0 && unit != LuxVidOutputUnit.Diopters)
            return unit == LuxVidOutputUnit.Meters ? measuredMm / 1000d : measuredMm;

        double diopters = 1000d / measuredMm * factor + offset;
        if (!double.IsFinite(diopters)) throw new InvalidOperationException("修正后的 VID 屈光度不是有限数值。");
        if (unit == LuxVidOutputUnit.Diopters) return diopters;
        if (diopters == 0) throw new InvalidOperationException("修正后的 VID 为 0 D，无法表示为有限距离。");

        double distance = (unit == LuxVidOutputUnit.Meters ? 1d : 1000d) / diopters;
        if (!double.IsFinite(distance)) throw new InvalidOperationException("修正后的 VID 距离不是有限数值。");
        return distance;
    }

    internal static async Task<double?> MeasureVidAsync(CancellationToken cancellationToken, VidFocusImageExport? export = null)
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
        bool captureEvidence = export != null && template != null && camera.RoutesLocally && VidFocusImageExport.HostSupportsEvidence;
        if (export != null && !captureEvidence)
            log.Warn("VID 对焦图像需要支持图像导出的宿主和本地相机聚焦模式。");
        MsgRecord command = captureEvidence ? VidFocusImageExport.Measure(camera, template!, cancellationToken, fineFocus)
            : template == null ? camera.DService.GetPosition()
            : localFocus?.Measure(camera, template, cancellationToken) ?? camera.DService.AutoFocus(template);
        MsgReturn? response = await WaitForResultAsync(command, cancellationToken);
        if (response != null && captureEvidence && export!.TryCaptureResult(response, out double? vid))
            return vid;
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

    internal static Task<(bool Succeeded, SpectumResultEntity? Result)> MeasureFluxAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var measure = MeasureOwnedSpectrum ?? throw new InvalidOperationException("当前 ColorVision 宿主缺少按原采集读取光谱结果的接口，请更新宿主后重试。");
        DeviceSpectrum spectrum = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault()
            ?? throw new InvalidOperationException("未配置光谱仪设备。");
        MQTTSpectrum service = spectrum.DService;
        bool openLocal = spectrum.IsLocalSpectrumBackend && service.DeviceStatus == DeviceStatusType.Closed;
        return RequestFluxMeasurementAsync(openLocal, service.Open, token => measure(service, token), cancellationToken);
    }

    internal static async Task<(bool Succeeded, SpectumResultEntity? Result)> RequestFluxMeasurementAsync(bool openLocal,
        Func<MsgRecord> open, Func<CancellationToken, Task<(bool Succeeded, SpectumResultEntity? Result)>> measure,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (openLocal)
        {
            log.Info("LUX 本地光谱仪打开开始");
            if (await WaitForResultAsync(open(), cancellationToken) == null) return (false, null);
            log.Info("LUX 本地光谱仪打开完成");
        }
        cancellationToken.ThrowIfCancellationRequested();
        log.Info("LUX 光通量采集开始");
        var result = await measure(cancellationToken);
        log.Info($"LUX 光通量采集结束: Success={result.Succeeded}");
        return result;
    }

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
