using ColorVision.Engine.Services.PhyCameras.Configs;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal sealed record LocalMotorSettings(bool Enabled, bool CameraLinkage, FOCUS_COMMUN Controller, string Port, int BaudRate,
    int Speed, int Acceleration, int Deceleration, int HomeAcceleration, int HomeHighSpeed, int HomeLowSpeed,
    GoHome_WAY HomeWay, int HomeTimeout, int Timeout, int Minimum, int Maximum, FindFuncModel Search, bool UseVid, string VidFile)
{
    internal static LocalMotorSettings FromConfig(MotorConfig config) => new(config.IsUseMotor, config.IsCameraLinkage, config.eFOCUSCOMMUN,
        config.SzComName?.Trim().ToUpperInvariant() ?? string.Empty, config.BaudRate, config.RunSpeed, config.RunAcceleration,
        config.Deceleration, config.HomeAcceleration, config.HomeHightSpeed, config.HomeLowSpeed, config.GoHomeWay,
        config.HomeTimeout, config.DwTimeOut, config.MinPosition, config.MaxPosition, config.FindFuncModel,
        config.VID?.IsUseVID == true, config.VID?.MappingFileName ?? string.Empty);

    internal void Validate()
    {
        if (!Enabled) return;
        if (!Enum.IsDefined(Controller) || !Regex.IsMatch(Port, @"^COM[1-9][0-9]*$") || !int.TryParse(Port.AsSpan(3), out _) || BaudRate <= 0)
            throw new ArgumentException("请配置有效的电机类型、COM 端口和波特率。");
        if (Minimum < 0 || Minimum >= Maximum || Timeout <= 0 || HomeTimeout <= 0 || !Enum.IsDefined(HomeWay) || !Enum.IsDefined(Search)
            || Speed <= 0 || Acceleration <= 0 || Deceleration <= 0 || HomeAcceleration <= 0 || HomeHighSpeed <= 0 || HomeLowSpeed <= 0)
            throw new ArgumentException("电机行程、运行/回零速度、加减速度和超时配置无效。");
    }
}

internal interface ILocalCameraMotorNative
{
    int Open(ref IntPtr handle, LocalMotorSettings settings);
    void Configure(IntPtr handle, LocalMotorSettings settings);
    int Close(IntPtr handle);
    int Move(IntPtr handle, int position, int timeout);
    int GetPosition(IntPtr handle, out int position, int timeout);
    int Home(IntPtr handle, LocalMotorSettings settings);
    int Aperture(IntPtr handle, float aperture, int timeout);
    void PrepareFocus(IntPtr cameraHandle, float exposure, float gain);
    int EvaluateFocus(IntPtr cameraHandle, EvaFunc evaluation, out double value);
}

