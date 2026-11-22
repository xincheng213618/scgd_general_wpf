using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ColorVision.Experimental.CVLicenseHook;

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int CallWithHandle(IntPtr handle);

/// <summary>
/// 不需要相机硬件的验证程序：
///   A. 内存 mock：手工构造一段与真实 CM_ValiLic 入口字节一致的原生函数，
///      再用一条 E8 直接调用去模拟「CM_Open 内部调用 CM_ValiLic」，跑通 hook / trampoline / 卸载。
///   B. 真实 DLL：加载部署版 cvCamera.dll，运行时确认 CM_Open 里确实有 5 条指向 CM_ValiLic 的
///      E8 直接调用，再用 CM_ValiLic(IntPtr.Zero) 做安全探针（不接触硬件），对比 hook 前后返回值。
/// </summary>
internal static class Program
{
    private const string DefaultDll =
        @"C:\Users\17917\Desktop\CVWindowsService\CVWindowsService\CVMainWindowsService_x64\plugin\Camera\cvCamera.dll";

    private static int _passed;
    private static int _failed;

    private static int Main(string[] args)
    {
        string dll = args.Length > 0 ? args[0] : DefaultDll;

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("===================================================================");
        Console.WriteLine(" cvCamera.dll / CM_ValiLic 运行时 hook 可行性验证 (x64)");
        Console.WriteLine("===================================================================");
        Console.WriteLine($"进程位数      : {(Environment.Is64BitProcess ? "x64" : "x86")}");
        Console.WriteLine($"目标 DLL      : {dll}");
        Console.WriteLine();

        RunMockSuite();
        RunFingerprintSuite(dll);
        RunRealDllSuite(dll);
        RunLegacyCompatSuite();

        Console.WriteLine();
        Console.WriteLine("===================================================================");
        Console.WriteLine($" 结果：通过 {_passed} 项，失败 {_failed} 项");
        Console.WriteLine("===================================================================");
        return _failed == 0 ? 0 : 1;
    }

