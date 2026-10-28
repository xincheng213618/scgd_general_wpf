using ColorVision.Engine.Messages;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates;
using cvColorVision;
using System;
using System.Threading;
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
            if (mode == TakeImageMode.Live) mode = TakeImageMode.Measure_Normal;
            int result = LocalCameraSession.Open(cameraId, mode, bpp);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("本地相机打开失败", result);
            Config.TakeImageMode = mode;
            return null;
        });

        internal MsgRecord CloseLocalCamera() => RunLocalCommand("Close", () =>
        {
            LocalCameraSession.Close(unregisterCallback: true);
            return null;
        });

        internal MsgRecord RunLocalMotorCommand(string eventName, Func<LocalCameraMotorSession, int?> operation)
            => RunLocalCommand(eventName, () => ExecuteLocalMotor(operation));

        internal object? ExecuteLocalMotor(Func<LocalCameraMotorSession, int?> operation)
        {
            var result = LocalCameraSession.UseMotor(motor =>
            {
                int? position = operation(motor);
                return position.HasValue ? new { nPosition = position.Value, VidPos = motor.ConvertVid(position.Value) } : null;
            });
            if (result != null) PublishLocalMotorPosition(result.nPosition, result.VidPos);
            return result;
        }

        private void PublishLocalMotorPosition(int position, double? vidPosition)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (IsDisposed) return;
                Config.MotorConfig.Position = position;
                Config.MotorConfig.VIDPosition = vidPosition ?? 0;
            });
        }

        internal MsgRecord AutoFocusLocally(AutoFocusParam template)
        {
            LocalAutoFocusSettings settings;
            try { settings = LocalAutoFocusSettings.FromTemplate(template, PhyCamera?.Config.MotorConfig ?? Config.MotorConfig); }
            catch (Exception ex) { return RunLocalCommand("AutoFocus", () => throw ex); }
            float exposure = (float)(Config.IsExpThree ? DisplayConfig.ExpTimeR : DisplayConfig.ExpTime);
            float gain = DisplayConfig.Gain;
            return RunLocalCommand("AutoFocus", () =>
            {
                LocalAutoFocusResult result = ExecuteLocalAutoFocus(settings, exposure, gain, showProgress: true);
                LocalCameraCaptureResult capture = LocalCameraCaptureService.Capture(new LocalCameraCaptureRequest
                {
                    Device = this, CameraParameters = BuildLocalCameraParameters(), SaveFiles = false, FlipMode = DisplayConfig.FlipMode
                });
                using (capture.Frame) PublishLocalPreview(capture.Frame, null, forceDisplay: true);
                return new { result.Position, result.Score, result.Samples, result.TotalTime, result.VidPosition };
            });
        }

        internal LocalAutoFocusResult ExecuteLocalAutoFocus(LocalAutoFocusSettings settings, float exposure, float gain, CancellationToken stop = default, bool showProgress = false)
        {
            Action<LocalAutoFocusFrame>? preview = showProgress ? source => PublishLocalFocusPreview(source, exposure, gain) : null;
            LocalAutoFocusResult result = LocalCameraSession.AutoFocus(settings, exposure, gain, stop, preview);
            stop.ThrowIfCancellationRequested();
            PublishLocalMotorPosition(result.Position, result.VidPosition);
            return result;
        }

        private unsafe void PublishLocalFocusPreview(LocalAutoFocusFrame source, float exposure, float gain)
        {
            if (IsDisposed || ExistingView == null) return;
            if (source.Pixels == IntPtr.Zero || source.Width <= 0 || source.Height <= 0 || source.Bits is not (8 or 16) || source.Channels is not (1 or 3))
                throw new InvalidOperationException("自动对焦过程图像格式无效。");
            int length = checked(source.Width * source.Height * source.Channels * (source.Bits / 8));
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = source.Width, Height = source.Height, SourceBpp = source.Bits, Channels = source.Channels,
                Gain = gain, Exposure = source.Channels == 1 ? [exposure] : [exposure, exposure, exposure],
                DeviceCode = Code, PrimaryBufferKind = LocalFrameBufferKind.CvRaw, FlipMode = DisplayConfig.FlipMode
            }, length, 0, LocalCameraSession.RawBufferPool);
            using (LocalFlowFrameLease lease = frame.Acquire()) Buffer.MemoryCopy(source.Pixels.ToPointer(), lease.RawPointer.ToPointer(), lease.RawLength, length);
            // Publish synchronously copies a frozen snapshot before the native evaluation releases its frame.
            PublishLocalPreview(frame, null, forceDisplay: true);
        }

        internal MsgRecord AutoExposeLocally(ParamBase template, CalibrationParam? calibration)
        {
            CameraRunParam parameters = BuildLocalCameraParameters(calibration: calibration);
            string configuration;
            try { configuration = LocalAutoExposureTemplate.BuildConfiguration(template, DisplayConfig.LocalAutoExposureConfig); }
            catch (Exception ex) { return RunLocalCommand("GetAutoExpTime", () => throw ex); }
            return RunLocalCommand("GetAutoExpTime", () =>
            {
                EnsureLocalMeasurementConnected(autoConnect: false);
                return LocalCameraSession.UseOpened(handle =>
                {
                    using LocalFlowFrame frame = LocalCameraAutoExposure.MeasureFrame(this, handle, parameters, configuration);
                    PublishLocalPreview(frame, null, forceDisplay: true);
                    return new
                    {
                        ExpTime = new[] { parameters.ExpTimeR, parameters.ExpTimeG, parameters.ExpTimeB },
                        frame.FrameId, frame.Metadata.Width, frame.Metadata.Height, frame.Metadata.SourceBpp, frame.Metadata.Channels
                    };
                });
            });
        }

        internal MsgRecord CaptureLocally(double[] exposure, CalibrationParam calibration, ParamBase autoExposure, ParamBase hdr)
        {
            CameraRunParam parameters = BuildLocalCameraParameters(exposure, calibration);
            string? configuration;
            try { configuration = autoExposure.Id == -1 ? null : LocalAutoExposureTemplate.BuildConfiguration(autoExposure, DisplayConfig.LocalAutoExposureConfig); }
            catch (Exception ex) { return RunLocalCommand("GetData", () => throw ex); }
            LocalCameraCaptureRequest request = new()
            {
                Device = this, CameraParameters = parameters, Calibration = calibration,
                IsAutoExposure = autoExposure.Id != -1, AutoExposureConfiguration = configuration, SaveFiles = DisplayConfig.SaveLocalCaptureFiles,
                FlipMode = DisplayConfig.FlipMode
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
