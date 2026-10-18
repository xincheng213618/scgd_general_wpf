using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIGenCali;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Diagnostics;

namespace ColorVision.Engine.FlowProcessing.Nodes;

[STNode("Flow_CustomNodes/Flow_LocalDevices", "光谱参考校正", CategoryOrder = 9900, NodeOrder = 90)]
public sealed class LocalSpectrumCalibrationNode : LocalFlowNodeBase
{
    private string poiPointName = string.Empty;
    private GenCalibrationType correctionMode = GenCalibrationType.BrightnessAndChroma;

    [STNodeProperty("POI点名称", "与光谱仪视场对应的参考点。上游只有一个 POI 时可留空，多个点时必须填写唯一名称。", true)]
    public string POIPointName { get => poiPointName; set { poiPointName = value ?? string.Empty; OnPropertyChanged(); } }

    [STNodeProperty("修正方式", "每次使用本次光谱参考生成倍率，合入当前 CVRAW 的色度矩阵；不修改相机校正模板，不生成 CVCIE。", true)]
    public GenCalibrationType CorrectionMode { get => correctionMode; set { correctionMode = value; OnPropertyChanged(); } }

    public LocalSpectrumCalibrationNode() : base("光谱参考校正", "LocalSpectrumCalibration", "CorrectRaw", "IN_POI", "IN_SPE") { }

    protected override string GetCompactSummaryValue() => CompactValueOrDash(POIPointName);

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        if (!TryGetInputMasterResult(action, 0, out int poiId, out int poiType, out _)
            || poiId <= 0 || poiType != (int)ViewResultAlgType.POI_XYZ
            || !action.RuntimeResources.TryGet(LocalPoiCalculator.ResultResourceKey(poiId), out LocalPoiResultSet? poi) || poi == null
            || !action.TryGetCurrentFrame(out LocalFlowFrame? frame) || frame == null
            || poi.FrameId != frame.FrameId.ToString("N"))
            throw new InvalidOperationException("IN_POI 必须连接本次本地相机的本地 XYZ 关注点结果，请使用“相机取图 → POI”。");
        if (!TryGetInputMasterResult(action, 1, out int spectrumId, out int spectrumType, out _)
            || spectrumId <= 0 || spectrumType != 300)
            throw new InvalidOperationException("IN_SPE 必须连接本次光谱仪的亮度测量结果。");
        MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByCode(action.SerialNumber)
            ?? throw new InvalidOperationException($"找不到本次流程批次：{action.SerialNumber}");
        using var db = MySqlControl.CreateDbClient();
        SpectumResultEntity spectrum = db.Queryable<SpectumResultEntity>().Where(item => item.Id == spectrumId).First()
            ?? throw new InvalidOperationException("找不到输入的光谱结果。");
        if (spectrum.BatchId != batch.Id || spectrum.DataType || spectrum.fx == null || spectrum.fy == null || spectrum.fPh == null)
            throw new InvalidOperationException("光谱结果必须属于本次流程，且为含 x、y、亮度的测量；不支持光通量/EQE 数据。");
        LocalPoiPointResult point = SpectrumRawCalibrationService.SelectPoint(poi, POIPointName);
        Stopwatch watch = Stopwatch.StartNew();
        lock (frame)
        {
            if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _))
                throw new OperationCanceledException("流程已停止，取消光谱参考校正。");
            ColorCalibrationSnapshot corrected = SpectrumRawCalibrationService.Calculate(frame, point,
                spectrum.fx.Value, spectrum.fy.Value, spectrum.fPh.Value, CorrectionMode, poiId, spectrumId);
            var parameters = new
            {
                SourceFrameId = frame.FrameId.ToString("N"), SourceImageMasterId = frame.MasterId,
                PoiMasterId = poiId, SpectrumMasterId = spectrumId, Correction = corrected.SpectrumCorrection,
                CalibrationTemplate = corrected.Template, OutputBuffer = "RAW", AllowAcceleration = true
            };
            MeasureResultImgModel model = new()
            {
                BatchId = batch.Id, ZIndex = ZIndex, DeviceCode = frame.Metadata.DeviceCode,
                RawFile = frame.CvRawFilePath, FileUrl = frame.CvRawFilePath, FileType = 0,
                Params = JsonConvert.SerializeObject(parameters), ResultCode = 0, Result = "光谱参考校正完成",
                ImgFrameInfo = JsonConvert.SerializeObject(new { rows = frame.Metadata.Height, cols = frame.Metadata.Width,
                    bpp = frame.Metadata.SourceBpp, channels = frame.Metadata.Channels })
            };
            int masterId = SpectrumRawCalibrationService.Save(frame, corrected, () =>
            {
                if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _))
                    throw new OperationCanceledException("流程已停止，取消保存光谱参考校正结果。");
                model.TotalTime = checked((int)Math.Min(watch.ElapsedMilliseconds, int.MaxValue));
                model.CreateDate = DateTime.Now;
                return model.Id = MeasureImgResultDao.Instance.SaveAndReturnId(model);
            });
            action.MasterValue(null, masterId, (int)ViewResultAlgType.Calibration);
            action.SetCurrentFrame(frame);
            ResultMessageBus.Default.Publish(new ResultMessage(ResultRoutes.Camera, ResultKinds.Image, frame.Metadata.DeviceCode,
                OperatorCode, action.SerialNumber, NodeID, ZIndex, new ResultReference(masterId, (int)ViewResultAlgType.Calibration), model));
            return new LocalNodeExecutionResult { Data = new { MasterId = masterId, MasterResultType = (int)ViewResultAlgType.Calibration,
                FrameId = frame.FrameId.ToString("N"), CvRawFilePath = frame.CvRawFilePath, HasCie = false, corrected.SpectrumCorrection } };
        }
    }

    protected override string BuildRunPayload(CVStartCFC action)
        => JsonConvert.SerializeObject(new { action.SerialNumber, POIPointName, CorrectionMode });
}
