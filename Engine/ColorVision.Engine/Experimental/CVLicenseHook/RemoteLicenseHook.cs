using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace ColorVision.Experimental.CVLicenseHook
{
    /// <summary>
    /// 跨进程 inline hook：对「已经在运行的服务进程」里的 licenseClientDLL.dll 打补丁，
    /// 把 LicenseValidate 替换为一段读取上层策略的原生 stub。
    ///
    /// ── 为什么不用注入 DLL / 远程线程 ──────────────────────────────────
    /// 托管 detour 的函数地址只在本进程有效，跨进程必须有一段常驻目标进程的原生代码。
    /// 这里用 VirtualAllocEx 在目标进程里放 27 字节机器码 stub，
    /// 它只做两件事：累加命中计数、读取策略整数并作为返回值。
    /// 上层（本程序）用 WriteProcessMemory 推送鉴权结论，用 ReadProcessMemory 取命中数。
    /// 因此不需要 C++ 编译器、不需要 LoadLibraryW 远程线程、不触碰 CLR / Loader Lock。
    ///
    /// ── stub 机器码（x64，替换整个 LicenseValidate，四个入参不再需要）────
    ///   48 B8 &lt;hitAddr&gt;      mov rax, hitAddr
    ///   F0 48 FF 00            lock inc qword ptr [rax]
    ///   48 B8 &lt;policyAddr&gt;   mov rax, policyAddr
    ///   8B 00                  mov eax, dword ptr [rax]
    ///   C3                     ret
    ///
    /// ── patch（前 12 字节绝对跳转 + 5 个 NOP，共 17 字节＝两条完整指令）──
    ///   48 B8 &lt;stubAddr&gt;      mov rax, stubAddr
    ///   FF E0                  jmp rax
    ///
    /// ── 安全边界 ──────────────────────────────────────────────────────
    /// * 只改目标进程的私有内存，不写磁盘、不改 DLL 文件、不注入模块；
    /// * 安装前强制校验入口 17 字节 = 已知版本特征，不匹配直接拒绝；
    /// * 策略初始值 fail-closed（-10044）；上层不推送放行就一直拒绝；
    /// * 必须由调用方显式 Install，Uninstall 完整恢复原始字节并释放远程内存；
    /// * 需要管理员 + SeDebugPrivilege 才能打开 SYSTEM 服务进程。
    /// </summary>
    public sealed class RemoteLicenseHook : IDisposable
    {
        public const int CV_ERR_SUCCESS = 1;
        public const int CV_ERR_CAM_LICENSE = 0;   // LicenseValidate 是 BOOL：0=false 拒绝
        public const string DefaultModuleName = "licenseClientDLL.dll";
        public const string ExportName = "LicenseValidate";

        private const int PATCH_LENGTH = 17;   // 8 条 push(10) + sub rsp,188h(7)，两条完整指令边界
        private const int STATE_HITS_OFFSET = 0;
        private const int STATE_POLICY_OFFSET = 8;
        private const int STATE_SIZE = 4096;
        private const int STUB_SIZE = 64;

        // 部署版 LicenseValidate 入口：8 条 push (10B) + sub rsp,188h (7B)
        // licenseClientDLL.dll!LicenseValidate 入口：push rbx/rsi/rdi/r12/r13/r14/r15 + sub rsp,188h
        private static readonly byte[] KnownPrefix =
        {
            0x40, 0x53, 0x56, 0x57, 0x41, 0x54, 0x41, 0x56, 0x41, 0x57,
            0x48, 0x81, 0xEC, 0x88, 0x01, 0x00, 0x00
        };

        private readonly object _gate = new object();
        private IntPtr _process;
        private IntPtr _stubPage;
        private IntPtr _statePage;
        private IntPtr _target;
        private byte[]? _saved;
        private uint _oldProtection;
        private bool _installed;
        private bool _patchWritten;
        private bool _disposed;

        public int Pid { get; private set; }
        public IntPtr ValiLicAddress { get { return _target; } }
        public bool IsInstalled { get { lock (_gate) return _installed; } }
        public RemoteModuleDiagnostics Diagnostics { get; private set; } = new RemoteModuleDiagnostics();

        private RemoteLicenseHook() { }

        // ------------------------------------------------------------------
        // 权限
        // ------------------------------------------------------------------

        public static bool IsElevated
        {
            get
            {
                try
                {
                    using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch { return false; }
            }
        }

        /// <summary>尝试给当前进程启用 SeDebugPrivilege（打开 SYSTEM 服务进程必需）。</summary>
        public static bool TryEnableSeDebugPrivilege(out string message)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                {
                    message = "OpenProcessToken 失败：" + DescribeWin32Error(Marshal.GetLastWin32Error());
                    return false;
                }
                if (!LookupPrivilegeValueW(null, "SeDebugPrivilege", out LUID luid))
                {
                    message = "LookupPrivilegeValue 失败：" + DescribeWin32Error(Marshal.GetLastWin32Error());
                    return false;
                }

                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                };
                if (!AdjustTokenPrivileges(token, false, ref tp, (uint)Marshal.SizeOf<TOKEN_PRIVILEGES>(), IntPtr.Zero, IntPtr.Zero))
                {
                    message = "AdjustTokenPrivileges 失败：" + DescribeWin32Error(Marshal.GetLastWin32Error());
                    return false;
                }
                int error = Marshal.GetLastWin32Error();
                if (error == ERROR_NOT_ALL_ASSIGNED)
                {
                    message = "令牌里没有 SeDebugPrivilege（需要以管理员身份运行）。";
                    return false;
                }
                message = "SeDebugPrivilege 已启用。";
                return true;
            }
            finally
            {
                if (token != IntPtr.Zero) CloseHandle(token);
            }
        }

        /// <summary>只读探测：能否以写内存权限打开目标进程。不修改任何东西。</summary>
        public static string ProbeAccess(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_ACCESS, false, (uint)pid);
            if (h == IntPtr.Zero)
                return "拒绝访问（" + DescribeWin32Error(Marshal.GetLastWin32Error()) + "）";
            CloseHandle(h);
            return "可读写进程内存";
        }

        // ------------------------------------------------------------------
        // 进程定位
        // ------------------------------------------------------------------

        /// <summary>
        /// 只处理这两个服务进程。ColorVisionServiceHost 不加载 licenseClientDLL.dll，
        /// 因此不列为目标；诊断窗口与放行管理器共用此清单，避免目标漂移。
        /// </summary>
        public static readonly string[] TargetProcessNames =
        {
            "CVMainWindowsService_dev",
            "CVMainWindowsService_x64"
        };

        /// <summary>列出可作为目标的 CV 服务进程（只读，不需要特殊权限）。</summary>
        public static IReadOnlyList<RemoteProcessCandidate> FindCandidateProcesses()
        {
            List<RemoteProcessCandidate> result = new List<RemoteProcessCandidate>();
            foreach (string name in TargetProcessNames)
            {
                Process[] found;
                try { found = Process.GetProcessesByName(name); }
                catch { continue; }

                foreach (Process p in found)
                {
                    try
                    {
                        result.Add(new RemoteProcessCandidate
                        {
                            Pid = p.Id,
                            Name = p.ProcessName,
                            Path = SafePath(p),
                            StartTimeUtc = SafeStart(p)
                        });
                    }
                    catch { /* 进程刚退出 */ }
                    finally { p.Dispose(); }
                }
            }
            return result.OrderBy(x => x.Name).ThenBy(x => x.Pid).ToList();
        }

        private static string SafePath(Process p)
        {
            try { return p.MainModule?.FileName ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static DateTime? SafeStart(Process p)
        {
            try { return p.StartTime.ToUniversalTime(); }
            catch { return null; }
        }

        // ------------------------------------------------------------------
        // 只读诊断
        // ------------------------------------------------------------------

        /// <summary>
        /// 只读：远程读取目标进程已加载的 licenseClientDLL.dll 的 PE 导出表，
        /// 定位 LicenseValidate 并核对入口 17 字节。不做任何写入。
        /// </summary>
        public static RemoteModuleDiagnostics Inspect(int pid, string moduleName = DefaultModuleName)
        {
            RemoteModuleDiagnostics d = new RemoteModuleDiagnostics { Pid = pid, ModuleName = moduleName };
            try { d.ProcessName = Process.GetProcessById(pid).ProcessName; } catch { d.ProcessName = "?"; }

            IntPtr h = OpenProcess(PROCESS_ACCESS, false, (uint)pid);
            if (h == IntPtr.Zero)
            {
                d.AccessError = DescribeWin32Error(Marshal.GetLastWin32Error());
                d.Status = "无法打开进程：" + d.AccessError;
                return d;
            }

            try
            {
                if (!IsWow64Process(h, out bool wow64))
                    throw new InvalidOperationException("IsWow64Process 失败：" + DescribeWin32Error(Marshal.GetLastWin32Error()));
                d.Is64Bit = !wow64;
                if (wow64)
                {
                    d.Status = "目标进程是 32 位，x64 stub 不适用。";
                    return d;
                }

                IntPtr moduleBase = FindModule(h, moduleName, out string modulePath, out uint moduleSize);
                d.ModuleBase = moduleBase;
                d.ModulePath = modulePath;
                d.ModuleSize = moduleSize;
                if (moduleBase == IntPtr.Zero)
                {
                    d.Status = "目标进程尚未加载 " + moduleName + "（需先让服务打开过相机 / 初始化相机插件）。";
                    return d;
                }
                try { d.FileSizeBytes = new FileInfo(modulePath).Length; } catch { }

                ReadExportInfo(h, moduleBase, d);
                if (d.ValiLicRva == 0)
                {
                    d.Status = "该 DLL 未导出 LicenseValidate，可能已是放行版，无需 hook。";
                    return d;
                }

                byte[] entry = ReadRemote(h, d.ValiLicAddress, PATCH_LENGTH);
                d.EntryHex = BitConverter.ToString(entry).Replace("-", " ");
                d.EntryMatches = entry.SequenceEqual(KnownPrefix);
                d.Status = d.EntryMatches
                    ? "就绪：入口特征匹配，可以安装 hook。"
                    : "入口字节与已知版本不符，拒绝安装（版本漂移防呆）。";
                d.CanInstall = d.EntryMatches;
                return d;
            }
            catch (Exception ex)
            {
                d.Status = "诊断失败：" + ex.Message;
                return d;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        // ------------------------------------------------------------------
        // 安装 / 卸载
        // ------------------------------------------------------------------

        /// <summary>安装 hook。失败时不会留下任何补丁；策略初值为 fail-closed。</summary>
        public static RemoteLicenseHook Install(int pid, string moduleName = DefaultModuleName)
        {
            RemoteModuleDiagnostics d = Inspect(pid, moduleName);
            if (!d.CanInstall)
                throw new InvalidOperationException(d.Status);

            RemoteLicenseHook hook = new RemoteLicenseHook();
            hook.Pid = pid;
            hook.Diagnostics = d;
            hook._target = d.ValiLicAddress;

            IntPtr h = OpenProcess(PROCESS_ACCESS, false, (uint)pid);
            if (h == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess 失败");
            hook._process = h;

            try
            {
                hook._saved = ReadRemote(h, hook._target, PATCH_LENGTH);
                if (!hook._saved.SequenceEqual(KnownPrefix))
                    throw new InvalidOperationException("入口字节在安装瞬间发生变化，已放弃（版本漂移防呆）。");

                hook._statePage = VirtualAllocEx(h, IntPtr.Zero, Sz(STATE_SIZE),
                    MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (hook._statePage == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx(状态块) 失败");

                hook._stubPage = VirtualAllocEx(h, IntPtr.Zero, Sz(STUB_SIZE),
                    MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (hook._stubPage == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx(stub) 失败 —— 目标进程可能启用了 ACG / 动态代码策略");

                // fail-closed：上层不推送结论前一律返回许可证失败
                WriteRemote(h, hook._statePage + STATE_POLICY_OFFSET, BitConverter.GetBytes(CV_ERR_CAM_LICENSE));
                WriteRemote(h, hook._statePage + STATE_HITS_OFFSET, BitConverter.GetBytes(0L));

                WriteRemote(h, hook._stubPage, hook.BuildStub());
                if (!FlushInstructionCache(h, hook._stubPage, Sz(STUB_SIZE)))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "FlushInstructionCache(stub) 失败");

                byte[] patch = new byte[PATCH_LENGTH];
                patch[0] = 0x48;   // mov rax, imm64
                patch[1] = 0xB8;
                Array.Copy(BitConverter.GetBytes(hook._stubPage.ToInt64()), 0, patch, 2, 8);
                patch[10] = 0xFF;  // jmp rax
                patch[11] = 0xE0;
                for (int i = 12; i < PATCH_LENGTH; i++) patch[i] = 0x90;  // 对齐到完整指令边界

                if (!VirtualProtectEx(h, hook._target, Sz(PATCH_LENGTH), PAGE_EXECUTE_READWRITE, out hook._oldProtection))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualProtectEx(RWX) 失败");

                WriteRemote(h, hook._target, patch);
                hook._patchWritten = true;   // 从此刻起，卸载必须成功恢复原始字节
                if (!FlushInstructionCache(h, hook._target, Sz(PATCH_LENGTH)))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "FlushInstructionCache(target) 失败");

                // 立即恢复原页保护：只在写入的瞬间短暂 RWX
                uint ignore;
                VirtualProtectEx(h, hook._target, Sz(PATCH_LENGTH), hook._oldProtection, out ignore);

                hook._installed = true;
                return hook;
            }
            catch
            {
                hook.CleanupAfterFailedInstall();
                throw;
            }
        }

        private byte[] BuildStub()
        {
            long hitAddr = _statePage.ToInt64() + STATE_HITS_OFFSET;
            long policyAddr = _statePage.ToInt64() + STATE_POLICY_OFFSET;
            List<byte> code = new List<byte>(32);

            code.AddRange(new byte[] { 0x48, 0xB8 });                 // mov rax, hitAddr
            code.AddRange(BitConverter.GetBytes(hitAddr));
            code.AddRange(new byte[] { 0xF0, 0x48, 0xFF, 0x00 });    // lock inc qword ptr [rax]
            code.AddRange(new byte[] { 0x48, 0xB8 });                 // mov rax, policyAddr
            code.AddRange(BitConverter.GetBytes(policyAddr));
            code.AddRange(new byte[] { 0x8B, 0x00 });                 // mov eax, dword ptr [rax]
            code.Add(0xC3);                                            // ret
            return code.ToArray();
        }

        /// <summary>推送上层统一鉴权结论。1=放行；-10044=拒绝；其它整数也可用于验证。</summary>
        public void SetPolicy(int returnValue)
        {
            lock (_gate)
            {
                EnsureInstalled();
                WriteRemote(_process, _statePage + STATE_POLICY_OFFSET, BitConverter.GetBytes(returnValue));
            }
        }

        public int GetPolicy()
        {
            lock (_gate)
            {
                EnsureInstalled();
                return BitConverter.ToInt32(ReadRemote(_process, _statePage + STATE_POLICY_OFFSET, 4), 0);
            }
        }

        /// <summary>读取目标进程里 LicenseValidate 被命中的次数（stub 用 lock inc 累加）。</summary>
        public long GetHits()
        {
            lock (_gate)
            {
                EnsureInstalled();
                return BitConverter.ToInt64(ReadRemote(_process, _statePage + STATE_HITS_OFFSET, 8), 0);
            }
        }

        private void EnsureInstalled()
        {
            if (!_installed || _disposed)
                throw new InvalidOperationException("hook 未安装或已卸载");
        }

        /// <summary>
        /// 恢复原始 17 字节并释放远程内存，返回是否干净卸载。
        /// 恢复失败时保留远程 stub 与进程句柄：绝不能让目标进程里仍生效的跳转
        /// 指向已被 VirtualFreeEx 释放的地址，否则服务进程会崩溃。
        /// </summary>
        public bool Uninstall()
        {
            lock (_gate)
            {
                if (!_installed) return true;

                // 先把策略改回 fail-closed，避免恢复窗口期出现放行
                try { WriteRemote(_process, _statePage + STATE_POLICY_OFFSET, BitConverter.GetBytes(CV_ERR_CAM_LICENSE)); }
                catch { }

                uint previous;
                if (!VirtualProtectEx(_process, _target, Sz(PATCH_LENGTH), PAGE_EXECUTE_READWRITE, out previous))
                    return false;

                try
                {
                    WriteRemote(_process, _target, _saved!);
                    FlushInstructionCache(_process, _target, Sz(PATCH_LENGTH));
                }
                catch
                {
                    return false;
                }
                finally
                {
                    uint ignore;
                    VirtualProtectEx(_process, _target, Sz(PATCH_LENGTH), previous, out ignore);
                }

                _installed = false;
                _patchWritten = false;
                ReleaseRemote();
                return true;
            }
        }

        private void CleanupAfterFailedInstall()
        {
            if (_patchWritten)
            {
                try
                {
                    uint previous;
                    if (VirtualProtectEx(_process, _target, Sz(PATCH_LENGTH), PAGE_EXECUTE_READWRITE, out previous))
                    {
                        WriteRemote(_process, _target, _saved!);
                        FlushInstructionCache(_process, _target, Sz(PATCH_LENGTH));
                        uint ignore;
                        VirtualProtectEx(_process, _target, Sz(PATCH_LENGTH), previous, out ignore);
                        _patchWritten = false;
                    }
                }
                catch { }
                if (_patchWritten) return;   // 保守保活：不释放远程内存 / 不关闭句柄
            }
            ReleaseRemote();
        }

        private void ReleaseRemote()
        {
            if (_process == IntPtr.Zero) return;
            if (_stubPage != IntPtr.Zero) { VirtualFreeEx(_process, _stubPage, UIntPtr.Zero, MEM_RELEASE); _stubPage = IntPtr.Zero; }
            if (_statePage != IntPtr.Zero) { VirtualFreeEx(_process, _statePage, UIntPtr.Zero, MEM_RELEASE); _statePage = IntPtr.Zero; }
            CloseHandle(_process);
            _process = IntPtr.Zero;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                bool clean;
                try { clean = Uninstall(); }
                catch { clean = false; }
                if (!clean) return;   // 保留资源与重试机会，避免目标进程崩溃
                _disposed = true;
                ReleaseRemote();
            }
        }

        // ------------------------------------------------------------------
        // 远程 PE 解析
        // ------------------------------------------------------------------

        private static IntPtr FindModule(IntPtr h, string moduleName, out string path, out uint size)
        {
            path = string.Empty;
            size = 0;
            IntPtr[] modules = new IntPtr[1024];
            if (!EnumProcessModules(h, modules, (uint)(IntPtr.Size * modules.Length), out uint needed))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "EnumProcessModules 失败");

            int count = Math.Min(modules.Length, (int)(needed / IntPtr.Size));
            StringBuilder sb = new StringBuilder(1024);
            for (int i = 0; i < count; i++)
            {
                sb.Clear();
                uint len = GetModuleFileNameExW(h, modules[i], sb, (uint)sb.Capacity);
                string full = len > 0 ? sb.ToString() : string.Empty;
                if (!full.EndsWith(moduleName, StringComparison.OrdinalIgnoreCase)) continue;

                MODULEINFO info;
                if (GetModuleInformation(h, modules[i], out info, (uint)Marshal.SizeOf<MODULEINFO>()))
                    size = info.SizeOfImage;
                path = full;
                return modules[i];
            }
            return IntPtr.Zero;
        }

        private static void ReadExportInfo(IntPtr h, IntPtr moduleBase, RemoteModuleDiagnostics d)
        {
            uint lfa = BitConverter.ToUInt32(ReadRemote(h, moduleBase + 0x3C, 4), 0);
            byte[] sig = ReadRemote(h, moduleBase + (int)lfa, 4);
            if (!(sig[0] == 0x50 && sig[1] == 0x45 && sig[2] == 0 && sig[3] == 0))
                throw new InvalidOperationException("目标模块 PE 签名无效");

            ushort magic = BitConverter.ToUInt16(ReadRemote(h, moduleBase + (int)lfa + 24, 2), 0);
            if (magic != 0x20B) throw new InvalidOperationException("目标模块不是 PE32+ (x64)");

            d.BuildTimestampUtc = DateTimeOffset
                .FromUnixTimeSeconds(BitConverter.ToUInt32(ReadRemote(h, moduleBase + (int)lfa + 8, 4), 0))
                .UtcDateTime;

            int dataDir = (int)lfa + 24 + 112;   // PE32+ OptionalHeader + DataDirectory[0]
            uint exportRva = BitConverter.ToUInt32(ReadRemote(h, moduleBase + dataDir, 4), 0);
            if (exportRva == 0) return;

            byte[] dir = ReadRemote(h, moduleBase + (int)exportRva, 40);
            uint numberOfFunctions = BitConverter.ToUInt32(dir, 20);
            uint numberOfNames = BitConverter.ToUInt32(dir, 24);
            uint addressOfFunctions = BitConverter.ToUInt32(dir, 28);
            uint addressOfNames = BitConverter.ToUInt32(dir, 32);
            uint addressOfOrdinals = BitConverter.ToUInt32(dir, 36);
            d.NamedExportCount = (int)numberOfNames;
            d.OrdinalBase = BitConverter.ToUInt32(dir, 16);
            if (numberOfNames == 0 || numberOfNames > 100000 || numberOfFunctions > 100000) return;

            // 分批读名字 RVA 表，避免一次分配过大
            for (uint i = 0; i < numberOfNames; i++)
            {
                uint nameRva = BitConverter.ToUInt32(
                    ReadRemote(h, moduleBase + (int)(addressOfNames + i * 4), 4), 0);
                if (nameRva == 0) continue;
                byte[] raw;
                try { raw = ReadRemote(h, moduleBase + (int)nameRva, 32); }
                catch { continue; }   // 靠近节尾时可能跨到未映射页，跳过该名字
                int end = Array.IndexOf(raw, (byte)0);
                string name = Encoding.ASCII.GetString(raw, 0, end < 0 ? raw.Length : end);
                if (!string.Equals(name, ExportName, StringComparison.Ordinal)) continue;

                ushort ordinal = BitConverter.ToUInt16(
                    ReadRemote(h, moduleBase + (int)(addressOfOrdinals + i * 2), 2), 0);
                if (ordinal >= numberOfFunctions) break;
                uint funcRva = BitConverter.ToUInt32(
                    ReadRemote(h, moduleBase + (int)(addressOfFunctions + ordinal * 4), 4), 0);
                if (funcRva == 0) break;

                d.ValiLicOrdinal = ordinal + d.OrdinalBase;
                d.ValiLicRva = funcRva;
                d.ValiLicAddress = moduleBase + (int)funcRva;
                break;
            }
        }

        // ------------------------------------------------------------------
        // Win32
        // ------------------------------------------------------------------

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_ACCESS = PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION;

        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        private const int ERROR_NOT_ALL_ASSIGNED = 1300;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID Luid; public uint Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MODULEINFO
        {
            public IntPtr lpBaseOfDll;
            public uint SizeOfImage;
            public IntPtr EntryPoint;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, UIntPtr size, out UIntPtr bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, UIntPtr size, out UIntPtr bytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint allocationType, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint freeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtectEx(IntPtr process, IntPtr address, UIntPtr size, uint protect, out uint oldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process(IntPtr process, out bool wow64Process);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcessModules(IntPtr process, IntPtr[] modules, uint cb, out uint needed);

        [DllImport("psapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetModuleFileNameExW(IntPtr process, IntPtr module, StringBuilder filename, uint size);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetModuleInformation(IntPtr process, IntPtr module, out MODULEINFO info, uint cb);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool LookupPrivilegeValueW(string? systemName, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState,
            uint bufferLength, IntPtr previousState, IntPtr returnLength);

        private static UIntPtr Sz(int byteCount) { return new UIntPtr((uint)byteCount); }

        private static byte[] ReadRemote(IntPtr process, IntPtr address, int length)
        {
            byte[] buffer = new byte[length];
            UIntPtr read;
            if (!ReadProcessMemory(process, address, buffer, Sz(length), out read) || read.ToUInt64() != (ulong)length)
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "ReadProcessMemory(0x" + address.ToInt64().ToString("X") + ", " + length + ") 失败");
            return buffer;
        }

        private static void WriteRemote(IntPtr process, IntPtr address, byte[] data)
        {
            UIntPtr written;
            if (!WriteProcessMemory(process, address, data, Sz(data.Length), out written) || written.ToUInt64() != (ulong)data.Length)
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "WriteProcessMemory(0x" + address.ToInt64().ToString("X") + ") 失败");
        }

        private static string DescribeWin32Error(int code)
        {
            string text = new Win32Exception(code).Message;
            return "Win32 " + code + (string.IsNullOrWhiteSpace(text) ? string.Empty : " " + text);
        }
    }

    public sealed class RemoteProcessCandidate
    {
        public int Pid { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime? StartTimeUtc { get; set; }
        public string Display { get { return Name + "  (PID " + Pid + ")"; } }
    }

    public sealed class RemoteModuleDiagnostics
    {
        public int Pid { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ModuleName { get; set; } = RemoteLicenseHook.DefaultModuleName;
        public string ModulePath { get; set; } = string.Empty;
        public IntPtr ModuleBase { get; set; }
        public uint ModuleSize { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime BuildTimestampUtc { get; set; }
        public int NamedExportCount { get; set; }
        public uint OrdinalBase { get; set; }
        public uint ValiLicOrdinal { get; set; }
        public uint ValiLicRva { get; set; }
        public IntPtr ValiLicAddress { get; set; }
        public string EntryHex { get; set; } = string.Empty;
        public bool EntryMatches { get; set; }
        public bool CanInstall { get; set; }
        public bool Is64Bit { get; set; }
        public string AccessError { get; set; } = string.Empty;
        public string Status { get; set; } = "尚未诊断";
    }
}
