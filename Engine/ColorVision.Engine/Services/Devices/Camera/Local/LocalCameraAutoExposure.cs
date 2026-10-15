using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.UI;
using cvColorVision;
using FlowEngineLib.Algorithm;
using System;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal static class LocalCameraAutoExposure
    {
        internal static void Measure(DeviceCamera device, IntPtr handle, CameraRunParam parameters, string? configuration = null)
        {
            Prepare(device, handle, parameters, configuration);
            float[] exposure = new float[3];
            float[] saturation = new float[3];
            int result = cvCameraCSLib.CM_GetAutoExpTime(handle, exposure, saturation);
            if (result != cvErrorDefine.CV_ERR_SUCCESS)
                throw LocalCameraCaptureService.CreateNativeException("本地自动曝光失败", result);
            ApplyExposure(parameters, exposure, device.Config.IsExpThree);
            UpdateDisplay(device, parameters, saturation);
        }

        // Own the returned frame until the caller has copied/published its preview.
        internal static LocalFlowFrame MeasureFrame(DeviceCamera device, IntPtr handle, CameraRunParam parameters, string? configuration = null)
        {
            Prepare(device, handle, parameters, configuration);
            uint width = 0, height = 0, bpp = 0, channels = 0;
            int infoResult = unchecked((int)cvCameraCSLib.CM_GetSrcFrameInfo(handle, ref width, ref height, ref bpp, ref channels));
            if (infoResult != cvErrorDefine.CV_ERR_SUCCESS)
                throw LocalCameraCaptureService.CreateNativeException("本地自动曝光查询图像尺寸失败", infoResult);
            if (width == 0 || height == 0 || bpp is not (8 or 16) || channels is not (1 or 3))
                throw new InvalidOperationException("本地自动曝光没有返回有效的图像尺寸或格式。");
            int length = checked((int)((ulong)width * height * channels * (bpp / 8)));
            LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = checked((int)width), Height = checked((int)height), SourceBpp = (int)bpp, Channels = (int)channels,
                Gain = parameters.Gain, DeviceCode = device.Code, PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
                FlipMode = device.DisplayConfig.FlipMode, IsMirrorReady = true
            }, length, 0, device.LocalCameraSession.RawBufferPool);
            try
            {
                float[] exposure = new float[3], saturation = new float[3];
                using (LocalFlowFrameLease lease = frame.Acquire())
                {
                    int result = cvCameraCSLib.CM_GetAutoExpFrame(handle, ref width, ref height, ref bpp, ref channels,
                        lease.RawPointer, checked((ulong)lease.RawLength), exposure, saturation);
                    if (result != cvErrorDefine.CV_ERR_SUCCESS)
                        throw LocalCameraCaptureService.CreateNativeException("本地自动曝光取图失败", result);
                }
                if (width != frame.Metadata.Width || height != frame.Metadata.Height
                    || bpp != frame.Metadata.SourceBpp || channels != frame.Metadata.Channels)
                    throw new InvalidOperationException("本地自动曝光返回的图像格式发生变化。");
                ApplyFrameResult(frame, parameters, exposure, saturation, device.Config.IsExpThree);
                UpdateDisplay(device, parameters, saturation);
                return frame;
            }
            catch { frame.Dispose(); throw; }
        }

        private static void Prepare(DeviceCamera device, IntPtr handle, CameraRunParam parameters, string? configuration)
        {
            if (device.Config.IsAutoExpWithND)
                throw new NotSupportedException("本地自动曝光尚不支持服务的 ND 自动切换，请关闭自动曝光 ND 选项后重试。");
            if (configuration != null)
            {
                int configurationResult = cvCameraCSLib.UpdateCfgJson(handle, ConfigType.Cfg_ExpTime, configuration);
                if (configurationResult != cvErrorDefine.CV_ERR_SUCCESS)
                    throw LocalCameraCaptureService.CreateNativeException("本地自动曝光加载模板失败", configurationResult);
            }
            int gainResult = cvCameraCSLib.CM_SetGain(handle, parameters.Gain);
            if (gainResult != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("本地自动曝光设置增益失败", gainResult);
            int exposureResult = cvCameraCSLib.CM_SetExpTime(handle, parameters.ExpTime);
            if (exposureResult != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("本地自动曝光设置曝光失败", exposureResult);
        }

        private static void UpdateDisplay(DeviceCamera device, CameraRunParam parameters, float[] saturation)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                ApplyDisplayExposure(device.DisplayConfig, parameters, saturation);
                ConfigHandler.GetInstance().Save<DisplayConfigManager>();
            });
        }

        internal static void ApplyFrameResult(LocalFlowFrame frame, CameraRunParam parameters, float[] exposure, float[] saturation, bool threeChannels)
        {
            int count = threeChannels ? 3 : 1;
            if (saturation.Length < count || saturation.Take(count).Any(value => !float.IsFinite(value) || value < 0 || value > 100))
                throw new InvalidOperationException("本地自动曝光没有返回有效饱和度。");
            ApplyExposure(parameters, exposure, threeChannels);
            float[] frameExposure = threeChannels
                ? [parameters.ExpTimeR, parameters.ExpTimeG, parameters.ExpTimeB]
                : Enumerable.Repeat(parameters.ExpTime, frame.Metadata.Channels).ToArray();
            frame.PrepareForCalibration(string.Empty, 0, false, frameExposure);
        }

        internal static void ApplyExposure(CameraRunParam parameters, float[] exposure, bool threeChannels)
        {
            int count = threeChannels ? 3 : 1;
            if (exposure.Length < count) throw new InvalidOperationException("本地自动曝光返回的通道数不足。");
            for (int i = 0; i < count; i++)
                if (!float.IsFinite(exposure[i]) || exposure[i] <= 0)
                    throw new InvalidOperationException("本地自动曝光没有返回有效曝光值。");
            parameters.SetAllExposure(exposure[0]);
            if (threeChannels)
            {
                parameters.ExpTimeG = exposure[1];
                parameters.ExpTimeB = exposure[2];
            }
        }

        internal static void ApplyDisplayExposure(DisplayCameraConfig display, CameraRunParam parameters, float[] saturation)
        {
            display.ExpTime = parameters.ExpTime;
            display.ExpTimeR = parameters.ExpTimeR;
            display.ExpTimeG = parameters.ExpTimeG;
            display.ExpTimeB = parameters.ExpTimeB;
            display.Saturation = display.SaturationR = saturation[0];
            display.SaturationG = saturation[1];
            display.SaturationB = saturation[2];
        }
    }
}
