using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using Newtonsoft.Json.Linq;
using SqlSugar;

namespace ProjectARVRPro.Services;

internal static class LuxDeviceMeasurements
{
    internal static async Task<double?> MeasureVidAsync(CancellationToken cancellationToken)
    {
        DeviceCamera camera = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().FirstOrDefault()
            ?? throw new InvalidOperationException("未配置相机设备。");
        DisplayCameraConfig display = DisplayConfigManager.Instance.GetDisplayConfig<DisplayCameraConfig>(camera.Config.Code);
        if (display.AutoFocusTemplateIndex < 0 || display.AutoFocusTemplateIndex >= TemplateAutoFocus.Params.Count)
            throw new InvalidOperationException("请先选择自动对焦模板。");
        MsgReturn? response = await WaitForResultAsync(camera.DService.AutoFocus(TemplateAutoFocus.Params[display.AutoFocusTemplateIndex].Value), cancellationToken);
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

    internal static async Task<(bool Succeeded, ViewResultSpectrum? Result)> MeasureFluxAsync(CancellationToken cancellationToken)
    {
        DeviceSpectrum spectrum = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault()
            ?? throw new InvalidOperationException("未配置光谱仪设备。");
        // Keep the existing device API: its backend decides between local SDK and service execution.
        MsgReturn? response = await WaitForResultAsync(spectrum.DService.GetData(), cancellationToken);
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
        return (true, model == null ? null : new ViewResultSpectrum(model));
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
            else if (state is MsgRecordState.Fail or MsgRecordState.Timeout) completion.TrySetResult(null);
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
