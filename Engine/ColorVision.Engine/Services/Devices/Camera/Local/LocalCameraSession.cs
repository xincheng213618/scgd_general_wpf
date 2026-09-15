using cvColorVision;
using ColorVision.Engine.Services.PhyCameras.Configs;
using Newtonsoft.Json;
using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Owns the native local-camera manager for one logical camera device.
    /// The UI controls the normal open/close lifecycle; local flow nodes may also
    /// open the configured camera on demand and then reuse the same session.
    /// </summary>
    internal sealed class LocalCameraSession : IDisposable
    {
        private readonly ILocalCameraNative native;
        private readonly CameraBackendState backend;
        private readonly Action ensureAvailable;
        private string? openedCameraId;
        private int openedBpp;
        public TakeImageMode OpenedMode { get; private set; }
        private IntPtr handle;
        private string? loadedCalibrationJson;
        private bool disposed;

        public LocalCameraSession(DeviceCamera device)
        {
            native = new LocalCameraNative(device);
            backend = device.CameraBackend;
            ensureAvailable = device.EnsureLocalCameraAvailable;
        }

        internal LocalCameraSession(ILocalCameraNative native, CameraBackendState backend, Action? ensureAvailable = null)
        {
            this.native = native;
            this.backend = backend;
            this.ensureAvailable = ensureAvailable ?? backend.EnsureLocalAvailable;
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
                    return handle != IntPtr.Zero && native.IsOpen(handle);
                }
            }
        }

        public IntPtr EnsureInitialized()
        {
            lock (SyncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (handle != IntPtr.Zero) return handle;

                handle = native.Initialize();
                return handle;
            }
        }

        public int Open(string cameraId, TakeImageMode takeImageMode, int imageBpp)
        {
            lock (SyncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                ensureAvailable();
                if (IsOpen)
                {
                    if (openedCameraId != cameraId || OpenedMode != takeImageMode || openedBpp != imageBpp)
                        throw new InvalidOperationException("本地会话已使用其它打开参数连接，请先关闭相机再修改 Camera ID、模式或位深。");
                    return cvErrorDefine.CV_ERR_SUCCESS;
                }
                ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
                lock (CameraBackendState.OwnershipSync)
                {
                    ensureAvailable();
                    backend.BeginLocalOpen();
                }
                try
                {
                    IntPtr manager = EnsureInitialized();
                    int result = native.Open(manager, cameraId, takeImageMode, imageBpp);
                    if (result == cvErrorDefine.CV_ERR_SUCCESS && !native.IsOpen(manager))
                        throw new InvalidOperationException("本地相机返回打开成功，但未建立会话。");
                    if (native.IsOpen(manager))
                    {
                        openedCameraId = cameraId;
                        OpenedMode = takeImageMode;
                        openedBpp = imageBpp;
                        loadedCalibrationJson = null;
                    }
                    return result;
                }
                finally
                {
                    RefreshStatus();
                }
            }
        }

        internal static string BuildCameraConfigurationJson(PhyCameraCfg cameraConfig)
        {
            ArgumentNullException.ThrowIfNull(cameraConfig);
            return JsonConvert.SerializeObject(new
            {
                cameraCfg = new
                {
                    ob = cameraConfig.Ob,
                    obR = cameraConfig.ObR,
                    obT = cameraConfig.ObT,
                    obB = cameraConfig.ObB,
                    tempCtlChecked = cameraConfig.TempCtlChecked,
                    targetTemp = cameraConfig.TargetTemp,
                    cameraConfig.TempSpanTime,
                    usbTraffic = cameraConfig.UsbTraffic,
                    offset = cameraConfig.Offset,
                    gain = cameraConfig.Gain,
                    ex = cameraConfig.PointX,
                    ey = cameraConfig.PointY,
                    ew = cameraConfig.Width,
                    eh = cameraConfig.Height
                }
            });
        }

        public void Close(bool unregisterCallback)
        {
            lock (SyncRoot)
            {
                try
                {
                    if (handle == IntPtr.Zero || !native.IsOpen(handle)) return;
                    if (unregisterCallback) native.DetachCallback(handle);
                    native.Close(handle);
                    if (native.IsOpen(handle)) throw new InvalidOperationException("本地相机关闭失败，会话仍然打开。");
                    loadedCalibrationJson = null;
                }
                finally { RefreshStatus(); }
            }
        }

        public void DetachCallback()
        {
            lock (SyncRoot)
            {
                if (handle != IntPtr.Zero && native.IsOpen(handle))
                {
                    native.DetachCallback(handle);
                }
            }
        }

        public bool UpdateCalibration(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            lock (SyncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (handle == IntPtr.Zero || !native.IsOpen(handle))
                {
                    return false;
                }
                if (string.Equals(loadedCalibrationJson, json, StringComparison.Ordinal))
                {
                    return true;
                }
                if (!native.UpdateCalibration(handle, json))
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
                if (handle == IntPtr.Zero || !native.IsOpen(handle))
                {
                    backend.SetLocalStatus(DeviceStatusType.Closed);
                    throw new InvalidOperationException("本地相机尚未打开，请先连接相机。");
                }
                ensureAvailable();
                try { return operation(handle); }
                finally { RefreshStatus(); }
            }
        }

        private void RefreshStatus()
        {
            backend.SetLocalStatus(IsOpen
                ? (OpenedMode == TakeImageMode.Live ? DeviceStatusType.LiveOpened : DeviceStatusType.Opened)
                : DeviceStatusType.Closed);
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
                    if (native.IsOpen(handle))
                    {
                        native.DetachCallback(handle);
                        native.Close(handle);
                    }
                    native.Release(handle);
                }
                finally
                {
                    handle = IntPtr.Zero;
                    backend.SetLocalStatus(DeviceStatusType.Closed);
                }
            }
        }
    }
}
