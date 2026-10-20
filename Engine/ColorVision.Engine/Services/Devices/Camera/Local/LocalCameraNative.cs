using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal interface ILocalCameraNative
    {
        IReadOnlyList<string> GetCameraIds();
        IntPtr Initialize();
        bool IsOpen(IntPtr handle);
        void PrepareMeasurement(IntPtr handle);
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
        public void PrepareMeasurement(IntPtr handle)
        {
            try
            {
                ConfigureFilterWheel(device, (enabled, port, baudRate) => cvCameraCSLib.CM_SetPortComParam(handle, enabled, port, baudRate));
            }
            catch (Exception error)
            {
                // A failed first preparation must not leave a partial port or stop video.
                try { ReleaseFilterWheel(handle); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }

        public int Open(IntPtr handle, string cameraId, TakeImageMode mode, int bpp, bool useHikMvs, int hikBayerQuality, bool hikOutputBgr)
        {
            try
            {
                // The manager survives Close; reapply configuration changed since its creation.
                int result = cvCameraCSLib.CM_SetCameraModel(handle, device.Config.CameraModel, device.Config.CameraMode);
                if (result != cvErrorDefine.CV_ERR_SUCCESS) throw CreateOpenException("CM_SetCameraModel", cameraId, result);
                result = cvCameraCSLib.CM_SetCameraID(handle, cameraId);
                if (result != cvErrorDefine.CV_ERR_SUCCESS) throw CreateOpenException("CM_SetCameraID", cameraId, result);
                result = cvCameraCSLib.CM_SetTakeImageMode(handle, mode);
                if (result != cvErrorDefine.CV_ERR_SUCCESS) throw CreateOpenException("CM_SetTakeImageMode", cameraId, result);
                result = cvCameraCSLib.CM_SetImageBpp(handle, bpp);
                if (result != cvErrorDefine.CV_ERR_SUCCESS) throw CreateOpenException("CM_SetImageBpp", cameraId, result);
                var config = device.PhyCamera?.Config.CameraCfg;
                JObject cameraConfig = config == null ? new JObject() : JObject.Parse(LocalCameraSession.BuildCameraConfigurationJson(config));
                cameraConfig["useHikMvs"] = useHikMvs;
                cameraConfig["hikBayerQuality"] = hikBayerQuality;
                cameraConfig["hikOutputBgr"] = hikOutputBgr;
                result = cvCameraCSLib.UpdateCfgJson(handle, ConfigType.Cfg_Camera, cameraConfig.ToString());
                if (result != cvErrorDefine.CV_ERR_SUCCESS) throw CreateOpenException("CM_UpdateCfgJson", cameraId, result);
                // CV serial control is prepared by the session only for measurement.
                // Preserve the existing disabled-serial configuration for other modes.
                if (device.Config.CameraMode != CameraMode.CV_MODE)
                    ConfigureFilterWheel(device, (enabled, port, baudRate) => cvCameraCSLib.CM_SetPortComParam(handle, enabled, port, baudRate));
                result = cvCameraCSLib.CM_Open(handle);
                if (result != cvErrorDefine.CV_ERR_SUCCESS) throw CreateOpenException("CM_Open", cameraId, result, GetCameraIds);
                return result;
            }
            catch (Exception error)
            {
                if (!IsOpen(handle))
                {
                    try { ReleaseFilterWheel(handle); }
                    catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                }
                throw;
            }
        }

        internal static void ConfigureFilterWheel(DeviceCamera device, Func<bool, string, uint, int> configurePort)
        {
            var filterWheel = device.PhyCamera?.Config.CFW ?? device.Config.CFW;
            bool enabled = device.Config.CameraMode == CameraMode.CV_MODE && filterWheel.IsUseCFW && filterWheel.IsCOM;
            string port = enabled ? filterWheel.SzComName?.Trim().ToUpperInvariant() ?? string.Empty : string.Empty;
            uint baudRate = 9600;
            if (enabled)
            {
                if (!Regex.IsMatch(port, @"^COM[1-9][0-9]*$") || !int.TryParse(port.AsSpan(3), out _) || filterWheel.BaudRate <= 0)
                    throw new InvalidOperationException("本地相机滤光轮串口配置无效，请检查 COM 端口和波特率。");
                baudRate = (uint)filterWheel.BaudRate;
            }
            // A retained manager may still have serial control enabled from its previous connection.
            int result = configurePort(enabled, port, baudRate);
            if (result != 1)
                throw new InvalidOperationException($"配置本地相机滤光轮失败：{port}，{baudRate}（CM_SetPortComParam 返回 {result}）。");
        }

        private static void ReleaseFilterWheel(IntPtr handle)
        {
            int result = cvCameraCSLib.CM_SetPortComParam(handle, false, string.Empty, 9600);
            if (result != 1)
                throw new InvalidOperationException($"释放本地相机滤光轮串口失败（CM_SetPortComParam 返回 {result}）。");
        }

        internal static InvalidOperationException CreateOpenException(string operation, string cameraId, int errorCode, Func<IReadOnlyList<string>>? discover = null)
        {
            string prefix = string.Format(EngineLocalization.Get("Camera_LocalOpenFailure"), cameraId, operation);
            var failure = LocalCameraCaptureService.CreateNativeException(prefix, errorCode);
            if (discover == null) return failure;

            string detail;
            try
            {
                string[] ids = discover().Select(id => id.Trim()).Where(id => id.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                detail = ids.Length == 0
                    ? EngineLocalization.Get("Camera_LocalOpenNoDevices")
                    : !ids.Contains(cameraId.Trim(), StringComparer.OrdinalIgnoreCase)
                        ? string.Format(EngineLocalization.Get("Camera_LocalOpenIdMismatch"), cameraId, string.Join(", ", ids))
                        : string.Format(EngineLocalization.Get("Camera_LocalOpenDetected"), cameraId);
            }
            catch (Exception ex)
            {
                detail = string.Format(EngineLocalization.Get("Camera_LocalOpenDiscoveryFailed"), ex.Message);
            }
            return new InvalidOperationException($"{detail}{Environment.NewLine}{failure.Message}", failure);
        }
        public void Close(IntPtr handle)
        {
            if (IsOpen(handle))
            {
                int result = cvCameraCSLib.CM_Close(handle);
                if (result != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("关闭本地相机失败", result);
            }
            ReleaseFilterWheel(handle);
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
