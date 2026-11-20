using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
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
        // Write the array columns directly instead of creating a JSON token for every sample.
        var scalarData = capture.Data;
        scalarData.fPL = null!;
        scalarData.fRi = null!;
        JObject data = JObject.FromObject(scalarData);
        data.Remove(nameof(SpectumResultEntity.fPL));
        data.Remove(nameof(SpectumResultEntity.fRi));
        // SQL numeric columns cannot store NaN/Infinity; retain the original values in the JSON archive.
        var storageData = (JObject)data.DeepClone();
        foreach (var field in storageData.Properties())
            if (field.Value.Type == JTokenType.Float && !double.IsFinite(field.Value.Value<double>())) field.Value = JValue.CreateNull();
        var model = storageData.ToObject<SpectumResultEntity>()!;
        model.fPL = JsonConvert.SerializeObject(capture.Data.fPL);
        if (capture.Data.fRi is { } indices && Array.TrueForAll(indices, float.IsFinite))
            model.fRi = JsonConvert.SerializeObject(indices);
        data[nameof(Views.cieData.AcquisitionParametersJson)] = capture.AcquisitionParametersJson;
        model.CieDataEx = data.ToString(Formatting.None);
        model.DeviceCode = capture.DeviceCode;
        model.CreateDate = capture.CapturedAt;
        model.IntTime = capture.IntegralTime;
        model.NDPort = capture.NDPort;
        model.iAveNum = capture.EffectiveAverageCount ?? parameters.NumberOfAverage;
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

    internal static SpectumResultEntity Save(LocalSpectrumCapture capture, LocalSpectrumParameters parameters, MySqlConnectedEndpoint? endpoint)
    {
        var model = CreateModel(capture, parameters);
        if (endpoint == null) return model;
        using var db = endpoint.CreateDbClient();
        return Insert(db, capture, model);
    }

    internal static SpectumResultEntity SaveFlow(LocalSpectrumCapture capture, LocalSpectrumParameters parameters,
        CVStartCFC action, int zIndex, MySqlConnectedEndpoint? endpoint)
    {
        var model = CreateModel(capture, parameters);
        if (!action.PersistResults) return model;
        MySqlConnectedEndpoint database = endpoint ?? throw new InvalidOperationException("保存本地光谱流程结果需要已验证的 MySQL 连接。");
        model.BatchId = LocalFlowResultPersistence.ResolveBatchId(action, database);
        model.Zindex = zIndex;
        if (!parameters.Eqe && action.Data.TryGetValue("SMUResult", out var value)
            && value is FlowEngineLib.SMUResultData { MasterId: > 0, MasterResultType: 200 } smu)
        {
            model.SmuDataId = smu.MasterId;
            model.VResult = FiniteFloat(smu.V);
            model.IResult = FiniteFloat(smu.I);
        }
        using var db = database.CreateDbClient();
        return Insert(db, capture, model);
    }

    private static SpectumResultEntity Insert(SqlSugarClient db, LocalSpectrumCapture capture, SpectumResultEntity model)
    {
        model.Id = db.Insertable(model).ExecuteReturnIdentity();
        if (model.Id <= 0) throw new InvalidOperationException("保存本地光谱结果失败。");
        capture.MeasurementId = model.Id;
        return model;
    }
}
