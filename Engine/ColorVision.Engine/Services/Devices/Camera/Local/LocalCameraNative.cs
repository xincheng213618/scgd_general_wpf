using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal interface ILocalCameraNative
    {
        IReadOnlyList<string> GetCameraIds();
        IntPtr Initialize();
        bool IsOpen(IntPtr handle);
        int Open(IntPtr handle, string cameraId, TakeImageMode mode, int bpp, bool useHikMvs, int hikBayerQuality, bool hikOutputBgr);
        int SwitchMode(IntPtr handle, TakeImageMode mode, int bpp);
        void Close(IntPtr handle);
        void DetachCallback(IntPtr handle);
        bool UpdateCalibration(IntPtr handle, string json);
        void Release(IntPtr handle);
    }

    internal sealed class LocalCameraNative(DeviceCamera device) : ILocalCameraNative
    {
        public int SwitchMode(IntPtr handle, TakeImageMode mode, int bpp) => cvCameraCSLib.CM_SwitchCaptureMode(handle, mode, bpp);

        public IReadOnlyList<string> GetCameraIds()
        {
            var summary = cvCameraCSLib.SearchCameraIds(new[] { device.Config.CameraModel });
            if (summary.Models.Single().Success != true)
                throw new InvalidOperationException(EngineLocalization.Get("Camera_LocalDiscoveryFailed"));
            return summary.Cameras.Select(camera => camera.CameraId).ToArray();
        }

        public IntPtr Initialize()
        {
            IntPtr manager = cvCameraCSLib.CM_CreatCameraManagerV1(device.Config.CameraModel, device.Config.CameraMode, null);
            if (manager == IntPtr.Zero) throw new InvalidOperationException($"创建本地相机管理器失败：{device.Code}");
            return manager;
        }

        public bool IsOpen(IntPtr handle) => cvCameraCSLib.CM_IsOpen(handle);
        public int Open(IntPtr handle, string cameraId, TakeImageMode mode, int bpp, bool useHikMvs, int hikBayerQuality, bool hikOutputBgr)
        {
            int result = cvCameraCSLib.CM_SetCameraID(handle, cameraId);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) return result;
            result = cvCameraCSLib.CM_SetTakeImageMode(handle, mode);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) return result;
            result = cvCameraCSLib.CM_SetImageBpp(handle, bpp);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) return result;
            var config = device.PhyCamera?.Config.CameraCfg;
            JObject cameraConfig = config == null ? new JObject() : JObject.Parse(LocalCameraSession.BuildCameraConfigurationJson(config));
            cameraConfig["useHikMvs"] = useHikMvs;
            cameraConfig["hikBayerQuality"] = hikBayerQuality;
            cameraConfig["hikOutputBgr"] = hikOutputBgr;
            result = cvCameraCSLib.UpdateCfgJson(handle, ConfigType.Cfg_Camera, cameraConfig.ToString());
            if (result != cvErrorDefine.CV_ERR_SUCCESS) return result;
            return cvCameraCSLib.CM_Open(handle);
        }
        public void Close(IntPtr handle)
        {
            int result = cvCameraCSLib.CM_Close(handle);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("关闭本地相机失败", result);
        }
        public void DetachCallback(IntPtr handle)
        {
            int result = cvCameraCSLib.CM_UnregisterCallBack(handle);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("注销本地相机视频回调失败", result);
        }
        public bool UpdateCalibration(IntPtr handle, string json) => cvCameraCSLib.UpdateCfgJson(handle, ConfigType.Cfg_Calibration, json) == cvErrorDefine.CV_ERR_SUCCESS;
        public void Release(IntPtr handle)
        {
            _ = cvCameraCSLib.ReleaseCameraManager(handle);
        }
    }
}
