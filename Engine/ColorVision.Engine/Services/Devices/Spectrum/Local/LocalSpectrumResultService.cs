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
        var model = data.ToObject<SpectumResultEntity>()!;
        model.fCCT = float.IsFinite(capture.Data.fCCT) ? capture.Data.fCCT : null;
        model.dC = float.IsFinite(capture.Data.dC) ? capture.Data.dC : null;
        model.fLd = float.IsFinite(capture.Data.fLd) ? capture.Data.fLd : null;
        model.fPur = float.IsFinite(capture.Data.fPur) ? capture.Data.fPur : null;
        model.fRa = float.IsFinite(capture.Data.fRa) ? capture.Data.fRa : null;
        model.fLav = float.IsFinite(capture.Data.fLav) ? capture.Data.fLav : null;
        if (capture.Data.fRi == null || Array.Exists(capture.Data.fRi, value => !float.IsFinite(value)))
            model.fRi = null;
        data.Remove(nameof(SpectumResultEntity.fPL));
        data.Remove(nameof(SpectumResultEntity.fRi));
        data[nameof(Views.cieData.AcquisitionParametersJson)] = capture.AcquisitionParametersJson;
        model.CieDataEx = data.ToString(Formatting.None);
        model.DeviceCode = capture.DeviceCode;
        model.CreateDate = capture.CapturedAt;
        model.IntTime = capture.IntegralTime;
        model.iAveNum = parameters.NumberOfAverage;
        model.AutoIntegration = parameters.AutoIntegration;
        model.AutoInitDark = parameters.AutoInitDark;
        model.SelfAdaptionInitDark = parameters.SelfAdaptionInitDark;
        model.Params = JsonConvert.SerializeObject(parameters);
        model.DataType = parameters.Eqe;
        if (capture.EqeData is { } eqe)
        {
            model.Eqe = eqe.dEqe; model.VResult = (float)eqe.dVoltage; model.IResult = (float)eqe.dCurrent;
            model.AFactor = (float)parameters.AFactor; model.LuminousFlux = eqe.dIm; model.RadiantFlux = eqe.dW;
        }
        return model;
    }

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
                model.VResult = double.IsFinite(smu.V) ? (float)smu.V : null;
                model.IResult = double.IsFinite(smu.I) ? (float)smu.I : null;
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
