using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using cvColorVision;
using FlowEngineLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal sealed class LocalCameraCaptureRequest
    {
        public required DeviceCamera Device { get; init; }
        public CameraRunParam? CameraParameters { get; init; }
        public CalibrationParam? Calibration { get; init; }
        public CVImageFlipMode FlipMode { get; init; } = CVImageFlipMode.None;
        public bool IsAutoExposure { get; init; }
        public bool SaveFiles { get; init; }
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
        private static readonly SemaphoreSlim CaptureLock = new(1, 1);
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

        public static LocalCameraCaptureResult Capture(LocalCameraCaptureRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            CaptureLock.Wait();
            try
            {
                return request.Device.LocalCameraSession.UseOpened(handle => CaptureCore(request, handle));
            }
            finally
            {
                CaptureLock.Release();
            }
        }

        private static LocalCameraCaptureResult CaptureCore(LocalCameraCaptureRequest request, IntPtr cameraHandle)
        {
            LocalFrameMirrorService.ValidateFlipMode(request.FlipMode);
            DeviceCamera device = request.Device;
            if (device.Config.TakeImageMode == TakeImageMode.Live)
            {
                throw new InvalidOperationException("本地取图结点不能使用 Live 模式，请先在本地相机窗口中以测量模式连接相机。");
            }

            if (!device.TryGetCalibrationTemplateFiles(request.Calibration, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles, out string? calibrationError))
            {
                throw new InvalidOperationException(calibrationError ?? "校正模板无效。");
            }

            CameraRunParam cameraParameters = request.CameraParameters ?? BuildDefaultCameraParameters(device);
            LocalFlowFrame? frame = null;
            Stopwatch stopwatch = Stopwatch.StartNew();
            int captureTimeMs = 0;
            int calibrationTimeMs = 0;
            int saveTimeMs = 0;
            try
            {
                _ = cvCameraCSLib.CM_SetGain(cameraHandle, cameraParameters.Gain);
                if (!device.Config.IsExpThree) _ = cvCameraCSLib.CM_SetExpTime(cameraHandle, cameraParameters.ExpTime);

                string captureJson = BuildRawCaptureJson(device, cameraParameters, request.IsAutoExposure);
                uint width = 0, height = 0, sourceBpp = 0, channels = 0;
                if (cvCameraCSLib.CM_GetSrcFrameInfo(cameraHandle, ref width, ref height, ref sourceBpp, ref channels) == 0
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
                    PrimaryBufferKind = cieLength > 0 ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw,
                    FlipMode = request.FlipMode,
                    IsMirrorReady = calibrationFiles.Count > 0
                };
                frame = LocalFlowFrame.Allocate(metadata, rawLength, cieLength);

                using (LocalFlowFrameLease lease = frame.Acquire())
                {
                    Stopwatch captureStopwatch = Stopwatch.StartNew();
                    uint destinationBpp = 32;
                    // CM_GetFrame is retained for its CFW, averaging and auto-exposure
                    // acquisition pipeline. BuildRawCaptureJson deliberately supplies no
                    // calibration items; all calibration runs once below on these buffers.
                    int captureResult = cvCameraCSLib.CM_GetFrame(
                        cameraHandle,
                        captureJson,
                        ref width,
                        ref height,
                        ref sourceBpp,
                        ref destinationBpp,
                        ref channels,
                        lease.RawPointer,
                        IntPtr.Zero);
                    captureStopwatch.Stop();
                    captureTimeMs = ToMilliseconds(captureStopwatch.ElapsedMilliseconds);
                    if (captureResult != cvErrorDefine.CV_ERR_SUCCESS)
                    {
                        throw CreateNativeException("本地相机取图失败", captureResult);
                    }

                }

                if (calibrationFiles.Count > 0)
                {
                    Stopwatch calibrationStopwatch = Stopwatch.StartNew();
                    LocalFrameCalibrationService.CalibrateInPlace(
                        frame,
                        device.LocalCalibrationCacheManager,
                        calibrationFiles,
                        request.Calibration?.Name ?? string.Empty);
                    calibrationStopwatch.Stop();
                    calibrationTimeMs = ToMilliseconds(calibrationStopwatch.ElapsedMilliseconds);
                }

                if (request.SaveFiles)
                {
                    Stopwatch saveStopwatch = Stopwatch.StartNew();
                    LocalFrameFileService.SaveCapture(frame, device.Config.FileServerCfg.DataBasePath, device.Code);
                    saveStopwatch.Stop();
                    saveTimeMs = ToMilliseconds(saveStopwatch.ElapsedMilliseconds);
                }

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

        private static string BuildRawCaptureJson(DeviceCamera device, CameraRunParam cameraParameters, bool isAutoExposure)
        {
            int channelCount = device.Config.Channel == ImageChannel.Three ? 3 : 1;
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
                autoExpFlag = isAutoExposure
            };
            IReadOnlyList<(ImageChannelType ChannelType, int CfwPort)> channels = GetChannelConfigs(device, channelCount);
            float[] exposures = GetExposureValues(device, cameraParameters, channelCount);
            for (int index = 0; index < channelCount; index++)
            {
                (ImageChannelType channelType, int cfwPort) = channels[index];
                param.channels.Add(new ChannelParam
                {
                    exp = GetExposureForChannel(device, cameraParameters, channelType, index, exposures),
                    channelType = channelType,
                    cfwport = cfwPort,
                    check = new ChannelCalibration()
                });
            }
            return JsonConvert.SerializeObject(param);
        }

        private static IReadOnlyList<(ImageChannelType ChannelType, int CfwPort)> GetChannelConfigs(DeviceCamera device, int channelCount)
        {
            if (channelCount == 3 && device.Config.CameraMode is CameraMode.BV_MODE or CameraMode.LVTOBV_MODE)
            {
                return ColorFrameChannelOrder;
            }

            List<(ImageChannelType, int)> result = new(channelCount);
            if (device.Config.CFW.ChannelCfgs != null)
            {
                foreach (var channel in device.Config.CFW.ChannelCfgs.Take(channelCount)) result.Add((channel.Chtype, channel.Cfwport));
            }
            if (result.Count < channelCount && device.PhyCamera?.Config.CFW.ChannelCfgs != null)
            {
                foreach (var channel in device.PhyCamera.Config.CFW.ChannelCfgs.Take(channelCount - result.Count)) result.Add((channel.Chtype, channel.Cfwport));
            }
            while (result.Count < channelCount) result.Add(DefaultChannelOrder[Math.Min(result.Count, DefaultChannelOrder.Length - 1)]);
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

        private static InvalidOperationException CreateNativeException(string prefix, int errorCode)
        {
            string message = string.Empty;
            cvCameraCSLib.CM_GetErrorMessage(errorCode, ref message);
            return new InvalidOperationException($"{prefix}：{message} ({errorCode})");
        }
    }
}
