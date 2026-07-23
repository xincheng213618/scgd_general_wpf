using cvColorVision;
using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Owns the native local-camera manager for one logical camera device.
    /// The UI controls open/close; flow nodes may only use an already-open handle.
    /// </summary>
    internal sealed class LocalCameraSession : IDisposable
    {
        private readonly DeviceCamera device;
        private IntPtr handle;
        private string? loadedCalibrationJson;
        private bool disposed;

        public LocalCameraSession(DeviceCamera device)
        {
            this.device = device;
        }

        internal object SyncRoot { get; } = new();

        public IntPtr Handle
        {
            get
            {
                lock (SyncRoot)
                {
                    return handle;
                }
            }
        }

        public bool IsOpen
        {
            get
            {
                lock (SyncRoot)
                {
                    return handle != IntPtr.Zero && cvCameraCSLib.CM_IsOpen(handle);
                }
            }
        }

        public IntPtr EnsureInitialized()
        {
            lock (SyncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (handle != IntPtr.Zero) return handle;

                IntPtr manager = cvCameraCSLib.CM_CreatCameraManagerV1(device.Config.CameraModel, device.Config.CameraMode, "cfg\\sys.cfg");
                if (manager == IntPtr.Zero) throw new InvalidOperationException($"创建本地相机管理器失败：{device.Code}");
                if (cvCameraCSLib.CM_InitXYZ(manager) == 0)
                {
                    _ = cvCameraCSLib.ReleaseCameraManager(manager);
                    throw new InvalidOperationException($"初始化本地相机 CIE 上下文失败：{device.Code}");
                }

                handle = manager;
                return handle;
            }
        }

        public int Open(string cameraId, TakeImageMode takeImageMode, int imageBpp)
        {
            lock (SyncRoot)
            {
                IntPtr manager = EnsureInitialized();
                if (cvCameraCSLib.CM_IsOpen(manager)) return cvErrorDefine.CV_ERR_SUCCESS;

                cvCameraCSLib.CM_SetCameraID(manager, cameraId);
                _ = cvCameraCSLib.CM_SetTakeImageMode(manager, takeImageMode);
                _ = cvCameraCSLib.CM_SetImageBpp(manager, imageBpp);
                return cvCameraCSLib.CM_Open(manager);
            }
        }

        public void Close(bool unregisterCallback)
        {
            lock (SyncRoot)
            {
                if (handle == IntPtr.Zero || !cvCameraCSLib.CM_IsOpen(handle)) return;
                if (unregisterCallback) cvCameraCSLib.CM_UnregisterCallBack(handle);
                cvCameraCSLib.CM_Close(handle);
            }
        }

        public void DetachCallback()
        {
            lock (SyncRoot)
            {
                if (handle != IntPtr.Zero && cvCameraCSLib.CM_IsOpen(handle))
                {
                    cvCameraCSLib.CM_UnregisterCallBack(handle);
                }
            }
        }

        public bool UpdateCalibration(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            lock (SyncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (handle == IntPtr.Zero || !cvCameraCSLib.CM_IsOpen(handle))
                {
                    return false;
                }
                if (string.Equals(loadedCalibrationJson, json, StringComparison.Ordinal))
                {
                    return true;
                }
                if (!cvCameraCSLib.UpdateCfgJson(handle, ConfigType.Cfg_Calibration, json))
                {
                    return false;
                }

                loadedCalibrationJson = json;
                return true;
            }
        }

        public T UseOpened<T>(Func<IntPtr, T> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);
            lock (SyncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (handle == IntPtr.Zero || !cvCameraCSLib.CM_IsOpen(handle))
                {
                    throw new InvalidOperationException($"本地相机尚未打开：{device.Code}。请先在本地相机窗口中连接相机。");
                }
                return operation(handle);
            }
        }

        public void Dispose()
        {
            lock (SyncRoot)
            {
                if (disposed) return;
                disposed = true;
                if (handle == IntPtr.Zero) return;

                try
                {
                    if (cvCameraCSLib.CM_IsOpen(handle))
                    {
                        cvCameraCSLib.CM_UnregisterCallBack(handle);
                        cvCameraCSLib.CM_Close(handle);
                    }
                    _ = cvCameraCSLib.CM_UnInitXYZ(handle);
                    _ = cvCameraCSLib.ReleaseCameraManager(handle);
                }
                finally
                {
                    handle = IntPtr.Zero;
                }
            }
        }
    }
}
