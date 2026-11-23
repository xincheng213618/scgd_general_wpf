using ColorVision.Engine.Messages;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public partial class DeviceCamera
    {
        internal const int LocalAutoFocusCanceledCode = -2;

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
            bool closing = eventName == "Close";
            Task? pendingCommand = null;
            try
            {
                if (closing) pendingCommand = CameraBackend.BeginLocalClose();
                else CameraBackend.BeginLocalCommand();
            }
            catch (Exception ex) { reservationError = ex; }
            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                object? data = null;
                Exception? failure = reservationError;
                try
                {
                    if (failure == null)
                    {
                        if (pendingCommand != null) await pendingCommand;
                        ObjectDisposedException.ThrowIf(IsDisposed, this);
                        data = await Task.Run(work);
                    }
                }
                catch (Exception ex) { failure = ex; }
                finally
                {
                    if (reservationError == null)
                    {
                        if (closing) CameraBackend.EndLocalClose();
                        else CameraBackend.EndLocalCommand();
                    }
                    bool canceled = eventName == "AutoFocus" && failure is OperationCanceledException;
                    record.ReciveTime = DateTime.Now;
                    record.MsgReturn = new MsgReturn
                    {
                        MsgID = record.MsgID, EventName = eventName, DeviceCode = Code,
                        Code = failure == null ? 0 : canceled ? LocalAutoFocusCanceledCode : -1,
                        Message = canceled ? EngineLocalization.Get("CameraPanel_AutoFocusStopped") : failure?.Message ?? "ok", Data = data!
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

        internal MsgRecord MoveMotorAndCaptureLocally(int target, bool absolute) => RunLocalCommand("Move", () =>
        {
            var (position, vid) = LocalCameraSession.UseMotor(motor =>
            {
                int reached = motor.Move(target, absolute);
                return (reached, motor.ConvertVid(reached));
            });
            return CompleteMotorMoveWithCapture(position, vid);
        });

        internal MsgRecord MoveToVidLocally(double targetMm) => RunLocalCommand("Move", () =>
        {
            var (position, vid) = LocalCameraSession.UseMotor(motor => motor.MoveToVid(targetMm));
            return CompleteMotorMoveWithCapture(position, vid);
        });

        private object CompleteMotorMoveWithCapture(int position, double? vid)
        {
            PublishLocalMotorPosition(position, vid);
            try
            {
                LocalCameraCaptureResult capture = LocalCameraCaptureService.Capture(new LocalCameraCaptureRequest
                {
                    Device = this, CameraParameters = BuildLocalCameraParameters(), SaveFiles = false, FlipMode = DisplayConfig.FlipMode
                });
                using (capture.Frame) PublishLocalPreview(capture.Frame, null, forceDisplay: true);
            }
            catch (Exception ex)
            {
                MQTTServiceBase.log.Warn($"[LocalMotor] Camera={Code} Position={position} capture after motor move failed.", ex);
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    EngineLocalization.Get("CameraPanel_MotorMoveCaptureFailed"), position, ex.Message), ex);
            }
            return new { nPosition = position, VidPos = vid };
        }

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

        internal MsgRecord AutoFocusLocally(AutoFocusParam template, CancellationToken stop = default, bool fineOnly = false) =>
            RunLocalAutoFocus(template, stop, fineOnly, captureEvidence: false);

        /// <summary>Runs local autofocus and returns a detached <see cref="CameraAutoFocusEvidence"/> in the completed command.</summary>
        public MsgRecord AutoFocusLocallyWithEvidence(AutoFocusParam template, CancellationToken stop, bool fineOnly) =>
            RunLocalAutoFocus(template, stop, fineOnly, captureEvidence: true);

        private MsgRecord RunLocalAutoFocus(AutoFocusParam template, CancellationToken stop, bool fineOnly, bool captureEvidence)
        {
            LocalAutoFocusSettings settings;
            try { settings = LocalAutoFocusSettings.FromTemplate(template, PhyCamera?.Config.MotorConfig ?? Config.MotorConfig) with { FineOnly = fineOnly }; }
            catch (Exception ex) { return RunLocalCommand("AutoFocus", () => throw ex); }
            float exposure = (float)(Config.CameraMode == CameraMode.CV_MODE ? DisplayConfig.ExpTimeR : DisplayConfig.ExpTime);
            float gain = DisplayConfig.Gain;
            bool saveResult = !captureEvidence && DisplayConfig.SaveLocalAutoFocusChart;
            string saveDirectory = DisplayConfig.LocalAutoFocusChartDirectory;
            MQTTServiceBase.log.Info($"[LocalAutoFocus] Template Name={template.Name} Id={template.Id}");
            return RunLocalCommand("AutoFocus", () =>
            {
                OrderedDictionary<int, double>? curve = captureEvidence || saveResult ? new() : null;
                LocalAutoFocusResult result = ExecuteLocalAutoFocus(settings, exposure, gain, stop, showProgress: true, evidenceSamples: curve);
                stop.ThrowIfCancellationRequested();
                LocalCameraCaptureResult capture = LocalCameraCaptureService.Capture(new LocalCameraCaptureRequest
                {
                    Device = this, CameraParameters = BuildLocalCameraParameters(), SaveFiles = false, FlipMode = DisplayConfig.FlipMode, Stop = stop
                });
                CameraAutoFocusEvidence? evidence = null;
                using (capture.Frame)
                {
                    stop.ThrowIfCancellationRequested();
                    PublishLocalPreview(capture.Frame, null, forceDisplay: true, focusFrame: true);
                    if (curve != null)
                    {
                        try
                        {
                            evidence = new CameraAutoFocusEvidence(LocalCameraPreview.CreateBitmap(capture.Frame), result, curve,
                                Code, settings.Evaluation.ToString());
                        }
                        catch (Exception ex)
                        {
                            MQTTServiceBase.log.Warn($"[LocalAutoFocus] Camera={Code} evidence image could not be prepared.", ex);
                        }
                    }
                }
                if (evidence != null)
                {
                    if (captureEvidence) return evidence;
                    try
                    {
                        string path = evidence.SaveLocalResult(saveDirectory, DateTime.Now, stop);
                        MQTTServiceBase.log.Info($"[LocalAutoFocus] Result saved: {path}");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        MQTTServiceBase.log.Warn($"[LocalAutoFocus] Camera={Code} result could not be saved.", ex);
                    }
                }
                return new { Position = result.MotorPosition, result.Score, result.Samples, result.TotalTime, result.VidPosition };
            });
        }

        internal LocalAutoFocusResult ExecuteLocalAutoFocus(LocalAutoFocusSettings settings, float exposure, float gain,
            CancellationToken stop = default, bool showProgress = false, IDictionary<int, double>? evidenceSamples = null)
        {
            settings = settings with { UseMotorReadbackForVid = DisplayConfig.UseMotorReadbackForVid };
            Action<LocalAutoFocusFrame>? preview = showProgress ? source =>
            {
                if (!stop.IsCancellationRequested) PublishLocalFocusPreview(source, exposure, gain);
            } : null;
            string evaluation = settings.Evaluation.ToString();
            if (showProgress) DispatchFocusView(view => view.FocusChart.BeginAutoFocus(evaluation));
            try
            {
                LocalAutoFocusResult result = LocalCameraSession.AutoFocus(settings, exposure, gain, stop, preview, (position, vid, score) =>
                {
                    if (!score.HasValue) PublishLocalMotorPosition(position, vid);
                    else
                    {
                        if (evidenceSamples != null) evidenceSamples[position] = score.Value;
                        if (showProgress) DispatchFocusView(view => view.FocusChart.AddAutoFocusSample(position, score.Value));
                    }
                });
                stop.ThrowIfCancellationRequested();
                if (showProgress) DispatchFocusView(view => view.FocusChart.CompleteAutoFocus(result));
                return result;
            }
            catch (OperationCanceledException)
            {
                if (showProgress) DispatchFocusView(view => view.FocusChart.EndAutoFocus(canceled: true));
                throw;
            }
            catch
            {
                if (showProgress) DispatchFocusView(view => view.FocusChart.EndAutoFocus(canceled: false));
                throw;
            }
        }

        private void DispatchFocusView(Action<ViewCamera> update)
        {
            ViewCamera? view = ExistingView;
            if (IsDisposed || view == null || !_view.IsVisible) return;
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (IsDisposed || !_view.IsVisible || !ReferenceEquals(ExistingView, view)) return;
                view.EnsureInitialized();
                update(view);
            });
        }

        internal void PublishLocalFocusPreview(LocalAutoFocusFrame source, float exposure, float gain)
        {
            if (IsDisposed || ExistingView == null) return;
            if (source.Pixels == IntPtr.Zero || source.Width <= 0 || source.Height <= 0 || source.Bits is not (8 or 16) || source.Channels is not (1 or 3))
                throw new InvalidOperationException("自动对焦过程图像格式无效。");
            int length = checked(source.Width * source.Height * source.Channels * (source.Bits / 8));
            LocalFrameMetadata metadata = new()
            {
                Width = source.Width, Height = source.Height, SourceBpp = source.Bits, Channels = source.Channels,
                Gain = gain, Exposure = source.Channels == 1 ? [exposure] : [exposure, exposure, exposure],
                DeviceCode = Code, PrimaryBufferKind = LocalFrameBufferKind.CvRaw, FlipMode = DisplayConfig.FlipMode
            };
            PublishLocalPreview((Pixels: source.Pixels, Length: length, Metadata: metadata),
                static raw => LocalCameraPreview.CreateFromBorrowedRaw(raw.Pixels, raw.Length, raw.Metadata),
                null, forceDisplay: true, focusFrame: true);
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

        internal MsgRecord CaptureLocally(double[] exposure, CalibrationParam calibration, ParamBase autoExposure,
            Func<LocalCameraCaptureRequest, LocalCameraCaptureResult>? captureImage = null)
        {
            MySqlConnectedEndpoint? endpoint = ResultDatabaseEndpoint;
            if (!OwnsResultDatabase(endpoint)) endpoint = null;
            bool connectedWithoutOwnership = endpoint == null && MySqlControl.GetInstance().ConnectedEndpoint is { IsCurrent: true };

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
                EnsureLocalMeasurementConnected(autoConnect: false);
                LocalCameraCaptureResult capture = captureImage == null ? LocalCameraCaptureService.Capture(request) : captureImage(request);
                using LocalFlowFrame frame = capture.Frame;
                MeasureResultImgModel? model = null;
                string serialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                try
                {
                    if (connectedWithoutOwnership)
                        throw new InvalidOperationException("本地相机未绑定当前数据库，请重新选择相机后取图。");
                    if (endpoint == null)
                        return new { MasterId = 0, MasterResultType = 100 };

                    model = LocalCameraResultService.SaveInteractiveModel(endpoint, serialNumber, capture, parameters, calibration, request.IsAutoExposure);
                    frame.MasterId = model.Id;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"保存本地相机结果失败；已采集图像仍可预览。{ex.Message}", ex);
                }
                finally
                {
                    // Live pixels can still be shown when the capture no longer belongs to the visible history.
                    bool showHistory = model != null && OwnsResultDatabase(endpoint);
                    PublishLocalPreview(frame, showHistory ? model : null, forceDisplay: true, databaseEndpoint: showHistory ? endpoint : null);
                }
                ResultMessageBus.Default.PublishPersisted(ResultRoutes.Camera, ResultKinds.Image, Code, "GetData", serialNumber, string.Empty, -1, model.Id, 100, endpoint);
                return new { MasterId = model.Id, MasterResultType = 100 };
            });
        }
    }
}
