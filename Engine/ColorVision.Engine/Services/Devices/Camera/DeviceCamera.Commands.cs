using ColorVision.Engine.Messages;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates;
using cvColorVision;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public partial class DeviceCamera
    {
        // Keep the existing command completion contract for timed buttons, POI and scheduled captures.
        // Local commands deliberately never enter MQTT publishing or its timeout timer.
        internal MsgRecord RunLocalCommand(string eventName, Func<object?> work)
        {
            MsgRecord record = new()
            {
                MsgID = Guid.NewGuid().ToString(), SendTime = DateTime.Now,
                MsgSend = new MsgSend { EventName = eventName, DeviceCode = Code },
                MsgRecordState = MsgRecordState.Sended
            };
            Exception? reservationError = null;
            try { CameraBackend.BeginLocalCommand(eventName == "Close"); }
            catch (Exception ex) { reservationError = ex; }
            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                object? data = null;
                Exception? failure = reservationError;
                try
                {
                    if (failure == null)
                    {
                        ObjectDisposedException.ThrowIf(IsDisposed, this);
                        data = await Task.Run(work);
                    }
                }
                catch (Exception ex) { failure = ex; }
                finally
                {
                    if (reservationError == null) CameraBackend.EndLocalCommand();
                    record.ReciveTime = DateTime.Now;
                    record.MsgReturn = new MsgReturn
                    {
                        MsgID = record.MsgID, EventName = eventName, DeviceCode = Code,
                        Code = failure == null ? 0 : -1, Message = failure?.Message ?? "ok", Data = data!
                    };
                    record.MsgRecordState = failure == null ? MsgRecordState.Success : MsgRecordState.Fail;
                }
            });
            return record;
        }

        internal MsgRecord OpenLocalCamera(string cameraId, TakeImageMode mode, int bpp) => RunLocalCommand("Open", () =>
        {
            if (mode == TakeImageMode.Live) throw new InvalidOperationException("主面板本地取图使用测量模式；Live 预览请使用本地相机管理窗口。");
            int result = LocalCameraSession.Open(cameraId, mode, bpp);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("本地相机打开失败", result);
            return null;
        });

        internal MsgRecord CloseLocalCamera() => RunLocalCommand("Close", () =>
        {
            LocalCameraSession.Close(unregisterCallback: true);
            return null;
        });

        internal MsgRecord AutoExposeLocally() => RunLocalCommand("GetAutoExpTime", () =>
        {
            EnsureLocalMeasurementConnected(autoConnect: false);
            CameraRunParam parameters = BuildLocalCameraParameters();
            LocalCameraSession.UseOpened(handle =>
            {
                LocalCameraAutoExposure.Measure(this, handle, parameters);
                return true;
            });
            return new { ExpTime = new[] { parameters.ExpTimeR, parameters.ExpTimeG, parameters.ExpTimeB } };
        });

        internal MsgRecord CaptureLocally(double[] exposure, CalibrationParam calibration, ParamBase autoExposure, ParamBase hdr)
        {
            CameraRunParam parameters = BuildLocalCameraParameters(exposure, calibration);
            LocalCameraCaptureRequest request = new()
            {
                Device = this, CameraParameters = parameters, Calibration = calibration,
                IsAutoExposure = autoExposure.Id != -1, SaveFiles = DisplayConfig.SaveLocalCaptureFiles,
                SaveCieFile = Config.IsCVCIEFileSave, FlipMode = DisplayConfig.FlipMode
            };
            return RunLocalCommand("GetData", () =>
            {
                if (hdr.Id != -1) throw new NotSupportedException("本地取图尚不支持服务 HDR 模板，请选择空 HDR 模板。");
                EnsureLocalMeasurementConnected(autoConnect: false);
                bool persistResults = MySqlSetting.IsConnect;
                LocalCameraCaptureResult capture = LocalCameraCaptureService.Capture(request);
                using LocalFlowFrame frame = capture.Frame;
                if (!persistResults)
                {
                    PublishLocalPreview(frame, null, forceDisplay: true);
                    return new { MasterId = 0, MasterResultType = 100 };
                }
                MeasureResultImgModel? model = null;
                string serialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                try
                {
                    MeasureBatchModel batch = new() { Code = serialNumber, Name = serialNumber, ArchiveStatus = ArchiveStatus.NotArchived };
                    int batchId = BatchResultMasterDao.Instance.SaveAndReturnId(batch);
                    if (batchId <= 0) throw new InvalidOperationException("保存本地相机批次失败；已采集图像仍可预览。");
                    var result = LocalCameraResultService.CreateModel(batchId, -1, frame, capture, parameters, calibration, request.IsAutoExposure);
                    result.Id = MeasureImgResultDao.Instance.SaveAndReturnId(result);
                    if (result.Id <= 0) throw new InvalidOperationException("保存本地相机结果失败；已采集图像仍可预览。");
                    frame.MasterId = result.Id;
                    model = result;
                }
                finally
                {
                    PublishLocalPreview(frame, model, forceDisplay: true);
                }
                ResultMessageBus.Default.PublishPersisted(ResultRoutes.Camera, ResultKinds.Image, Code, "GetData", serialNumber, string.Empty, -1, model.Id, 100);
                return new { MasterId = model.Id, MasterResultType = 100 };
            });
        }
    }
}