internal sealed class LocalCameraMotorNative : ILocalCameraMotorNative
{
    // The native motor registry is process-wide and does not synchronize its map.
    private static readonly object nativeSync = new();
    public int Open(ref IntPtr handle, LocalMotorSettings settings)
    {
        lock (nativeSync) return cvCameraCSLib.CM_InitComEx(ref handle, settings.Controller, settings.Port, (uint)settings.BaudRate);
    }
    public void Configure(IntPtr handle, LocalMotorSettings settings)
    {
        lock (nativeSync)
        {
            LocalCameraMotorSession.Check(cvCameraCSLib.CM_ClearAlarm(handle), "清除电机报警");
            LocalCameraMotorSession.Check(cvCameraCSLib.CM_SetGoHomeSpeed(handle, settings.HomeAcceleration, settings.HomeHighSpeed, settings.HomeLowSpeed), "设置电机回零速度");
            LocalCameraMotorSession.Check(cvCameraCSLib.CM_SetRunSpeed(handle, settings.Speed, settings.Acceleration, settings.Deceleration), "设置电机运行速度");
            LocalCameraMotorSession.Check(cvCameraCSLib.CM_SetMotorScope(handle, settings.Minimum, settings.Maximum), "设置电机行程");
        }
    }
    public int Close(IntPtr handle) { lock (nativeSync) return cvCameraCSLib.CM_ShutDown(handle); }
    public int Move(IntPtr handle, int position, int timeout) { lock (nativeSync) return cvCameraCSLib.CM_MoveAbsPostion(handle, position, (uint)timeout); }
    public int GetPosition(IntPtr handle, out int position, int timeout) { lock (nativeSync) return cvCameraCSLib.CM_GetPosition(handle, out position, (uint)timeout); }
    public int Home(IntPtr handle, LocalMotorSettings settings) { lock (nativeSync) return cvCameraCSLib.CM_GoHomeEx(handle, (int)settings.HomeWay, (uint)settings.HomeTimeout); }
    public int Aperture(IntPtr handle, float aperture, int timeout) { lock (nativeSync) return cvCameraCSLib.CM_MoveDiaphragm(handle, aperture, (uint)timeout); }
    public void PrepareFocus(IntPtr cameraHandle, float exposure, float gain)
    {
        LocalCameraMotorSession.Check(cvCameraCSLib.CM_SetGain(cameraHandle, gain), "设置对焦增益");
        LocalCameraMotorSession.Check(cvCameraCSLib.CM_SetExpTime(cameraHandle, exposure), "设置对焦曝光");
    }
    public int EvaluateFocus(IntPtr cameraHandle, EvaFunc evaluation, out double value) => cvCameraCSLib.CM_EvaluateFocus(cameraHandle, evaluation, out value);
}

// All instance operations run under the owning LocalCameraSession.SyncRoot.
internal sealed class LocalCameraMotorSession(ILocalCameraMotorNative native)
{
    private static readonly object ownersSync = new();
    private static readonly Dictionary<string, LocalCameraMotorSession> owners = new(StringComparer.OrdinalIgnoreCase);
    private IntPtr handle;
    private LocalMotorSettings? connection;
    private bool ready;
    private string? openError;

