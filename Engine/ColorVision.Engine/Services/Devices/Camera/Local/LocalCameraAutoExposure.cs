using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.UI;
using cvColorVision;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal static class LocalCameraAutoExposure
    {
        internal static void Measure(DeviceCamera device, IntPtr handle, CameraRunParam parameters)
        {
            if (device.Config.IsAutoExpWithND)
                throw new NotSupportedException("本地自动曝光尚不支持服务的 ND 自动切换，请关闭自动曝光 ND 选项后重试。");
            float[] exposure = new float[3];
            float[] saturation = new float[3];
            _ = cvCameraCSLib.CM_SetGain(handle, parameters.Gain);
            _ = cvCameraCSLib.CM_SetExpTime(handle, parameters.ExpTime);
            int result = cvCameraCSLib.CM_GetAutoExpTime(handle, exposure, saturation);
            if (result != cvErrorDefine.CV_ERR_SUCCESS)
                throw LocalCameraCaptureService.CreateNativeException("本地自动曝光失败", result);
            ApplyExposure(parameters, exposure, device.Config.IsExpThree);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                ApplyDisplayExposure(device.DisplayConfig, parameters, saturation);
                ConfigHandler.GetInstance().Save<DisplayConfigManager>();
            });
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
