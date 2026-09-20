using cvColorVision;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

/// <summary>A direct SDK session. Does not create DeviceCamera, templates, database records or service clients.</summary>
public sealed class StandaloneCameraOptions
{
    [Category("连接"), DisplayName("相机型号")]
    public CameraModel Model { get; set; } = CameraModel.HK_USB;
    [Category("连接"), DisplayName("相机模式")]
    public CameraMode Mode { get; set; } = CameraMode.BV_MODE;
    [Browsable(false)]
    public string CameraId { get; set; } = string.Empty;
    [Browsable(false)]
    public string ConfigurationFile { get; set; } = "cfg/sys.cfg";
    [Category("采集"), DisplayName("位深"), Description("支持 8 或 16 位；实际支持能力由相机驱动决定。")]
    public int BitDepth { get; set; } = 8;
    [Category("采集"), DisplayName("曝光时间 (ms)")]
    public float ExposureMilliseconds { get; set; } = 100;
    [Category("采集"), DisplayName("增益")]
    public float Gain { get; set; }

    public StandaloneCameraOptions Copy() => (StandaloneCameraOptions)MemberwiseClone();

    public void Validate()
    {
        if (!Enum.IsDefined(Model) || !Enum.IsDefined(Mode) || BitDepth is not (8 or 16)
            || !float.IsFinite(ExposureMilliseconds) || ExposureMilliseconds <= 0 || !float.IsFinite(Gain) || Gain < 0)
            throw new ArgumentException("请检查相机型号、模式、位深、曝光时间和增益。");
        if (string.IsNullOrWhiteSpace(CameraId)) throw new ArgumentException("请选择或填写相机 ID。");
        if (Mode is not (CameraMode.BV_MODE or CameraMode.LV_MODE))
            throw new ArgumentException("独立检测目前支持 BV_MODE 彩色相机及 LV_MODE 单色相机；不执行滤轮或 CIE 测量。");
    }
}

/// <summary>Owns a packed BGR/BGRA or gray copy. SDK memory never escapes its callback.</summary>
public sealed record StandaloneCameraFrame(byte[] Pixels, int Width, int Height, int BitDepth, int Channels, int Stride, DateTimeOffset CapturedAt)
{
    public static int RequiredBytes(int width, int height, int bitDepth, int channels, int stride)
    {
        if (width <= 0 || height <= 0 || bitDepth is not (8 or 16) || channels is not (1 or 3 or 4)
            || stride < checked(width * channels * (bitDepth / 8)))
            throw new ArgumentException("不支持的相机像素格式或行跨度。");
        long bytes = (long)stride * height;
        if (bytes > 512L * 1024 * 1024) throw new ArgumentException("单帧超过 512 MiB。");
        return checked((int)bytes);
    }
}