    internal void Open(MotorConfig config)
    {
        var settings = LocalMotorSettings.FromConfig(config);
        if (connection != null) { EnsureConfiguration(config); return; }
        connection = settings;
        try
        {
            settings.Validate();
            if (!settings.Enabled) return;
            lock (ownersSync)
            {
                if (owners.TryGetValue(settings.Port, out var owner) && !ReferenceEquals(owner, this))
                    throw new InvalidOperationException($"电机端口 {settings.Port} 已由另一台本地相机占用，请先关闭该相机。");
                owners[settings.Port] = this;
            }
            Check(native.Open(ref handle, settings), "打开本地相机电机");
            if (handle == IntPtr.Zero) throw new InvalidOperationException("电机返回打开成功，但未建立句柄。");
            native.Configure(handle, settings);
            ready = true;
            openError = null;
        }
        catch (Exception error)
        {
            ready = false;
            openError = error.Message;
            try { Close(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            connection = settings;
            openError = error.Message;
            throw;
        }
    }

    internal void EnsureConfiguration(MotorConfig config)
    {
        if (connection != LocalMotorSettings.FromConfig(config)) throw new InvalidOperationException("电机配置已更改，请先关闭本地相机后重新连接。");
    }

    private LocalMotorSettings RequireOpen()
    {
        if (handle == IntPtr.Zero || connection?.Enabled != true || !ready)
            throw new InvalidOperationException(openError == null ? "本地相机未启用或未连接电机，请配置后重新连接相机。" : $"本地电机连接失败：{openError}。请关闭相机后重新连接。");
        return connection;
    }

    internal int ReadPosition()
    {
        var settings = RequireOpen();
        Check(native.GetPosition(handle, out int position, settings.Timeout), "读取本地电机位置");
        return position;
    }

    internal int Move(int position, bool absolute)
    {
        var settings = RequireOpen();
        long target = absolute ? position : (long)ReadPosition() + position;
        if (target < settings.Minimum || target > settings.Maximum) throw new ArgumentOutOfRangeException(nameof(position), $"目标位置 {target} 超出电机行程 [{settings.Minimum}, {settings.Maximum}]。");
        Check(native.Move(handle, (int)target, settings.Timeout), "移动本地电机");
        return ReadPosition();
    }

    internal int Home()
    {
        Check(native.Home(handle, RequireOpen()), "本地电机回零");
        return ReadPosition();
    }

    internal void Aperture(double aperture)
    {
        var settings = RequireOpen();
        if (!double.IsFinite(aperture) || aperture <= 0 || aperture > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(aperture), "光圈必须为大于 0 的有限值。");
        Check(native.Aperture(handle, (float)aperture, settings.Timeout), "设置本地镜头光圈");
    }

    internal LocalAutoFocusResult AutoFocus(IntPtr cameraHandle, LocalAutoFocusSettings focus, float exposure, float gain, CancellationToken token)
    {
        var settings = RequireOpen();
        focus.Validate(settings.Minimum, settings.Maximum);
        if (!float.IsFinite(exposure) || exposure <= 0 || !float.IsFinite(gain) || gain < 0)
            throw new ArgumentException("对焦曝光须为正有限值，增益须为非负有限值。");
        token.ThrowIfCancellationRequested();
        var watch = Stopwatch.StartNew();
        native.PrepareFocus(cameraHandle, exposure, gain);
        var result = LocalAutoFocusSearch.Run(focus, (position, remaining) =>
        {
            Check(native.Move(handle, position, Math.Min(settings.Timeout, remaining)), "自动对焦移动电机");
            token.ThrowIfCancellationRequested();
            if (watch.ElapsedMilliseconds >= focus.Timeout) throw new TimeoutException("本地自动对焦超时，已停止后续取图和移动。");
            Check(native.EvaluateFocus(cameraHandle, focus.Evaluation, out double value), "自动对焦取图评价");
            return value;
        }, (position, remaining) => Check(native.Move(handle, position, Math.Min(settings.Timeout, remaining)), "移动至最佳对焦位置"), token, () => watch.ElapsedMilliseconds);
        return result with { VidPosition = ConvertVid(result.Position) };
    }

    internal double? ConvertVid(int position)
    {
        var settings = RequireOpen();
        if (!settings.UseVid) return null;
        try
        {
            var root = JObject.Parse(File.ReadAllText(settings.VidFile));
            JToken coefficients = root;
            if (root["VidNewVision"] is JObject split)
            {
                int boundary = split.Value<int?>("positionValue") ?? throw new FormatException("缺少 positionValue。");
                coefficients = split[position > boundary ? "longSighted" : "shortSighted"] ?? throw new FormatException("缺少 VID 分段系数。");
            }
            double Number(string name) => coefficients.Value<double?>(name) ?? throw new FormatException($"缺少 VID 系数 {name}。");
            double value = (position + Number("a")) / (Number("b") * position + Number("c")) + Number("d");
            if (!double.IsFinite(value)) throw new FormatException("VID 映射结果不是有限值。");
            return value;
        }
        catch (Exception error) { throw new InvalidOperationException("读取电机 VID 映射失败，请检查映射文件和系数。", error); }
    }

    internal void Close()
    {
        if (handle != IntPtr.Zero)
        {
            // On failure retain the handle and port reservation for an explicit retry.
            Check(native.Close(handle), "关闭本地相机电机");
            handle = IntPtr.Zero;
        }
        lock (ownersSync)
        {
            if (connection != null && owners.TryGetValue(connection.Port, out var owner) && ReferenceEquals(owner, this)) owners.Remove(connection.Port);
        }
        connection = null;
        ready = false;
        openError = null;
    }

    internal static void Check(int code, string operation)
    {
        if (code != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException(operation, code);
    }
}
