using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace ColorVision.Experimental.CVLicenseHook.Legacy
{
    /// <summary>
    /// 单文件、C# 7.0 / .NET Framework 4.7.2 兼容版本。
    /// 可直接整文件复制进 cvwindowsservice（CameraPlugin 或 CameraWindowsService）工程，
    /// 不需要 NuGet 包、不需要 native DLL、不需要注入其它进程。
    ///
    /// 调用时机：Device_DynamicAPI_CVCamera 构造完成（cvCamera.dll 已 LoadLibrary）之后，
    ///           任何 CM_Open / CM_OpenEx 之前；建议在服务 OnStart 的单线程阶段安装。
    ///
    /// 用法：
    ///   _hook = ServiceCameraLicenseHook.Install("cvCamera.dll", handle => {
    ///       // 这里接上层统一鉴权：GetLicenseBySN + CheckLicense / IsLicenseValidate
    ///       return IsLicenseValidate ? 1 : -10044;
    ///   });
    ///   // OnStop:  _hook.Dispose();
    ///
    /// 原理与已验证的 net10 版本完全一致：改 CM_ValiLic 入口 12 字节为绝对跳转，
    /// 一处覆盖 CM_Open 内部 5 条 E8 直接调用 + C# 侧缓存的 GetProcAddress 函数指针。
    /// </summary>
    public sealed class ServiceCameraLicenseHook : IDisposable
    {
        public const int CV_ERR_SUCCESS = 1;
        public const int CV_ERR_CAM_LICENSE = -10044;
        public const int CV_ERR_CAM_HANDLE_INVALID = -10001;

        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const int PATCH_LENGTH = 12;

        // CM_ValiLic 入口：mov [rsp+8],rcx (5) + sub rsp,108h (7) —— 恰好两条完整指令
        private static readonly byte[] KnownPrefix = new byte[]
        {
            0x48, 0x89, 0x4C, 0x24, 0x08,
            0x48, 0x81, 0xEC, 0x08, 0x01, 0x00, 0x00
        };

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ValidationProc(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetModuleHandleW(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protection, out uint oldProtection);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protection);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        private readonly Func<IntPtr, int> _authorize;
        private readonly byte[] _saved = new byte[PATCH_LENGTH];
        private IntPtr _target;
        private IntPtr _trampoline;
        private uint _oldProtection;
        private long _hits;

        // 必须作为字段长期持有：原生函数指针不持有 GC 根
        private ValidationProc _detour;
        private ValidationProc _originalCall;
        private bool _installed;
        private bool _disposed;

        public long Hits { get { return Interlocked.Read(ref _hits); } }
        public bool IsInstalled { get { return _installed; } }

        private ServiceCameraLicenseHook(Func<IntPtr, int> authorize)
        {
            _authorize = authorize;
        }

        public static ServiceCameraLicenseHook Install(string moduleName, Func<IntPtr, int> authorize)
        {
            if (!Environment.Is64BitProcess)
                throw new PlatformNotSupportedException("该补丁仅适用于 x64 进程。");
            if (authorize == null)
                throw new ArgumentNullException("authorize");

            IntPtr module = GetModuleHandleW(moduleName);
            if (module == IntPtr.Zero)
                throw new InvalidOperationException("模块尚未加载：" + moduleName);

            IntPtr target = GetProcAddress(module, "CM_ValiLic");
            if (target == IntPtr.Zero)
                throw new EntryPointNotFoundException("模块未导出 CM_ValiLic：" + moduleName);

            return InstallAt(target, authorize);
        }

        /// <summary>验证程序可直接传函数地址；服务里用上面的 Install(moduleName, ...) 即可。</summary>
        internal static ServiceCameraLicenseHook InstallAt(IntPtr target, Func<IntPtr, int> authorize)
        {
            ServiceCameraLicenseHook hook = new ServiceCameraLicenseHook(authorize);
            hook._target = target;
            hook._detour = hook.Detour;

            Marshal.Copy(target, hook._saved, 0, PATCH_LENGTH);
            for (int i = 0; i < PATCH_LENGTH; i++)
            {
                if (hook._saved[i] != KnownPrefix[i])
                    throw new InvalidOperationException(
                        "CM_ValiLic 入口字节与已知版本不符，拒绝安装（版本漂移防呆）。");
            }

            hook.BuildTrampoline();
            hook.WritePatch();
            hook._installed = true;
            return hook;
        }

        private void BuildTrampoline()
        {
            _trampoline = VirtualAlloc(IntPtr.Zero, new UIntPtr(64), MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
            if (_trampoline == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAlloc 失败");

            byte[] code = new byte[26];
            Array.Copy(_saved, 0, code, 0, PATCH_LENGTH);
            code[12] = 0xFF;
            code[13] = 0x25;                                  // jmp qword ptr [rip+0]
            byte[] back = BitConverter.GetBytes(_target.ToInt64() + PATCH_LENGTH);
            Array.Copy(back, 0, code, 18, 8);

            Marshal.Copy(code, 0, _trampoline, code.Length);
            FlushInstructionCache(GetCurrentProcess(), _trampoline, new UIntPtr(64));
            _originalCall = (ValidationProc)Marshal.GetDelegateForFunctionPointer(_trampoline, typeof(ValidationProc));
        }

        private void WritePatch()
        {
            if (!VirtualProtect(_target, new UIntPtr(16), PAGE_EXECUTE_READWRITE, out _oldProtection))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualProtect(RWX) 失败");

            byte[] patch = new byte[PATCH_LENGTH];
            patch[0] = 0x48;                                  // mov rax, imm64
            patch[1] = 0xB8;
            byte[] address = BitConverter.GetBytes(Marshal.GetFunctionPointerForDelegate(_detour).ToInt64());
            Array.Copy(address, 0, patch, 2, 8);
            patch[10] = 0xFF;                                 // jmp rax
            patch[11] = 0xE0;

            Marshal.Copy(patch, 0, _target, PATCH_LENGTH);
            FlushInstructionCache(GetCurrentProcess(), _target, new UIntPtr(16));

            uint ignore;
            VirtualProtect(_target, new UIntPtr(16), _oldProtection, out ignore);
        }

        private int Detour(IntPtr handle)
        {
            Interlocked.Increment(ref _hits);
            try
            {
                // 上层统一鉴权异常 / 返回非成功码时一律 fail-closed，绝不向非托管边界抛异常
                return _authorize(handle) == CV_ERR_SUCCESS ? CV_ERR_SUCCESS : CV_ERR_CAM_LICENSE;
            }
            catch
            {
                return CV_ERR_CAM_LICENSE;
            }
        }

        /// <summary>需要观测原校验结果时使用（经 trampoline，不经过 detour 决策）。</summary>
        public int CallOriginal(IntPtr handle)
        {
            if (_originalCall == null)
                throw new InvalidOperationException("hook 未安装或已卸载");
            return _originalCall(handle);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_installed)
            {
                uint previous;
                if (VirtualProtect(_target, new UIntPtr(16), PAGE_EXECUTE_READWRITE, out previous))
                {
                    Marshal.Copy(_saved, 0, _target, PATCH_LENGTH);
                    FlushInstructionCache(GetCurrentProcess(), _target, new UIntPtr(16));
                    uint ignore;
                    VirtualProtect(_target, new UIntPtr(16), previous, out ignore);
                }
                _installed = false;
            }

            if (_trampoline != IntPtr.Zero)
            {
                VirtualFree(_trampoline, UIntPtr.Zero, MEM_RELEASE);
                _trampoline = IntPtr.Zero;
            }
            _originalCall = null;
            _detour = null;
        }
    }
}
