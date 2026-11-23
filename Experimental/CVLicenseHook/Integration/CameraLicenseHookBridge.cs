namespace ColorVision.Experimental.CVLicenseHook.Integration;

/// <summary>
/// 接入示例（自包含，可整段复制到宿主工程）。
///
/// ── 装在哪里 ──────────────────────────────────────────────────────────
/// 必须在「cvCamera.dll 已加载」之后、「任何 CM_Open / CM_OpenEx」之前，且在同一进程内：
///   * Windows 服务侧：CVCameraPlugin 的 Device_DynamicAPI_CVCamera 构造完成
///     （它内部 LoadLibrary + InitFunc_Address 缓存函数指针）之后调用 InstallIfEnabled。
///     注意：缓存的是函数地址，inline hook 改的是该地址处的函数体，所以缓存指针照样生效。
///   * ColorVision 主程序侧：Engine 中加载本地相机的初始化路径。
///
/// ── 上层统一鉴权怎么接 ────────────────────────────────────────────────
/// authorize 回调返回 1 放行、其它值拒绝。可在其中调用：
///   * 主程序：LocalCameraLicense.EnsureAvailable(PhyCamera?.CameraLicenseModel?.ExpiryDate,
///             ExperimentalFeaturesConfig.Instance.IsEnabled, DateTime.Now)  —— 抛异常即拒绝
///   * 服务侧：CameraDevice.cs 的 GetLicenseBySN(cameraSN) + CheckLicense(camera_lic)
///             / IsLicenseValidate，或统一的 VScgdCameraLicenseAll 查询
/// 把 IntPtr handle 映射回设备 SN 时，用宿主自己维护的 handle→设备 字典，
/// 不要在 native 回调里做阻塞式数据库/网络调用（见下方风险说明）。
///
/// ── 开关 ──────────────────────────────────────────────────────────────
/// 用 ExperimentalFeaturesConfig.Instance.IsEnabled 控制；关闭时不安装，
/// 或调用 Shutdown() 恢复原始字节，这样实验功能可以随时回退。
/// </summary>
public static class CameraLicenseHookBridge
{
    private static CameraLicenseHook? s_hook;
    private static readonly object s_gate = new();

    public static bool IsInstalled { get { lock (s_gate) return s_hook?.IsInstalled == true; } }

    /// <summary>
    /// experimentalFeaturesEnabled 传实验功能开关；authorize 为上层统一鉴权。
    /// 返回是否成功安装。
    /// </summary>
    public static bool InstallIfEnabled(bool experimentalFeaturesEnabled, Func<IntPtr, int> authorize)
    {
        if (!experimentalFeaturesEnabled) return false;
        if (!Environment.Is64BitProcess) return false;

        lock (s_gate)
        {
            if (s_hook is not null) return true;

            // 只挂在已经加载的模块上，不改变 DLL 搜索路径、不注入其它进程。
            IntPtr module = Win32.GetModuleHandleW("cvCamera.dll");
            if (module == IntPtr.Zero) return false;

            try
            {
                s_hook = CameraLicenseHook.Install(module, authorize, LicenseHookPolicy.DelegateToUpperLayer);
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or EntryPointNotFoundException)
            {
                // 版本漂移时不强行 hook：保留原有校验路径，让上层走降级逻辑。
                return false;
            }
        }
    }

    /// <summary>示例：把主程序的 LocalCameraLicense 语义适配成 native 回调的返回值。</summary>
    public static Func<IntPtr, int> FromExpiryCheck(Func<IntPtr, DateTime?> resolveExpiry, DateTime nowUtc)
        => handle =>
        {
            try
            {
                DateTime? expiry = resolveExpiry(handle);
                return expiry.HasValue && expiry.Value > nowUtc
                    ? CameraLicenseHook.CV_ERR_SUCCESS
                    : CameraLicenseHook.CV_ERR_CAM_LICENSE;
            }
            catch
            {
                return CameraLicenseHook.CV_ERR_CAM_LICENSE;   // fail-closed
            }
        };

    public static void Shutdown()
    {
        lock (s_gate)
        {
            s_hook?.Dispose();
            s_hook = null;
        }
    }
}

/*
 * ── 风险与约束（上线前必读） ────────────────────────────────────────────
 * 1. 只改进程内存，不动磁盘文件：两份 cvCamera.dll 均无 Authenticode 签名
 *    (Security Directory = 0)，但运行时 hook 仍优于二进制补丁 ——
 *    不改动交付文件、不被升级覆盖、可用实验开关随时启停与回退，
 *    也不会破坏现场可能存在的文件哈希基线 / 完整性校验。
 * 2. 该 DLL 未启用 CFG (DllCharacteristics=0x160)，但启用了 ASLR + DEP；
 *    detour 使用绝对地址跳转，不受 ASLR 影响。若将来开启 CFG，需重新评估。
 * 3. 安装/卸载不是线程安全的原子操作：请在服务启动的单线程阶段安装，
 *    运行期不要反复装拆。detour 本身用 Interlocked 计数，可并发进入。
 * 4. detour 运行在 CM_Open 的调用栈上（相机打开线程）。托管回调会触发
 *    reverse P/Invoke；请勿在其中做长时间阻塞、UI 调度或日志同步写盘。
 *    建议上层鉴权结果预先缓存在内存里，回调内只做字典查找。
 * 5. 入口 12 字节特征与版本绑定。换 DLL 版本后必须重新核对，
 *    否则 Install 会主动抛 InvalidOperationException 拒绝安装（防呆）。
 * 6. 这是「实验功能」。既然 scgd_internal_dll 源码在手，长期正解仍是
 *    用已移除校验的源码重新编译 cvCamera.dll；hook 只用于无法替换二进制的存量部署。
 */