public sealed class StandaloneCameraSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _commands = new(1, 1);
    private readonly object _frames = new();
    private readonly cvCameraCSLib.QHYCCDProcCallBack _callback;
    private IntPtr _handle;
    private StandaloneCameraOptions? _options;
    private StandaloneCameraFrame? _latest;
    private bool _acceptFrames;
    private bool _disposed;
    private string? _frameError;
    public bool IsConnected => _handle != IntPtr.Zero;
    public bool IsLive { get; private set; }
    public StandaloneCameraOptions? RequestedSettings => _options?.Copy();

    public StandaloneCameraSession() => _callback = OnFrame;

    public async Task SetAcquisitionParameterAsync(bool exposure, float value)
    {
        if (!float.IsFinite(value) || (exposure ? value <= 0 : value < 0)) throw new ArgumentOutOfRangeException(nameof(value));
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!IsConnected || _options == null) throw new InvalidOperationException("请先连接相机。");
            await Task.Run(() =>
            {
                bool accepted = exposure ? cvCameraCSLib.CM_SetExpTime(_handle, value) : cvCameraCSLib.CM_SetGain(_handle, value);
                if (!accepted) throw new InvalidOperationException(exposure ? "相机拒绝曝光设置。" : "相机拒绝增益设置。");
                var next = _options.Copy();
                if (exposure) next.ExposureMilliseconds = value;
                else next.Gain = value;
                _options = next;
                lock (_frames) _latest = null;
            }).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }

    public static Task<cvCameraCSLib.CameraDiscoverySummary> DiscoverAsync(CameraModel model)
        => Task.Run(() => cvCameraCSLib.SearchCameraIds(new[] { model }));

    public async Task ConnectAsync(StandaloneCameraOptions options, bool live)
    {
        ArgumentNullException.ThrowIfNull(options);
        var copy = options.Copy();
        copy.Validate();
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsConnected) throw new InvalidOperationException("请先断开当前相机，再切换连接参数或采集模式。");
            await Task.Run(() =>
            {
                string config = Path.GetFullPath(copy.ConfigurationFile, AppContext.BaseDirectory);
                if (!File.Exists(config)) throw new FileNotFoundException("相机运行文件缺失，请检查安装目录中的 cfg/sys.cfg。", config);
                IntPtr handle = cvCameraCSLib.CM_CreatCameraManagerV1(copy.Model, copy.Mode, config);
                if (handle == IntPtr.Zero) throw new InvalidOperationException("创建相机 SDK 会话失败，请检查驱动、SDK 配置及许可证。");
                try
                {
                    cvCameraCSLib.CM_SetCameraID(handle, copy.CameraId);
                    if (!cvCameraCSLib.CM_SetTakeImageMode(handle, live ? TakeImageMode.Live : TakeImageMode.Measure_Normal)
                        || !cvCameraCSLib.CM_SetImageBpp(handle, copy.BitDepth))
                        throw new InvalidOperationException("相机拒绝采集模式或位深设置。");
                    int code = cvCameraCSLib.CM_Open(handle);
                    if (code != cvErrorDefine.CV_ERR_SUCCESS) throw NativeError("连接相机", code);
                    if (!cvCameraCSLib.CM_SetGain(handle, copy.Gain) || !cvCameraCSLib.CM_SetExpTime(handle, copy.ExposureMilliseconds))
                        throw new InvalidOperationException("相机拒绝曝光或增益设置。");
                    lock (_frames) { _latest = null; _frameError = null; _acceptFrames = live; }
                    if (live && !cvCameraCSLib.CM_SetCallBack(handle, _callback, IntPtr.Zero))
                        throw new InvalidOperationException("注册相机连续帧回调失败。");
                    _options = copy;
                    IsLive = live;
                    _handle = handle;
                    handle = IntPtr.Zero;
                }
                finally
                {
                    if (handle != IntPtr.Zero)
                    {
                        lock (_frames) _acceptFrames = false;
                        cvCameraCSLib.CM_UnregisterCallBack(handle);
                        cvCameraCSLib.CM_Close(handle);
                        _ = cvCameraCSLib.ReleaseCameraManager(handle);
                    }
                }
            }).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }

    public async Task<StandaloneCameraFrame> CaptureAsync()
    {
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!IsConnected || IsLive || _options == null) throw new InvalidOperationException("请先连接单帧采集模式。");
            return await Task.Run(() => Capture(_handle, _options)).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }

    private static StandaloneCameraFrame Capture(IntPtr handle, StandaloneCameraOptions options)
    {
        uint w = 0, h = 0, depth = 0, channels = 0;
        if (cvCameraCSLib.CM_GetSrcFrameInfo(handle, ref w, ref h, ref depth, ref channels) == 0)
            throw new InvalidOperationException("相机未返回图像格式。");
        if (channels is not (1 or 3)) throw new InvalidOperationException("单帧采集需要 SDK 提供单通道灰度或三通道 BGR 数据。");
        int stride = checked((int)(w * channels * (depth / 8)));
        int length = StandaloneCameraFrame.RequiredBytes(checked((int)w), checked((int)h), checked((int)depth), checked((int)channels), stride);
        var pixels = new byte[length];
        var param = new GetFrameParam { channelCount = (int)channels, measureCount = 1, title = string.Empty, ob = 4, startBurst = 1, endBurst = 3, autoExpFlag = false };
        // Empty calibration list preserves the SDK's packed BGR path. No CIE or file output is requested.
        param.calibrationlist = new();
        // Channel request order matches LocalCameraCaptureService; it is not the packed BGR byte order.
        ImageChannelType[] types = channels == 1 ? new[] { ImageChannelType.Gray_Y } : new[] { ImageChannelType.Gray_X, ImageChannelType.Gray_Y, ImageChannelType.Gray_Z };
        for (int index = 0; index < types.Length; index++)
            param.channels.Add(new ChannelParam { exp = options.ExposureMilliseconds, cfwport = index, channelType = types[index], check = new ChannelCalibration() });
        uint expectedW = w, expectedH = h, expectedDepth = depth, expectedChannels = channels, cieDepth = 32;
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            int code = cvCameraCSLib.CM_GetFrame(handle, JsonConvert.SerializeObject(param), ref w, ref h, ref depth, ref cieDepth, ref channels, pinned.AddrOfPinnedObject(), IntPtr.Zero);
            if (code != cvErrorDefine.CV_ERR_SUCCESS) throw NativeError("取图", code);
        }
        finally { pinned.Free(); }
        if (w != expectedW || h != expectedH || depth != expectedDepth || channels != expectedChannels)
            throw new InvalidOperationException("采集时图像格式发生变化，请重新连接相机。");
        return new(pixels, (int)w, (int)h, (int)depth, (int)channels, stride, DateTimeOffset.Now);
    }

    private ulong OnFrame(int imageType, IntPtr data, int width, int height, int lss, int depth, int channels, IntPtr userData)
    {
        try
        {
            lock (_frames)
            {
                if (!_acceptFrames || data == IntPtr.Zero) return 0;
                int stride = checked(width * channels * (depth / 8));
                int bytes = StandaloneCameraFrame.RequiredBytes(width, height, depth, channels, stride);
                var pixels = new byte[bytes];
                Marshal.Copy(data, pixels, 0, bytes);
                _latest = new(pixels, width, height, depth, channels, stride, DateTimeOffset.Now);
            }
        }
        catch (Exception exception) { lock (_frames) _frameError = exception.Message; }
        return 0;
    }

    public StandaloneCameraFrame? TakeLatestFrame()
    {
        lock (_frames)
        {
            if (_frameError != null) { string error = _frameError; _frameError = null; throw new InvalidOperationException(error); }
            var frame = _latest;
            _latest = null;
            return frame;
        }
    }

    public async Task DisconnectAsync()
    {
        await _commands.WaitAsync().ConfigureAwait(false);
        try { await Task.Run(Close).ConfigureAwait(false); }
        finally { _commands.Release(); }
    }

    private void Close()
    {
        lock (_frames) { _acceptFrames = false; _latest = null; _frameError = null; }
        IntPtr handle = _handle;
        if (handle == IntPtr.Zero) return;
        _handle = IntPtr.Zero;
        _options = null;
        IsLive = false;
        try
        {
            cvCameraCSLib.CM_UnregisterCallBack(handle);
            cvCameraCSLib.CM_Close(handle);
        }
        finally
        {
            _ = cvCameraCSLib.ReleaseCameraManager(handle);
            GC.KeepAlive(_callback);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            await Task.Run(Close).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }

    private static InvalidOperationException NativeError(string operation, int code)
    {
        string message = string.Empty;
        _ = cvCameraCSLib.CM_GetErrorMessage(code, ref message);
        return new InvalidOperationException($"{operation}失败：{code} {message}");
    }
}
