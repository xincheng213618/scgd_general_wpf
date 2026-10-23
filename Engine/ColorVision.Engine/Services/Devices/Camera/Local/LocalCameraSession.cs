using cvColorVision;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras.Configs;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
        private readonly Action ensureLicenseAvailable;
        private readonly Func<ConfigCamera> getConfig;
        private readonly Action saveConfig;
        private readonly Func<string, string> resolveCameraId;
        private readonly Func<bool> getUseHikMvs;
        private readonly Func<int> getHikBayerQuality;
        private readonly Func<bool> getHikOutputBgr;
        private string? openedCameraId;
        private int openedBpp;
        public TakeImageMode OpenedMode { get; private set; }
        internal string? OpenedCameraId => openedCameraId;
        public bool OpenedUseHikMvs { get; private set; }
        public int OpenedHikBayerQuality { get; private set; }
        public bool OpenedHikOutputBgr { get; private set; }
        internal int OpenedBpp => openedBpp;
        private IntPtr handle;
        private string? loadedCalibrationJson;
        private Action? stopPreview;
        private bool disposed;
        private bool identitySavePending;
        private bool filterWheelPrepared;
        private readonly Func<MotorConfig> getMotorConfig;
        private readonly LocalCameraMotorSession motor;
        private readonly object focusSync = new();
        private CancellationTokenSource? focusCancellation;
        private bool closingRequested;

        public LocalCameraSession(DeviceCamera device)
        {
            native = new LocalCameraNative(device);
            backend = device.CameraBackend;
            ensureAvailable = device.EnsureLocalCameraAvailable;
            ensureLicenseAvailable = device.EnsureLocalCameraLicense;
            getConfig = () => device.Config;
            saveConfig = () => device.SaveResolvedCameraId(device.Config.CameraID);
            resolveCameraId = id => device.ResolveCameraId(id);
            getUseHikMvs = () => device.DisplayConfig.UseHikMvs;
            getHikBayerQuality = () => (int)device.DisplayConfig.HikBayerQuality;
            getHikOutputBgr = () => device.DisplayConfig.HikOutputBgr;
            getMotorConfig = () => device.PhyCamera?.Config.MotorConfig ?? device.Config.MotorConfig;
            motor = new LocalCameraMotorSession(new LocalCameraMotorNative());
        }

        internal LocalCameraSession(ILocalCameraNative native, CameraBackendState backend, ConfigCamera? config = null, Action? saveConfig = null, Action<string?>? ensureAvailable = null, Func<bool>? getUseHikMvs = null, Func<int>? getHikBayerQuality = null, Func<bool>? getHikOutputBgr = null, Action? ensureLicenseAvailable = null, Func<string, string>? resolveCameraId = null, ILocalCameraMotorNative? motorNative = null)
        {
            this.native = native;
            this.backend = backend;
            this.ensureAvailable = ensureAvailable ?? (_ => backend.EnsureLocalAvailable());
            this.ensureLicenseAvailable = ensureLicenseAvailable ?? (() => { });
            ConfigCamera cameraConfig = config ?? new ConfigCamera();
            getConfig = () => cameraConfig;
            this.saveConfig = saveConfig ?? (() => { });
            this.resolveCameraId = resolveCameraId ?? (id => id);
            this.getUseHikMvs = getUseHikMvs ?? (() => true);
            this.getHikBayerQuality = getHikBayerQuality ?? (() => 3);
            this.getHikOutputBgr = getHikOutputBgr ?? (() => true);
            getMotorConfig = () => cameraConfig.MotorConfig;
            motor = new LocalCameraMotorSession(motorNative ?? new LocalCameraMotorNative());
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
                ConfigCamera config = getConfig();
                string? configuredCameraId = config.CameraID;
                cameraId = resolveCameraId(string.IsNullOrWhiteSpace(cameraId) ? configuredCameraId ?? string.Empty : cameraId).Trim();
                if (IsOpen)
                {
                    if (cameraId.Length == 0 && !string.IsNullOrWhiteSpace(config.CameraCode))
                    {
                        EnsureOpenedCameraIdentity();
                        cameraId = openedCameraId!;
                    }
                    if (openedCameraId != cameraId || OpenedMode != takeImageMode || openedBpp != imageBpp)
                        throw new InvalidOperationException("本地会话已使用其它打开参数连接，请先关闭相机再修改 Camera ID、模式或位深。");
                    motor.EnsureConfiguration(getMotorConfig());
                    config.TakeImageMode = OpenedMode;
                    return cvErrorDefine.CV_ERR_SUCCESS;
                }
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
                    PrepareMeasurement(takeImageMode);
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
                        EnsureLicenseAvailable();
                        if (result == cvErrorDefine.CV_ERR_SUCCESS)
                        {
                            config.TakeImageMode = takeImageMode;
                            try { motor.Open(getMotorConfig()); }
                            catch (Exception error)
                            {
                                if (getMotorConfig().IsCameraLinkage)
                                {
                                    try { motor.Close(); native.Close(manager); }
                                    catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                                    throw;
                                }
                                log.Warn("本地电机连接失败；相机联动未启用，相机仍可取图。", error);
                            }
                        }
                    }
                    if (result == cvErrorDefine.CV_ERR_SUCCESS && (identitySavePending || !string.IsNullOrWhiteSpace(config.CameraCode)
                        || !string.Equals(configuredCameraId, cameraId, StringComparison.Ordinal)))
                    {
                        // A logical mirror can already contain the right ID while the physical record is still unsaved.
                        identitySavePending = true;
                        try { saveConfig(); identitySavePending = false; }
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

        private void EnsureLicenseAvailable()
        {
            try { ensureLicenseAvailable(); }
            catch (LocalCameraLicenseException)
            {
                Close(unregisterCallback: true);
                throw;
            }
        }

        internal static string SelectCameraId(IReadOnlyList<string> cameraIds, string? configuredCameraId, string? cameraCode)
        {
            string[] ids = cameraIds.Select(id => id.Trim()).Where(id => id.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!string.IsNullOrWhiteSpace(cameraCode))
            {
                string[] matches = ids.Where(id => ColorVision.Common.Utilities.Tool.GetMD5(id).Equals(cameraCode.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
                return matches.Length == 1 ? matches[0] : string.Empty;
            }
            string? configured = ids.FirstOrDefault(id => id.Equals(configuredCameraId?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (configured != null) return configured;
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
                    EnsureOpenedCameraIdentity();
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

        private void PrepareMeasurement(TakeImageMode mode)
        {
            if (mode == TakeImageMode.Live || getConfig().CameraMode != CameraMode.CV_MODE || filterWheelPrepared) return;
            native.PrepareMeasurement(handle);
            filterWheelPrepared = true;
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
                EnsureOpenedCameraIdentity();
                if (OpenedMode == mode && openedBpp == imageBpp)
                {
                    getConfig().TakeImageMode = OpenedMode;
                    return cvErrorDefine.CV_ERR_SUCCESS;
                }
                try
                {
                    // Keep the existing preview intact if first-time CFW preparation fails.
                    PrepareMeasurement(mode);
                    DetachPreviewCore();
                    int result = native.SwitchMode(handle, mode, imageBpp);
                    if (result == cvErrorDefine.CV_ERR_CAM_TYPE_NOT)
                    {
                        native.Close(handle);
                        filterWheelPrepared = false;
                        if (native.IsOpen(handle)) throw new InvalidOperationException("本地相机关闭失败，无法切换采集模式。");
                        loadedCalibrationJson = null;
                        PrepareMeasurement(mode);
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
            RequestFocusStop();
            lock (SyncRoot)
            {
                try
                {
                    motor.Close();
                    if (handle == IntPtr.Zero || !native.IsOpen(handle)) return;
                    if (unregisterCallback) DetachPreviewCore();
                    native.Close(handle);
                    if (native.IsOpen(handle)) throw new InvalidOperationException("本地相机关闭失败，会话仍然打开。");
                    loadedCalibrationJson = null;
                }
                finally
                {
                    lock (focusSync) closingRequested = false;
                    RefreshStatus();
                }
            }
        }

        private void RequestFocusStop()
        {
            lock (focusSync)
            {
                closingRequested = true;
                focusCancellation?.Cancel();
            }
        }

        internal T UseMotor<T>(Func<LocalCameraMotorSession, T> operation) => UseOpened(_ =>
        {
            if (OpenedMode == TakeImageMode.Live) throw new InvalidOperationException("请先停止本地预览再操作电机。");
            motor.EnsureConfiguration(getMotorConfig());
            return operation(motor);
        });

        internal LocalAutoFocusResult AutoFocus(LocalAutoFocusSettings settings, float exposure, float gain, CancellationToken stop = default) => UseOpened(manager =>
        {
            stop.ThrowIfCancellationRequested();
            if (OpenedMode == TakeImageMode.Live) throw new InvalidOperationException("请先停止本地预览再自动对焦。");
            motor.EnsureConfiguration(getMotorConfig());
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stop);
            lock (focusSync)
            {
                if (closingRequested) throw new OperationCanceledException("本地相机正在关闭。");
                focusCancellation = cancellation;
            }
            try { return motor.AutoFocus(manager, settings, exposure, gain, cancellation.Token); }
            finally { lock (focusSync) focusCancellation = null; }
        });

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
                EnsureOpenedCameraIdentity();
                try { return operation(handle); }
                finally { RefreshStatus(); }
            }
        }

        private void EnsureOpenedCameraIdentity()
        {
            string currentId = resolveCameraId(openedCameraId!);
            if (string.Equals(openedCameraId, currentId, StringComparison.OrdinalIgnoreCase)) return;
            // A failed identity save must not invalidate a connection already verified by the fixed physical Code.
            string? code = getConfig().CameraCode;
            if (currentId.Length == 0 && !string.IsNullOrWhiteSpace(code)
                && string.Equals(ColorVision.Common.Utilities.Tool.GetMD5(openedCameraId!), code, StringComparison.OrdinalIgnoreCase)) return;
            throw new InvalidOperationException("物理相机 ID 已更新，请先关闭当前相机后重新连接。");
        }

        private void RefreshStatus()
        {
            bool isOpen = IsOpen;
            if (!isOpen)
            {
                filterWheelPrepared = false;
                RawBufferPool.Dispose();
                try { motor.Close(); }
                catch (Exception error)
                {
                    // The camera may have closed during a failed mode switch while its
                    // motor still owns a port. Keep Disconnect available for a retry.
                    log.Warn("本地相机已关闭，但电机句柄尚未释放，请重试关闭。", error);
                    backend.SetLocalStatus(DeviceStatusType.Opened);
                    return;
                }
            }
            backend.SetLocalStatus(isOpen
                ? (OpenedMode == TakeImageMode.Live ? DeviceStatusType.LiveOpened : DeviceStatusType.Opened)
                : DeviceStatusType.Closed);
        }

        public void Dispose()
        {
            RequestFocusStop();
            lock (SyncRoot)
            {
                if (disposed) return;
                disposed = true;
                RawBufferPool.Dispose();
                if (handle == IntPtr.Zero) return;

                try
                {
                    motor.Close();
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
