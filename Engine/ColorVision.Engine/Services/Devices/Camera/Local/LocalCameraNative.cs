using cvColorVision;
using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal interface ILocalCameraNative
    {
        IntPtr Initialize();
        bool IsOpen(IntPtr handle);
        int Open(IntPtr handle, string cameraId, TakeImageMode mode, int bpp);
        void Close(IntPtr handle);
        void DetachCallback(IntPtr handle);
        bool UpdateCalibration(IntPtr handle, string json);
        void Release(IntPtr handle);
    }

    internal sealed class LocalCameraNative(DeviceCamera device) : ILocalCameraNative
    {
        public IntPtr Initialize()
        {
            IntPtr manager = cvCameraCSLib.CM_CreatCameraManagerV1(device.Config.CameraModel, device.Config.CameraMode, "cfg\\sys.cfg");
            if (manager == IntPtr.Zero) throw new InvalidOperationException($"创建本地相机管理器失败：{device.Code}");
            if (cvCameraCSLib.CM_InitXYZ(manager) == 0)
            {
                _ = cvCameraCSLib.ReleaseCameraManager(manager);
                throw new InvalidOperationException($"初始化本地相机 CIE 上下文失败：{device.Code}");
            }
            return manager;
        }

        public bool IsOpen(IntPtr handle) => cvCameraCSLib.CM_IsOpen(handle);
        public int Open(IntPtr handle, string cameraId, TakeImageMode mode, int bpp)
        {
            cvCameraCSLib.CM_SetCameraID(handle, cameraId);
            _ = cvCameraCSLib.CM_SetTakeImageMode(handle, mode);
            _ = cvCameraCSLib.CM_SetImageBpp(handle, bpp);
            var config = device.PhyCamera?.Config.CameraCfg;
            if (config != null && !cvCameraCSLib.UpdateCfgJson(handle, ConfigType.Cfg_Camera, LocalCameraSession.BuildCameraConfigurationJson(config)))
                return cvErrorDefine.CV_ERR_UNKNOWN;
            return cvCameraCSLib.CM_Open(handle);
        }
        public void Close(IntPtr handle) => cvCameraCSLib.CM_Close(handle);
        public void DetachCallback(IntPtr handle) => cvCameraCSLib.CM_UnregisterCallBack(handle);
        public bool UpdateCalibration(IntPtr handle, string json) => cvCameraCSLib.UpdateCfgJson(handle, ConfigType.Cfg_Calibration, json);
        public void Release(IntPtr handle)
        {
            _ = cvCameraCSLib.CM_UnInitXYZ(handle);
            _ = cvCameraCSLib.ReleaseCameraManager(handle);
        }
    }
}
