using cvColorVision;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras.Configs;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Owns the native local-camera manager for one logical camera device.
    /// The UI controls the normal open/close lifecycle; local flow nodes may also
    /// open the configured camera on demand and then reuse the same session.
    /// </summary>
    internal sealed class LocalCameraSession : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalCameraSession));
        private readonly ILocalCameraNative native;
        private readonly CameraBackendState backend;
        private readonly Action<string?> ensureAvailable;
        private readonly Func<ConfigCamera> getConfig;
        private readonly Action saveConfig;
        private readonly Func<bool> getUseHikMvs;
        private readonly Func<int> getHikBayerQuality;
        private readonly Func<bool> getHikOutputBgr;
        private string? openedCameraId;
        private int openedBpp;
        public TakeImageMode OpenedMode { get; private set; }
        public bool OpenedUseHikMvs { get; private set; }
        public int OpenedHikBayerQuality { get; private set; }
        public bool OpenedHikOutputBgr { get; private set; }
        internal int OpenedBpp => openedBpp;
        private IntPtr handle;
        private string? loadedCalibrationJson;
        private Action? stopPreview;
        private bool disposed;

        public LocalCameraSession(DeviceCamera device)
        {
            native = new LocalCameraNative(device);
            backend = device.CameraBackend;
            ensureAvailable = device.EnsureLocalCameraAvailable;
            getConfig = () => device.Config;
            saveConfig = device.SaveConfig;
            getUseHikMvs = () => device.DisplayConfig.UseHikMvs;
            getHikBayerQuality = () => (int)device.DisplayConfig.HikBayerQuality;
            getHikOutputBgr = () => device.DisplayConfig.HikOutputBgr;
        }

        internal LocalCameraSession(ILocalCameraNative native, CameraBackendState backend, ConfigCamera? config = null, Action? saveConfig = null, Action<string?>? ensureAvailable = null, Func<bool>? getUseHikMvs = null, Func<int>? getHikBayerQuality = null, Func<bool>? getHikOutputBgr = null)
        {
            this.native = native;
            this.backend = backend;
            this.ensureAvailable = ensureAvailable ?? (_ => backend.EnsureLocalAvailable());
            ConfigCamera cameraConfig = config ?? new ConfigCamera();
            getConfig = () => cameraConfig;
            this.saveConfig = saveConfig ?? (() => { });
            this.getUseHikMvs = getUseHikMvs ?? (() => true);
            this.getHikBayerQuality = getHikBayerQuality ?? (() => 3);
            this.getHikOutputBgr = getHikOutputBgr ?? (() => true);
        }

        internal object SyncRoot { get; } = new();
        internal LocalCameraRawBufferPool RawBufferPool { get; private set; } = new();

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
                ensureAvailable(null);
                if (IsOpen)
                {
                    cameraId = string.IsNullOrWhiteSpace(cameraId) ? openedCameraId! : cameraId.Trim();
                    if (openedCameraId != cameraId || OpenedMode != takeImageMode || openedBpp != imageBpp)
                        throw new InvalidOperationException("本地会话已使用其它打开参数连接，请先关闭相机再修改 Camera ID、模式或位深。");
                    return cvErrorDefine.CV_ERR_SUCCESS;
                }
                ConfigCamera config = getConfig();
                string? configuredCameraId = config.CameraID;
                cameraId = string.IsNullOrWhiteSpace(cameraId) ? config.CameraID?.Trim() ?? string.Empty : cameraId.Trim();
                if (cameraId.Length == 0)
                {
                    IReadOnlyList<string> cameraIds = native.GetCameraIds();
                    cameraId = SelectCameraId(cameraIds, null, config.CameraCode);
                    if (cameraId.Length == 0)
                    {
                        string messageKey = cameraIds.Count == 0 ? "Camera_LocalNotFound"
                            : string.IsNullOrWhiteSpace(config.CameraCode) ? "Camera_LocalSelectionRequired" : "Camera_LocalBindingUnavailable";
                        throw new InvalidOperationException(EngineLocalization.Get(messageKey));
                    }
                }
                lock (CameraBackendState.OwnershipSync)
                {
                    ensureAvailable(cameraId);
                    // Reserve ownership and publish the resolved identity under the same lock.
                    backend.BeginLocalOpen();
                    config.CameraID = cameraId;
                }
                try
                {
                    IntPtr manager = EnsureInitialized();
                    bool useHikMvs = getUseHikMvs();
                    int hikBayerQuality = getHikBayerQuality();
                    bool hikOutputBgr = getHikOutputBgr();
                    int result = native.Open(manager, cameraId, takeImageMode, imageBpp, useHikMvs, hikBayerQuality, hikOutputBgr);
                    if (result == cvErrorDefine.CV_ERR_SUCCESS && !native.IsOpen(manager))
                        throw new InvalidOperationException("本地相机返回打开成功，但未建立会话。");
                    if (native.IsOpen(manager))
                    {
                        RawBufferPool.Dispose();
                        RawBufferPool = new LocalCameraRawBufferPool();
                        openedCameraId = cameraId;
                        OpenedMode = takeImageMode;
                        OpenedUseHikMvs = useHikMvs;
                        OpenedHikBayerQuality = hikBayerQuality;
                        OpenedHikOutputBgr = hikOutputBgr;
                        openedBpp = imageBpp;
                        loadedCalibrationJson = null;
                    }
                    if (result == cvErrorDefine.CV_ERR_SUCCESS && !string.Equals(configuredCameraId, cameraId, StringComparison.Ordinal))
                    {
                        try { saveConfig(); }
                        catch (Exception ex) { log.Error("保存本地相机 CameraID 失败；当前已打开的会话仍可使用。", ex); }
                    }
                    return result;
                }
                finally
                {
                    if (!IsOpen) config.CameraID = configuredCameraId!;
                    RefreshStatus();
                }
            }
        }

        internal static string SelectCameraId(IReadOnlyList<string> cameraIds, string? configuredCameraId, string? cameraCode)
        {
            string[] ids = cameraIds.Select(id => id.Trim()).Where(id => id.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string? configured = ids.FirstOrDefault(id => id.Equals(configuredCameraId?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (configured != null) return configured;
            if (!string.IsNullOrWhiteSpace(cameraCode))
            {
                string[] matches = ids.Where(id => ColorVision.Common.Utilities.Tool.GetMD5(id).Contains(cameraCode.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
                return matches.Length == 1 ? matches[0] : string.Empty;
            }
            return ids.Length == 1 ? ids[0] : string.Empty;
        }

        internal void EnsureMeasurement(bool autoConnect)
        {
            lock (SyncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                ConfigCamera config = getConfig();
                TakeImageMode mode = config.TakeImageMode == TakeImageMode.Live ? TakeImageMode.Measure_Normal : config.TakeImageMode;
                int result;
                if (IsOpen)
                {
                    if (OpenedMode != TakeImageMode.Live) return;
                    result = SwitchMode(mode, (int)config.ImageBpp);
                }
                else
                {
                    if (!autoConnect) throw new InvalidOperationException("本地相机尚未打开，请先连接相机。");
                    result = Open(config.CameraID?.Trim() ?? string.Empty, mode, (int)config.ImageBpp);
                    if (result == cvErrorDefine.CV_ERR_SUCCESS) config.TakeImageMode = mode;
                }
                if (result != cvErrorDefine.CV_ERR_SUCCESS)
                    throw LocalCameraCaptureService.CreateNativeException("本地相机进入拍照模式失败", result);
            }
        }

        // Retain an MVS connection, or reopen the same legacy manager/identity.
        // Keep ownership reserved until the SDK reports the actual final state.
        internal int SwitchMode(TakeImageMode mode, int imageBpp)
        {
            lock (SyncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
                if (imageBpp is not (8 or 16)) throw new ArgumentOutOfRangeException(nameof(imageBpp));
                if (!IsOpen) throw new InvalidOperationException("本地相机尚未打开，请先连接相机。");
                ensureAvailable(openedCameraId);
                if (OpenedMode == mode && openedBpp == imageBpp) return cvErrorDefine.CV_ERR_SUCCESS;
                try
                {
                    DetachPreviewCore();
                    int result = native.SwitchMode(handle, mode, imageBpp);
                    if (result == cvErrorDefine.CV_ERR_CAM_TYPE_NOT)
                    {
                        native.Close(handle);
                        if (native.IsOpen(handle)) throw new InvalidOperationException("本地相机关闭失败，无法切换采集模式。");
                        loadedCalibrationJson = null;
                        result = native.Open(handle, openedCameraId!, mode, imageBpp, OpenedUseHikMvs, OpenedHikBayerQuality, OpenedHikOutputBgr);
                    }
                    if (result == cvErrorDefine.CV_ERR_SUCCESS && !native.IsOpen(handle))
                        throw new InvalidOperationException("本地相机返回打开成功，但未建立会话。");
                    if (result == cvErrorDefine.CV_ERR_SUCCESS)
                    {
                        loadedCalibrationJson = null;
                        OpenedMode = mode;
                        openedBpp = imageBpp;
                        RawBufferPool.Dispose();
                        RawBufferPool = new LocalCameraRawBufferPool();
                        getConfig().TakeImageMode = mode;
                    }
                    return result;
                }
                finally { RefreshStatus(); }
            }
        }

        // A session has one preview owner. Stop its managed queue before detaching
        // the native callback, so late video frames cannot overwrite a measurement.
        internal void RegisterPreview(Func<IntPtr, int> register, Action onStopped)
        {
            ArgumentNullException.ThrowIfNull(register);
            ArgumentNullException.ThrowIfNull(onStopped);
            UseOpened(manager =>
            {
                if (OpenedMode != TakeImageMode.Live) throw new InvalidOperationException("本地视频需要 Live 会话。");
                DetachPreviewCore();
                int result = register(manager);
                if (result != cvErrorDefine.CV_ERR_SUCCESS)
                {
                    var failure = LocalCameraCaptureService.CreateNativeException("启动本地相机视频失败", result);
                    onStopped();
                    try { native.DetachCallback(manager); }
                    catch (Exception ex) { log.Warn("清理失败的视频回调失败。", ex); }
                    throw failure;
                }
                stopPreview = onStopped;
                return 0;
            });
        }

        private void DetachPreviewCore()
        {
            Action? stopped = stopPreview;
            stopPreview = null;
            if (stopped == null && OpenedMode != TakeImageMode.Live) return;
            try { stopped?.Invoke(); }
            finally { native.DetachCallback(handle); }
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
                    if (unregisterCallback) DetachPreviewCore();
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
                    DetachPreviewCore();
                }
            }
        }

        // A replaced/closed view must not detach another view's preview.
        internal void StopPreview(Action owner, bool closeCamera)
        {
            lock (SyncRoot)
            {
                if (stopPreview != owner) return;
                if (closeCamera) Close(unregisterCallback: true);
                else DetachCallback();
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
                    RefreshStatus();
                    throw new InvalidOperationException("本地相机尚未打开，请先连接相机。");
                }
                ensureAvailable(openedCameraId);
                try { return operation(handle); }
                finally { RefreshStatus(); }
            }
        }

        private void RefreshStatus()
        {
            bool isOpen = IsOpen;
            if (!isOpen) RawBufferPool.Dispose();
            backend.SetLocalStatus(isOpen
                ? (OpenedMode == TakeImageMode.Live ? DeviceStatusType.LiveOpened : DeviceStatusType.Opened)
                : DeviceStatusType.Closed);
        }

        public void Dispose()
        {
            lock (SyncRoot)
            {
                if (disposed) return;
                disposed = true;
                RawBufferPool.Dispose();
                if (handle == IntPtr.Zero) return;

                try
                {
                    if (native.IsOpen(handle))
                    {
                        DetachPreviewCore();
                        native.Close(handle);
                    }
                }
                finally
                {
                    try { native.Release(handle); }
                    finally
                    {
                        handle = IntPtr.Zero;
                        backend.SetLocalStatus(DeviceStatusType.Closed);
                    }
                }
            }
        }
    }
}
