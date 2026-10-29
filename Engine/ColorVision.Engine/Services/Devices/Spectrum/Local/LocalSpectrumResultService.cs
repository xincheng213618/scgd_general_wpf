using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal sealed record LocalSpectrumCommandResult(LocalSpectrumCapture Capture, SpectumResultEntity Model)
{
    public int MasterId => Model.Id;
    public int MasterResultType => 300;
}

internal static class LocalSpectrumResultService
{
    internal static SpectumResultEntity CreateModel(LocalSpectrumCapture capture, LocalSpectrumParameters parameters)
    {
        JObject data = JObject.FromObject(capture.Data);
        data[nameof(SpectumResultEntity.fPL)] = JsonConvert.SerializeObject(capture.Data.fPL);
        data[nameof(SpectumResultEntity.fRi)] = JsonConvert.SerializeObject(capture.Data.fRi);
        // SQL numeric columns cannot store NaN/Infinity; retain the original values in the JSON archive.
        var storageData = (JObject)data.DeepClone();
        foreach (var field in storageData.Properties())
            if (field.Value.Type == JTokenType.Float && !double.IsFinite(field.Value.Value<double>())) field.Value = JValue.CreateNull();
        var model = storageData.ToObject<SpectumResultEntity>()!;
        if (capture.Data.fRi == null || Array.Exists(capture.Data.fRi, value => !float.IsFinite(value)))
            model.fRi = null;
        data.Remove(nameof(SpectumResultEntity.fPL));
        data.Remove(nameof(SpectumResultEntity.fRi));
        data[nameof(Views.cieData.AcquisitionParametersJson)] = capture.AcquisitionParametersJson;
        model.CieDataEx = data.ToString(Formatting.None);
        model.DeviceCode = capture.DeviceCode;
        model.CreateDate = capture.CapturedAt;
        model.IntTime = capture.IntegralTime;
        model.NDPort = capture.NDPort;
        model.iAveNum = parameters.NumberOfAverage;
        model.AutoIntegration = parameters.AutoIntegration;
        model.AutoInitDark = parameters.AutoInitDark;
        model.SelfAdaptionInitDark = parameters.SelfAdaptionInitDark;
        model.Params = JsonConvert.SerializeObject(parameters);
        model.DataType = parameters.Eqe;
        if (capture.EqeData is { } eqe)
        {
            model.Eqe = double.IsFinite(eqe.dEqe) ? eqe.dEqe : null;
            model.VResult = FiniteFloat(parameters.SMUData?.V ?? parameters.Voltage);
            model.IResult = FiniteFloat(parameters.SMUData?.I ?? parameters.Current * 1000.0);
            if (parameters.SMUData is { MasterId: > 0, MasterResultType: 200 } smu) model.SmuDataId = smu.MasterId;
            model.AFactor = FiniteFloat(parameters.AFactor);
            model.LuminousFlux = FiniteFloat(eqe.dIm);
            model.RadiantFlux = double.IsFinite(eqe.dW) ? eqe.dW : null;
        }
        return model;
    }

    private static float? FiniteFloat(double value) => float.IsFinite((float)value) ? (float)value : null;

    internal static SpectumResultEntity Save(LocalSpectrumCapture capture, LocalSpectrumParameters parameters, CVStartCFC? action = null, int zIndex = 0)
    {
        var model = CreateModel(capture, parameters);
        bool persist = action?.PersistResults ?? MySqlSetting.IsConnect;
        if (!persist) return model;
        if (action != null)
        {
            MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
                ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
            model.BatchId = batch.Id;
            model.Zindex = zIndex;
            if (!parameters.Eqe && action.Data.TryGetValue("SMUResult", out var value)
                && value is FlowEngineLib.SMUResultData { MasterId: > 0, MasterResultType: 200 } smu)
            {
                model.SmuDataId = smu.MasterId;
                model.VResult = FiniteFloat(smu.V);
                model.IResult = FiniteFloat(smu.I);
            }
        }
        // Historical results always use the existing MySQL table, never the configuration SQLite store.
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = DbType.MySql, IsAutoCloseConnection = true });
        model.Id = db.Insertable(model).ExecuteReturnIdentity();
        if (model.Id <= 0) throw new InvalidOperationException("保存本地光谱结果失败。");
        capture.MeasurementId = model.Id;
        return model;
    }
}
