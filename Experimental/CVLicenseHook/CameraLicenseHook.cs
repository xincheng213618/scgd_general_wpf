namespace ColorVision.Experimental.CVLicenseHook;

/// <summary>hook 后的决策来源。</summary>
public enum LicenseHookPolicy
{
    /// <summary>交给上层统一鉴权回调；回调异常或无回调时 fail-closed。</summary>
    DelegateToUpperLayer = 0,

    /// <summary>实验对照：一律放行，返回 CV_ERR_SUCCESS。</summary>
    AllowAll = 1,

    /// <summary>实验对照：一律拒绝，返回 CV_ERR_CAM_LICENSE。</summary>
    DenyAll = 2,

    /// <summary>只计数，不改动判定：经 trampoline 调用原 CM_ValiLic 并原样返回。</summary>
    ObserveOnly = 3
}

/// <summary>
/// 把 cvCamera.dll 内嵌的许可证校验，改为由宿主（服务 / 主程序）统一裁决。
///
/// 返回值沿用 cvErrorDefine.h 的约定：
///   CV_ERR_SUCCESS      = 1      放行，CM_Open 继续执行
///   CV_ERR_CAM_LICENSE  = -10044 拒绝；原实现在失败时还会调用 CM_Close(handle)
///
/// 注意：CM_Open 把 CM_ValiLic 的返回值直接当作自己的返回值，
/// 所以拒绝时不要再自行 CM_Close，避免与上层资源状态不一致。
/// </summary>
public sealed class CameraLicenseHook : IDisposable
{
    public const int CV_ERR_SUCCESS = 1;
    public const int CV_ERR_CAM_LICENSE = -10044;
    public const int CV_ERR_CAM_HANDLE_INVALID = -10001;

    private readonly Func<IntPtr, int>? _authorize;
    private InlineHook? _hook;

    // 实例方法委托必须保持引用，否则 GC 回收后原生跳转会指向已释放的 thunk。
    private InlineHook.ValidationProc? _detourProc;

    private int _policy;
    private long _hits;
    private long _allows;
    private long _denies;
    private long _callbackFailures;

    public long Hits => Interlocked.Read(ref _hits);
    public long Allows => Interlocked.Read(ref _allows);
    public long Denies => Interlocked.Read(ref _denies);
    public long CallbackFailures => Interlocked.Read(ref _callbackFailures);
    public bool IsInstalled => _hook?.IsInstalled == true;
    public string EntryPrefixHex => _hook?.PrefixHex ?? string.Empty;

    public LicenseHookPolicy Policy
    {
        get => (LicenseHookPolicy)Volatile.Read(ref _policy);
        set => Volatile.Write(ref _policy, (int)value);
    }

    private CameraLicenseHook(Func<IntPtr, int>? authorize, LicenseHookPolicy policy)
    {
        _authorize = authorize;
        Volatile.Write(ref _policy, (int)policy);
    }

    /// <summary>
    /// 在服务进程内安装。cvCamera.dll 必须已经加载（LoadLibrary / GetModuleHandle）。
    /// 安装时机建议放在插件初始化完成、任何 CM_Open / CM_OpenEx 之前。
    /// </summary>
    public static CameraLicenseHook Install(
        IntPtr cvCameraModule,
        Func<IntPtr, int>? authorize,
        LicenseHookPolicy policy = LicenseHookPolicy.DelegateToUpperLayer,
        bool verifyEntryPrefix = true)
    {
        if (cvCameraModule == IntPtr.Zero)
            throw new ArgumentException("cvCamera.dll 尚未加载", nameof(cvCameraModule));

        IntPtr target = Win32.GetProcAddress(cvCameraModule, "CM_ValiLic");
        if (target == IntPtr.Zero)
            throw new EntryPointNotFoundException("cvCamera.dll 未导出 CM_ValiLic");

        return InstallAt(target, authorize, policy, verifyEntryPrefix);
    }

    /// <summary>直接对已解析的函数地址安装；验证程序用它在内存 mock 上跑通全套逻辑。</summary>
    internal static CameraLicenseHook InstallAt(
        IntPtr target,
        Func<IntPtr, int>? authorize,
        LicenseHookPolicy policy = LicenseHookPolicy.DelegateToUpperLayer,
        bool verifyEntryPrefix = true)
    {
        var service = new CameraLicenseHook(authorize, policy);
        service._detourProc = service.Detour;
        service._hook = InlineHook.Install(target, service._detourProc, verifyEntryPrefix);
        return service;
    }

    /// <summary>非托管入口。绝不向非托管边界抛异常。</summary>
    private int Detour(IntPtr handle)
    {
        Interlocked.Increment(ref _hits);
        try
        {
            switch (Policy)
            {
                case LicenseHookPolicy.AllowAll:
                    Interlocked.Increment(ref _allows);
                    return CV_ERR_SUCCESS;

                case LicenseHookPolicy.DenyAll:
                    Interlocked.Increment(ref _denies);
                    return CV_ERR_CAM_LICENSE;

                case LicenseHookPolicy.ObserveOnly:
                {
                    InlineHook.ValidationProc? original = _hook?.OriginalCall;
                    if (original is null)
                    {
                        Interlocked.Increment(ref _denies);
                        return CV_ERR_CAM_LICENSE;
                    }
                    int originalResult = original(handle);
                    if (originalResult == CV_ERR_SUCCESS) Interlocked.Increment(ref _allows);
                    else Interlocked.Increment(ref _denies);
                    return originalResult;
                }

                default:
                {
                    if (_authorize is null)
                    {
                        Interlocked.Increment(ref _callbackFailures);
                        Interlocked.Increment(ref _denies);
                        return CV_ERR_CAM_LICENSE;   // fail-closed
                    }

                    int decision;
                    try
                    {
                        decision = _authorize(handle);
                    }
                    catch
                    {
                        Interlocked.Increment(ref _callbackFailures);
                        Interlocked.Increment(ref _denies);
                        return CV_ERR_CAM_LICENSE;   // 上层鉴权异常时不允许放行
                    }

                    if (decision == CV_ERR_SUCCESS)
                    {
                        Interlocked.Increment(ref _allows);
                        return CV_ERR_SUCCESS;
                    }

                    Interlocked.Increment(ref _denies);
                    return CV_ERR_CAM_LICENSE;
                }
            }
        }
        catch
        {
            // 兜底：任何非预期异常都不能穿透到 CM_Open 的调用栈。
            Interlocked.Increment(ref _denies);
            return CV_ERR_CAM_LICENSE;
        }
    }

    public void Dispose()
    {
        _hook?.Dispose();
        _hook = null;
        _detourProc = null;
    }
}