    private static void Check(string name, int actual, int expected)
    {
        bool ok = actual == expected;
        if (ok) _passed++; else _failed++;
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name,-46} 实际={Describe(actual),-22} 期望={Describe(expected)}");
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok) _passed++; else _failed++;
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name,-46} {detail}");
    }

    private static string Describe(int v) => v switch
    {
        CameraLicenseHook.CV_ERR_SUCCESS => "1 (SUCCESS)",
        CameraLicenseHook.CV_ERR_CAM_LICENSE => "-10044 (LICENSE)",
        CameraLicenseHook.CV_ERR_CAM_HANDLE_INVALID => "-10001 (HANDLE_INVALID)",
        _ => v.ToString()
    };

    // ------------------------------------------------------------------
    // A. 内存 mock
    // ------------------------------------------------------------------
    private static void RunMockSuite()
    {
        Console.WriteLine("--- A. 内存 mock：验证 hook 机制本身（无硬件依赖） ---");

        IntPtr fakeFn, stub;
        IntPtr page = CreateMockFixture(out fakeFn, out stub);
        try
        {
            var callMock = Marshal.GetDelegateForFunctionPointer<CallWithHandle>(stub);

            int before = callMock(IntPtr.Zero);
            Check("未 hook 时原始函数返回拒绝码", before, CameraLicenseHook.CV_ERR_CAM_LICENSE);

            int callbackCalls = 0;
            using (var hook = CameraLicenseHook.InstallAt(
                       fakeFn,
                       _ => { callbackCalls++; return CameraLicenseHook.CV_ERR_SUCCESS; },
                       LicenseHookPolicy.DelegateToUpperLayer))
            {
                Check("入口特征匹配（12 字节完整指令边界）", hook.EntryPrefixHex == "48894C24084881EC08010000", hook.EntryPrefixHex);

                int after = callMock((IntPtr)0x1234);
                Check("E8 直接调用被 detour 拦截 -> 上层放行", after, CameraLicenseHook.CV_ERR_SUCCESS);
                Check("上层统一鉴权回调被调用 1 次", callbackCalls, 1);
                Check("命中计数 = 1", (int)hook.Hits, 1);

                hook.Policy = LicenseHookPolicy.ObserveOnly;
                int observed = callMock(IntPtr.Zero);
                Check("ObserveOnly 经 trampoline 调回原实现", observed, CameraLicenseHook.CV_ERR_CAM_LICENSE);

                hook.Policy = LicenseHookPolicy.DenyAll;
                int denied = callMock(IntPtr.Zero);
                Check("DenyAll 返回许可证错误码", denied, CameraLicenseHook.CV_ERR_CAM_LICENSE);
                Check("累计命中计数 = 3", (int)hook.Hits, 3);
            }

            int restored = callMock(IntPtr.Zero);
            Check("卸载后原始函数字节完整恢复", restored, CameraLicenseHook.CV_ERR_CAM_LICENSE);

            // fail-closed：无回调时不得放行
            using (var hook = CameraLicenseHook.InstallAt(fakeFn, null, LicenseHookPolicy.DelegateToUpperLayer))
            {
                int closed = callMock(IntPtr.Zero);
                Check("上层回调缺失时 fail-closed", closed, CameraLicenseHook.CV_ERR_CAM_LICENSE);
                Check("回调失败被计数", (int)hook.CallbackFailures, 1);
            }
        }
        finally
        {
            Win32.VirtualFree(page, 0, Win32.MEM_RELEASE);
        }
        Console.WriteLine();
    }

    /// <summary>
    /// 构造「与部署版 CM_ValiLic 入口字节完全一致」的 mock 函数，
    /// 以及一条模拟 CM_Open 内部调用的 E8 直接调用桩。返回需 VirtualFree 的页地址。
    /// </summary>
    private static IntPtr CreateMockFixture(out IntPtr fakeFunction, out IntPtr directCallStub)
    {
        IntPtr page = Win32.ExecutableMemory(4096);

        byte[] fake =
        {
            0x48, 0x89, 0x4C, 0x24, 0x08,                    // mov [rsp+8], rcx
            0x48, 0x81, 0xEC, 0x08, 0x01, 0x00, 0x00,        // sub rsp, 108h
            0xB8, 0xC4, 0xD8, 0xFF, 0xFF,                    // mov eax, -10044
            0x48, 0x81, 0xC4, 0x08, 0x01, 0x00, 0x00,        // add rsp, 108h
            0xC3                                              // ret
        };
        fakeFunction = page;
        Marshal.Copy(fake, 0, fakeFunction, fake.Length);

        directCallStub = IntPtr.Add(page, 0x100);
        BuildDirectCallStub(directCallStub, fakeFunction);
        Win32.Flush(page, 4096);
        return page;
    }

    /// <summary>sub rsp,28h / E8 rel32 (直接调用) / add rsp,28h / ret</summary>
    private static void BuildDirectCallStub(IntPtr stub, IntPtr target)
    {
        byte[] code = new byte[24];
        int i = 0;
        code[i++] = 0x48; code[i++] = 0x83; code[i++] = 0xEC; code[i++] = 0x28;   // sub rsp, 28h
        code[i++] = 0xE8;
        int relPos = i;
        long nextInstruction = stub.ToInt64() + relPos + 4;
        long delta = target.ToInt64() - nextInstruction;
        if (delta is < int.MinValue or > int.MaxValue)
            throw new InvalidOperationException("目标超出 E8 rel32 范围");
        BitConverter.GetBytes((int)delta).CopyTo(code, relPos);
        i += 4;
        code[i++] = 0x48; code[i++] = 0x83; code[i++] = 0xC4; code[i++] = 0x28;   // add rsp, 28h
        code[i++] = 0xC3;                                                              // ret
        Marshal.Copy(code, 0, stub, i);
    }

    // ------------------------------------------------------------------
    // B. 定位与版本指纹
    // ------------------------------------------------------------------
    private static void RunFingerprintSuite(string dll)
    {
        Console.WriteLine("--- B. 定位与版本指纹：确认该 hook 哪一份 cvCamera.dll ---");

        string root = @"C:\Users\17917\Desktop";
        var candidates = new[]
        {
            dll,
            Path.Combine(root, @"CVWindowsService\CVWindowsService\CVMainWindowsService_x64\plugin\Camera\cvCamera.dll"),
            Path.Combine(root, @"CVWindowsService\CVWindowsService\CVMainWindowsService_dev\plugin\Camera\cvCamera.dll"),
            Path.Combine(root, @"scgd_general_wpf\DLL\scgd_internal_dll\cvCamera.dll")
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        Console.WriteLine($"  {"文件",-30} {"大小",-10} {"架构",-5} {"构建(UTC)",-12} {"导出",-6} {"ValiLic RVA",-13} {"入口匹配",-9} {"签名",-5} CFG");
        Console.WriteLine("  " + new string('-', 108));

        int inspected = 0;
        foreach (string path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var f = CvCameraLocator.Inspect(path);
                inspected++;
                string shortName = path.Replace(root + "\\", "").Replace("CVWindowsService\\CVWindowsService\\", "srv/");
                if (shortName.Length > 30) shortName = "..." + shortName[^27..];
                Console.WriteLine($"  {shortName,-30} {f.SizeBytes / 1024 + " KB",-10} {f.Architecture,-5} " +
                                  $"{f.BuildTimestampUtc,-12:yyyy-MM-dd} {f.NamedExportCount,-6} " +
                                  $"{("0x" + f.ValiLicRva.ToString("X")),-13} {(f.EntryPrefixMatches ? "YES" : "NO"),-9} " +
                                  $"{(f.HasAuthenticodeSignature ? "YES" : "NO"),-5} {(f.CfgEnabled ? "ON" : "OFF")}");
            }
            catch (Exception ex) when (ex is BadImageFormatException or ArgumentOutOfRangeException)
            {
                Console.WriteLine($"  [WARN] 无法解析 {path}: {ex.Message}");
            }
        }

        Console.WriteLine();
        string? chosen = CvCameraLocator.FindHookable(candidates, out string reason);
        Console.WriteLine("  " + reason);
        Check("定位到唯一可安全 hook 的 x64 版本", chosen is not null, chosen ?? "无匹配");
        Check("选中版本启用 ASLR/DEP 且未启用 CFG", IsSafeConfiguration(chosen),
              "ASLR+DEP 下绝对跳转仍有效；CFG 关闭使入口改写无需额外注册");
        Console.WriteLine();
    }

    private static bool IsSafeConfiguration(string? path)
    {
        if (path is null) return false;
        try
        {
            var f = CvCameraLocator.Inspect(path);
            return f.AslrEnabled && f.DepEnabled && !f.CfgEnabled;
        }
        catch (BadImageFormatException) { return false; }
    }

    // ------------------------------------------------------------------
    // C. 真实部署 DLL
    // ------------------------------------------------------------------
    private static void RunRealDllSuite(string dll)
    {
        Console.WriteLine("--- C. 部署版 cvCamera.dll：运行时确认调用链并实测 hook ---");

        if (!File.Exists(dll))
        {
            Console.WriteLine($"  [SKIP] 文件不存在：{dll}");
            Console.WriteLine("         A 组已验证 hook 机制；把 DLL 路径作为首个参数传入即可跑 B 组。");
            return;
        }

        string? dir = Path.GetDirectoryName(dll);
        Win32.SetDllDirectoryW(dir);   // 让 opencv / qhyccd 等同目录依赖可解析
        IntPtr module = Win32.LoadLibraryW(dll);
        if (module == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            Console.WriteLine($"  [SKIP] LoadLibrary 失败，Win32Error={err} ({new Win32Exception(err).Message})");
            Console.WriteLine("         本机依赖不完整时属正常；A 组机制验证仍有效。");
            return;
        }

        IntPtr cmOpen = Win32.GetProcAddress(module, "CM_Open");
        IntPtr cmVali = Win32.GetProcAddress(module, "CM_ValiLic");
        Check("CM_Open / CM_ValiLic 均已导出", cmOpen != IntPtr.Zero && cmVali != IntPtr.Zero,
              $"CM_Open=0x{cmOpen.ToInt64():X}, CM_ValiLic=0x{cmVali.ToInt64():X}");
        if (cmOpen == IntPtr.Zero || cmVali == IntPtr.Zero) return;

        Console.WriteLine($"  模块基址      : 0x{module.ToInt64():X}");
        Console.WriteLine($"  CM_Open  RVA  : 0x{cmOpen.ToInt64() - module.ToInt64():X}");
        Console.WriteLine($"  CM_ValiLic RVA: 0x{cmVali.ToInt64() - module.ToInt64():X}");

        List<int> callOffsets = FindDirectCalls(cmOpen, cmVali, 0x400);
        Console.WriteLine($"  CM_Open 内指向 CM_ValiLic 的直接调用: {callOffsets.Count} 条，偏移 " +
                          string.Join(", ", callOffsets.Select(o => "+0x" + o.ToString("X"))));
        Check("确认 5 条内部直接调用（源码的 5 个分支）", callOffsets.Count == 5, $"实测 {callOffsets.Count} 条");
        Check("这些是直接调用而非 IAT 调用", callOffsets.Count > 0, "IAT hook 对它们无效，必须 inline hook");

        // 安全探针：handle = NULL 时原实现在 GetHandle 处直接返回，不接触相机硬件。
        var callVali = Marshal.GetDelegateForFunctionPointer<CallWithHandle>(cmVali);
        int before = callVali(IntPtr.Zero);
        Check("hook 前 CM_ValiLic(NULL) 返回句柄无效", before, CameraLicenseHook.CV_ERR_CAM_HANDLE_INVALID);

        int authCalls = 0;
        var hook = CameraLicenseHook.Install(
            module,
            _ => { authCalls++; return CameraLicenseHook.CV_ERR_SUCCESS; },
            LicenseHookPolicy.DelegateToUpperLayer);
        try
        {
            Check("真实 DLL 入口特征匹配，hook 安装成功", hook.IsInstalled, hook.EntryPrefixHex);

            int after = callVali(IntPtr.Zero);
            Check("hook 后 CM_ValiLic(NULL) 由上层放行", after, CameraLicenseHook.CV_ERR_SUCCESS);
            Check("上层统一鉴权回调被调用", authCalls, 1);

            hook.Policy = LicenseHookPolicy.ObserveOnly;
            int observed = callVali(IntPtr.Zero);
            Check("ObserveOnly 走 trampoline 调回原实现", observed, CameraLicenseHook.CV_ERR_CAM_HANDLE_INVALID);

            hook.Policy = LicenseHookPolicy.DenyAll;
            int denied = callVali(IntPtr.Zero);
            Check("DenyAll 可主动拒绝", denied, CameraLicenseHook.CV_ERR_CAM_LICENSE);
            Check("累计命中计数 = 3", (int)hook.Hits, 3);
        }
        finally
        {
            hook.Dispose();
        }

        int restored = callVali(IntPtr.Zero);
        Check("卸载后恢复原有许可证校验路径", restored, CameraLicenseHook.CV_ERR_CAM_HANDLE_INVALID);
        Win32.SetDllDirectoryW(null);
    }

    /// <summary>在已加载模块的 .text 中扫 E8 rel32，找出落到 target 的调用点。</summary>
    private static List<int> FindDirectCalls(IntPtr function, IntPtr target, int range)
    {
        var offsets = new List<int>();
        long baseAddr = function.ToInt64();
        for (int i = 0; i < range - 5; i++)
        {
            IntPtr p = IntPtr.Add(function, i);
            if (Marshal.ReadByte(p) != 0xE8) continue;
            int rel = Marshal.ReadInt32(IntPtr.Add(p, 1));
            long destination = baseAddr + i + 5 + rel;
            if (destination == target.ToInt64()) offsets.Add(i);
        }
        return offsets;
    }

    // ------------------------------------------------------------------
    // D. .NET Framework 4.7.2 兼容实现（服务侧可直接复制的那一份）
    // ------------------------------------------------------------------
    private static void RunLegacyCompatSuite()
    {
        Console.WriteLine();
        Console.WriteLine("--- D. .NET Framework 4.7.2 / C# 7 兼容实现（服务侧接入版）---");

        IntPtr fakeFn, stub;
        IntPtr page = CreateMockFixture(out fakeFn, out stub);
        try
        {
            var callMock = Marshal.GetDelegateForFunctionPointer<CallWithHandle>(stub);
            int before = callMock(IntPtr.Zero);
            Check("兼容版：未 hook 时返回拒绝码", before, CameraLicenseHook.CV_ERR_CAM_LICENSE);

            int authCalls = 0;
            using (var hook = Legacy.ServiceCameraLicenseHook.InstallAt(
                       fakeFn,
                       _ => { authCalls++; return Legacy.ServiceCameraLicenseHook.CV_ERR_SUCCESS; }))
            {
                int after = callMock(IntPtr.Zero);
                Check("兼容版：E8 直接调用被拦截并放行", after, CameraLicenseHook.CV_ERR_SUCCESS);
                Check("兼容版：上层鉴权回调被调用", authCalls, 1);
                Check("兼容版：命中计数 = 1", (int)hook.Hits, 1);

                int original = hook.CallOriginal(IntPtr.Zero);
                Check("兼容版：trampoline 可调用原实现", original, CameraLicenseHook.CV_ERR_CAM_LICENSE);
            }

            int restored = callMock(IntPtr.Zero);
            Check("兼容版：卸载后完全恢复", restored, CameraLicenseHook.CV_ERR_CAM_LICENSE);
        }
        finally
        {
            Win32.VirtualFree(page, 0, Win32.MEM_RELEASE);
        }
    }
}
