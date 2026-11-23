using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using cvColorVision;
using FlowEngineLib.Algorithm;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using ColorVision.ImageEditor.Tif;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal sealed record LocalCameraCaptureRequest
    {
        public required DeviceCamera Device { get; init; }
        public CameraRunParam? CameraParameters { get; init; }
        public CalibrationParam? Calibration { get; init; }
        public CVImageFlipMode FlipMode { get; init; } = CVImageFlipMode.None;
        public bool IsAutoExposure { get; init; }
        public string? AutoExposureConfiguration { get; init; }
        public LocalAutoFocusSettings? AutoFocus { get; init; }
        public bool SaveFiles { get; init; }
        public bool AllowAcceleration { get; init; }
        public CancellationToken Stop { get; init; }
    }

    internal sealed class LocalCameraCaptureResult
    {
        public required LocalFlowFrame Frame { get; init; }
        public int TotalTimeMs { get; init; }
        public int CaptureTimeMs { get; init; }
        public int CalibrationTimeMs { get; init; }
        public int SaveTimeMs { get; init; }
        public string CalibrationBackend { get; init; } = "None";
    }

    internal static class LocalCameraCaptureService
    {
        internal delegate int RawFrameCapture(IntPtr handle, string captureJson, ref uint width, ref uint height,
            ref uint sourceBpp, ref uint destinationBpp, ref uint channels, IntPtr raw, IntPtr cie);

        private static readonly SemaphoreSlim CaptureLock = new(1, 1);
        private static readonly uint RawFrameLeaseSize = (uint)Marshal.SizeOf<CameraRawFrameLeaseV1>();
        private static readonly (ImageChannelType ChannelType, int CfwPort)[] DefaultChannelOrder =
        {
            (ImageChannelType.Gray_Y, 0),
            (ImageChannelType.Gray_X, 1),
            (ImageChannelType.Gray_Z, 2),
        };
        private static readonly (ImageChannelType ChannelType, int CfwPort)[] ColorFrameChannelOrder =
        {
            (ImageChannelType.Gray_X, 0),
            (ImageChannelType.Gray_Y, 1),
            (ImageChannelType.Gray_Z, 2),
        };

        public static LocalCameraCaptureResult Capture(LocalCameraCaptureRequest request, bool autoConnect = false)
        {
            ArgumentNullException.ThrowIfNull(request);
            DeviceCamera device = request.Device;
            object sessionLock = device.LocalCameraSession.SyncRoot;
            while (true)
            {
                FlowNodeTiming.Run("WaitCamera", () => CaptureLock.Wait(request.Stop));
                bool sessionEntered = false;
                try
                {
                    Monitor.TryEnter(sessionLock, ref sessionEntered);
                    if (sessionEntered)
                    {
                        request.Stop.ThrowIfCancellationRequested();
                        var captureChannels = GetChannelConfigs(device, device.Config.Channel == ImageChannel.Three ? 3 : 1);
                        FlowNodeTiming.Run("ConnectCamera", () => device.EnsureLocalMeasurementConnected(autoConnect));
                        request.Stop.ThrowIfCancellationRequested();
                        return FlowNodeTiming.Run("CapturePipeline", () => device.LocalCameraSession.UseOpened(handle => CaptureCore(request, handle, captureChannels)));
                    }
                }
                finally
                {
                    if (sessionEntered) Monitor.Exit(sessionLock);
                    CaptureLock.Release();
                }

                // A manual operation owns only the session. Wait for it without blocking other cameras.
                FlowNodeTiming.Run("WaitCameraSession", () => WaitForSession(sessionLock, request.Stop));
            }
        }

        private static void WaitForSession(object sessionLock, CancellationToken stop)
        {
            stop.ThrowIfCancellationRequested();
            // A Monitor cannot wait on a token, so check for cancellation between timed waits.
            int timeout = stop.CanBeCanceled ? 50 : Timeout.Infinite;
            while (!Monitor.TryEnter(sessionLock, timeout)) stop.ThrowIfCancellationRequested();
            Monitor.Exit(sessionLock);
        }

        private static LocalCameraCaptureResult CaptureCore(LocalCameraCaptureRequest request, IntPtr cameraHandle,
            IReadOnlyList<(ImageChannelType ChannelType, int CfwPort)> captureChannels)
        {
            request.Stop.ThrowIfCancellationRequested();
            LocalFrameMirrorService.ValidateFlipMode(request.FlipMode);
            DeviceCamera device = request.Device;
            if (device.LocalCameraSession.OpenedMode == TakeImageMode.Live)
            {
                throw new InvalidOperationException("本地取图结点不能使用 Live 模式，请先在本地相机窗口中以测量模式连接相机。");
            }

            if (!device.TryGetCalibrationTemplateFiles(request.Calibration, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles, out string? calibrationError))
            {
                throw new InvalidOperationException(calibrationError ?? "校正模板无效。");
            }

            CameraRunParam cameraParameters = request.CameraParameters ?? BuildDefaultCameraParameters(device);
            if (!float.IsFinite(cameraParameters.Gain) || cameraParameters.Gain < 0 || cameraParameters.AvgCount < 1)
                throw new InvalidOperationException("增益必须为非负有限值，平均次数至少为 1。");
            foreach (float exposure in GetExposureValues(device, cameraParameters, device.Config.IsExpThree ? 3 : 1))
                if (!float.IsFinite(exposure) || exposure <= 0) throw new InvalidOperationException("曝光时间必须为大于 0 的有限值。");
            LocalFlowFrame? frame = null;
            Stopwatch stopwatch = Stopwatch.StartNew();
            int captureTimeMs = 0;
            int calibrationTimeMs = 0;
            int saveTimeMs = 0;
            try
            {
                if (request.IsAutoExposure)
                {
                    FlowNodeTiming.Run("AutoExposure", () => LocalCameraAutoExposure.Measure(device, cameraHandle, cameraParameters, request.AutoExposureConfiguration, captureChannels));
                }
                else
                {
                    int gainResult = FlowNodeTiming.Run("SetGain", () => cvCameraCSLib.CM_SetGain(cameraHandle, cameraParameters.Gain));
                    if (gainResult != cvErrorDefine.CV_ERR_SUCCESS) throw CreateNativeException("本地相机设置增益失败", gainResult);
                    if (!device.Config.IsExpThree)
                    {
                        int exposureResult = FlowNodeTiming.Run("SetExposure", () => cvCameraCSLib.CM_SetExpTime(cameraHandle, cameraParameters.ExpTime));
                        if (exposureResult != cvErrorDefine.CV_ERR_SUCCESS) throw CreateNativeException("本地相机设置曝光失败", exposureResult);
                    }
                    FlowNodeTiming.Skip("AutoExposure");
                }
                if (request.AutoFocus is { } focus)
                {
                    float focusExposure = device.Config.IsExpThree ? cameraParameters.ExpTimeR : cameraParameters.ExpTime;
                    FlowNodeTiming.Run("AutoFocus", () => device.LocalCameraSession.AutoFocus(focus, focusExposure, cameraParameters.Gain, request.Stop, includeVid: false));
                    request.Stop.ThrowIfCancellationRequested();
                }
                string captureJson = BuildRawCaptureJson(device, cameraParameters, captureChannels);
                uint width = 0, height = 0, sourceBpp = 0, channels = 0;
                uint sourceInfoResult;
                using (var sourceInfoStage = FlowNodeTiming.Measure("GetSourceFrameInfo"))
                {
                    sourceInfoResult = cvCameraCSLib.CM_GetSrcFrameInfo(cameraHandle, ref width, ref height, ref sourceBpp, ref channels);
                    if (sourceInfoResult != 0 && width > 0 && height > 0 && sourceBpp > 0 && channels > 0) sourceInfoStage?.Complete();
                }
                if (sourceInfoResult == 0
                    || width == 0 || height == 0 || sourceBpp == 0 || channels == 0)
                {
                    throw new InvalidOperationException("本地相机没有返回有效的源图尺寸。");
                }

                int rawLength = checked((int)((ulong)(sourceBpp / 8) * width * height * channels));
                int cieLength = calibrationFiles.Count == 0
                    ? 0
                    : LocalFrameCalibrationService.GetRequiredCieLength(
                        checked((int)width),
                        checked((int)height),
                        checked((int)channels),
                        calibrationFiles,
                        request.Calibration?.Name ?? string.Empty);
                if (request.AllowAcceleration) cieLength = 0;
                float[] exposure = GetExposureValues(device, cameraParameters, (int)channels);
                LocalFrameMetadata metadata = new()
                {
                    Width = (int)width,
                    Height = (int)height,
                    SourceBpp = (int)sourceBpp,
                    CieBpp = 32,
                    Channels = (int)channels,
                    Gain = cameraParameters.Gain,
                    Exposure = exposure,
                    DeviceCode = device.Code,
                    CalibrationTemplate = request.Calibration?.Name ?? string.Empty,
                    CaptureTime = DateTime.Now,
                    Acquisition = new ImageAcquisitionRecord
                    {
                        OpticalCenter = device.PhyCamera?.OpticalCenter,
                        SensorRoi = device.LocalCameraSession.OpenedCalibrationRoi.ToAcquisitionRoi(),
                        Camera = new ImageCameraInformation
                        {
                            DeviceName = device.Config.Name, DeviceCode = device.Code,
                            CameraId = device.LocalCameraSession.OpenedCameraId, CameraCode = device.Config.CameraCode,
                            Model = device.Config.CameraModel.ToString(), Mode = device.Config.CameraMode.ToString()
                        },
                        CapturedUtc = DateTime.UtcNow, Width = (int)width, Height = (int)height,
                        BitsPerChannel = (int)sourceBpp, Channels = (int)channels, Gain = cameraParameters.Gain,
                        Exposure = (float[])exposure.Clone(), ExposureUnit = "ms", NdPort = device.Config.NDPort,
                        AverageCount = cameraParameters.AvgCount,
                        ChannelOrder = channels == 1 ? "Y" : device.Config.IsExpThree ? "X/Y/Z" : "R/G/B"
                    },
                    PrimaryBufferKind = cieLength > 0 ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw,
                    FlipMode = request.FlipMode,
                    IsMirrorReady = calibrationFiles.Count > 0
                };
                if (sourceBpp == 16 && channels == 3 && device.LocalCameraSession.OpenedMode == TakeImageMode.Measure_Normal
                    && cvCameraCSLib.TryGetRawFrameLeaseV1(out var acquireRaw, out var releaseRaw))
                {
                    frame = TryCaptureOwnedRawFrame(cameraHandle, captureJson, metadata, rawLength, cieLength,
                        acquireRaw, releaseRaw, request.Stop, out captureTimeMs);
                }
                if (frame == null)
                {
                    frame = FlowNodeTiming.Run("AllocateFrame", () => LocalFlowFrame.Allocate(metadata, rawLength, cieLength, device.LocalCameraSession.RawBufferPool));
                    using LocalFlowFrameLease lease = frame.Acquire();
                    Stopwatch captureStopwatch = Stopwatch.StartNew();
                    using var captureStage = FlowNodeTiming.Measure("CaptureFrame");
                    CaptureRawFrame(cameraHandle, captureJson, lease, cvCameraCSLib.CM_GetFrame, request.Stop);
                    captureStopwatch.Stop();
                    captureTimeMs = ToMilliseconds(captureStopwatch.ElapsedMilliseconds);
                    captureStage?.Complete();
                }

                if (calibrationFiles.Count > 0)
                {
                    Stopwatch calibrationStopwatch = Stopwatch.StartNew();
                    LocalFrameCalibrationService.CalibrateInPlace(
                        frame,
                        device.LocalCalibrationCacheManager,
                        calibrationFiles,
                        request.Calibration?.Name ?? string.Empty,
                        LocalCalibrationRoi.ResolveForFrame(frame.Metadata, cameraConfig: null),
                        allowAcceleration: request.AllowAcceleration);
                    calibrationStopwatch.Stop();
                    calibrationTimeMs = ToMilliseconds(calibrationStopwatch.ElapsedMilliseconds);
                }
                else FlowNodeTiming.Skip("Calibration");

                request.Stop.ThrowIfCancellationRequested();
                if (request.SaveFiles)
                {
                    Stopwatch saveStopwatch = Stopwatch.StartNew();
                    LocalFrameFileService.SaveCapture(frame, LocalFrameFileService.CreateCapturePath(device.Config.FileServerCfg.DataBasePath, device.Code, frame.Metadata));
                    saveStopwatch.Stop();
                    saveTimeMs = ToMilliseconds(saveStopwatch.ElapsedMilliseconds);
                }
                else FlowNodeTiming.Skip("SaveImage");

                stopwatch.Stop();
                LocalFlowFrame completedFrame = frame;
                frame = null;
                return new LocalCameraCaptureResult
                {
                    Frame = completedFrame,
                    TotalTimeMs = ToMilliseconds(stopwatch.ElapsedMilliseconds),
                    CaptureTimeMs = captureTimeMs,
                    CalibrationTimeMs = calibrationTimeMs,
                    SaveTimeMs = saveTimeMs,
                    CalibrationBackend = calibrationFiles.Count == 0
                        ? "None"
                        : device.LocalCalibrationCacheManager.BackendName
                };
            }
            catch
            {
                frame?.Dispose();
                throw;
            }
        }

        internal static LocalFlowFrame? TryCaptureOwnedRawFrame(IntPtr cameraHandle, string captureJson,
            LocalFrameMetadata metadata, int rawLength, int cieLength,
            cvCameraCSLib.AcquireRawFrameV1 acquire, cvCameraCSLib.ReleaseRawFrameV1 release,
            CancellationToken stop, out int captureTimeMs)
        {
            captureTimeMs = 0;
            stop.ThrowIfCancellationRequested();
            CameraRawFrameOwner? owner = new(release);
            try
            {
                CameraRawFrameLeaseV1 descriptor = new() { StructSize = RawFrameLeaseSize };
                Stopwatch stopwatch = Stopwatch.StartNew();
                using var captureStage = FlowNodeTiming.Measure("CaptureRawFrameLease");
                int result;
                try
                {
                    result = acquire(cameraHandle, captureJson, ref descriptor);
                }
                finally
                {
                    owner.Attach(descriptor.Owner);
                }

                // The native owner is held even if a stop or format change is
                // reported just after the camera returns.
                stop.ThrowIfCancellationRequested();
                if (result == CameraRawFrameLeaseV1.UnsupportedBeforeCapture)
                {
                    if (descriptor.StructSize != RawFrameLeaseSize || descriptor.Owner != IntPtr.Zero
                        || descriptor.Pixels != IntPtr.Zero || descriptor.ByteLength != 0 || descriptor.Format != 0
                        || descriptor.Width != 0 || descriptor.Height != 0 || descriptor.BitsPerChannel != 0 || descriptor.Channels != 0)
                        throw new InvalidOperationException("本地相机返回了无效的 RAW 采集回退结果。");
                    captureStage?.Complete();
                    return null;
                }
                if (result != cvErrorDefine.CV_ERR_SUCCESS)
                    throw CreateNativeException("本地相机取图失败", result);
                ulong address = unchecked((ulong)descriptor.Pixels.ToInt64());
                if (descriptor.StructSize != RawFrameLeaseSize || owner.IsInvalid
                    || address == 0 || (address & 1) != 0 || address > ulong.MaxValue - descriptor.ByteLength
                    || descriptor.Format != CameraRawFrameLeaseV1.PackedBgr16
                    || descriptor.Width != metadata.Width || descriptor.Height != metadata.Height
                    || descriptor.BitsPerChannel != metadata.SourceBpp || descriptor.Channels != metadata.Channels
                    || descriptor.ByteLength != (ulong)rawLength)
                {
                    throw new InvalidOperationException("本地相机返回的 RAW 缓冲格式与预计规格不一致。请重新连接相机。");
                }

                stopwatch.Stop();
                captureTimeMs = ToMilliseconds(stopwatch.ElapsedMilliseconds);
                captureStage?.Complete();
                LocalFlowFrame frame = LocalFlowFrame.AdoptCameraRaw(metadata, rawLength, cieLength, descriptor.Pixels, owner);
                owner = null;
                return frame;
            }
            finally
            {
                owner?.Dispose();
            }
        }

        private sealed class CameraRawFrameOwner(cvCameraCSLib.ReleaseRawFrameV1 release)
            : SafeHandleZeroOrMinusOneIsInvalid(ownsHandle: true)
        {
            public void Attach(IntPtr owner) => SetHandle(owner);

            protected override bool ReleaseHandle()
            {
                release(handle);
                return true;
            }
        }

        internal static void CaptureRawFrame(IntPtr cameraHandle, string captureJson, LocalFlowFrameLease lease, RawFrameCapture capture, CancellationToken stop = default)
        {
            LocalFrameMetadata metadata = lease.Metadata;
            uint width = checked((uint)metadata.Width), height = checked((uint)metadata.Height);
            uint sourceBpp = checked((uint)metadata.SourceBpp), channels = checked((uint)metadata.Channels);
            uint destinationBpp = 32;
            stop.ThrowIfCancellationRequested();
            int result = capture(cameraHandle, captureJson, ref width, ref height, ref sourceBpp, ref destinationBpp, ref channels, lease.RawPointer, IntPtr.Zero);
            stop.ThrowIfCancellationRequested();
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw CreateNativeException("本地相机取图失败", result);
            if (width != metadata.Width || height != metadata.Height || sourceBpp != metadata.SourceBpp || channels != metadata.Channels)
            {
                throw new InvalidOperationException($"本地相机返回的图像格式发生变化。预期 {metadata.Width}×{metadata.Height}、{metadata.SourceBpp} 位、{metadata.Channels} 通道，实际 {width}×{height}、{sourceBpp} 位、{channels} 通道。请重新连接相机。");
            }
        }

        private static CameraRunParam BuildDefaultCameraParameters(DeviceCamera device)
        {
            return new CameraRunParam
            {
                Gain = device.DisplayConfig.Gain,
                ExpTime = (float)device.DisplayConfig.ExpTime,
                ExpTimeR = (float)device.DisplayConfig.ExpTimeR,
                ExpTimeG = (float)device.DisplayConfig.ExpTimeG,
                ExpTimeB = (float)device.DisplayConfig.ExpTimeB,
                AvgCount = 1
            };
        }

        private static string BuildRawCaptureJson(DeviceCamera device, CameraRunParam cameraParameters,
            IReadOnlyList<(ImageChannelType ChannelType, int CfwPort)> channels)
        {
            int channelCount = channels.Count;
            GetFrameParam param = new()
            {
                channelCount = channelCount,
                measureCount = Math.Max(cameraParameters.AvgCount, 1),
                title = string.Empty,
                ob = 4,
                obR = 0,
                obT = 0,
                obB = 0,
                startBurst = 1,
                endBurst = 3,
                posBurst = 0,
                autoExpFlag = false
            };
            // An empty list selects packed color RAW in cvCamera.dll.
            if (channelCount == 3 && device.Config.CameraMode is CameraMode.BV_MODE or CameraMode.LVTOBV_MODE)
                param.calibrationlist = Array.Empty<object>();
            float[] exposures = GetExposureValues(device, cameraParameters, channelCount);
            for (int index = 0; index < channelCount; index++)
            {
                (ImageChannelType channelType, int cfwPort) = channels[index];
                param.channels.Add(new ChannelParam
                {
                    exp = GetExposureForChannel(device, cameraParameters, channelType, index, exposures),
                    channelType = channelType,
                    cfwport = cfwPort
                });
            }
            return JsonConvert.SerializeObject(param);
        }

        internal static IReadOnlyList<(ImageChannelType ChannelType, int CfwPort)> GetChannelConfigs(DeviceCamera device, int channelCount)
        {
            if (channelCount == 3 && device.Config.CameraMode is CameraMode.BV_MODE or CameraMode.LVTOBV_MODE)
            {
                return ColorFrameChannelOrder;
            }

            List<(ImageChannelType ChannelType, int CfwPort)> result = new(channelCount);
            if (device.Config.CFW.ChannelCfgs != null)
            {
                foreach (var channel in device.Config.CFW.ChannelCfgs.Take(channelCount)) result.Add((channel.Chtype, channel.Cfwport));
            }
            if (result.Count < channelCount && device.PhyCamera?.Config.CFW.ChannelCfgs != null)
            {
                foreach (var channel in device.PhyCamera.Config.CFW.ChannelCfgs.Take(channelCount - result.Count)) result.Add((channel.Chtype, channel.Cfwport));
            }
            while (result.Count < channelCount) result.Add(DefaultChannelOrder[Math.Min(result.Count, DefaultChannelOrder.Length - 1)]);

            // CM_GetFrame can return capture success even when the native RAW copy skips a route missing X, Y or Z.
            if (channelCount == 3 && (result.Select(channel => channel.ChannelType).Distinct().Count() != 3
                || result.Any(channel => channel.ChannelType is not (ImageChannelType.Gray_X or ImageChannelType.Gray_Y or ImageChannelType.Gray_Z))))
                throw new InvalidOperationException("本地三通道取图需要各一个 X、Y、Z 通道，请检查滤轮通道配置。");
            return result;
        }

        private static float[] GetExposureValues(DeviceCamera device, CameraRunParam cameraParameters, int channelCount)
        {
            if (!device.Config.IsExpThree) return Enumerable.Repeat(cameraParameters.ExpTime, Math.Max(channelCount, 1)).ToArray();
            float[] values = { cameraParameters.ExpTimeR, cameraParameters.ExpTimeG, cameraParameters.ExpTimeB };
            return values.Take(Math.Max(channelCount, 1)).ToArray();
        }

        private static float GetExposureForChannel(DeviceCamera device, CameraRunParam cameraParameters, ImageChannelType channelType, int index, float[] fallback)
        {
            if (!device.Config.IsExpThree) return fallback[Math.Min(index, fallback.Length - 1)];
            return channelType switch
            {
                ImageChannelType.Gray_X => cameraParameters.ExpTimeR,
                ImageChannelType.Gray_Y => cameraParameters.ExpTimeG,
                ImageChannelType.Gray_Z => cameraParameters.ExpTimeB,
                _ => fallback[Math.Min(index, fallback.Length - 1)]
            };
        }

        private static int ToMilliseconds(long value)
            => checked((int)Math.Min(value, int.MaxValue));

        internal static InvalidOperationException CreateNativeException(string prefix, int errorCode)
        {
            string message = string.Empty;
            cvCameraCSLib.CM_GetErrorMessage(errorCode, ref message);
            return new InvalidOperationException($"{prefix}：{message} ({errorCode})");
        }
    }
}
