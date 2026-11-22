using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ColorVision.Experimental.CVLicenseHook;

/// <summary>
/// x64 入口 inline hook。
///
/// 针对部署版 cvCamera.dll 的 CM_ValiLic（x64, 2026-10-29 构建）：
///   原始入口 12 字节，恰好是两条完整指令，不存在跨指令切割：
///     48 89 4C 24 08           mov  [rsp+8], rcx        (5)
///     48 81 EC 08 01 00 00     sub  rsp, 108h           (7)
///
/// 替换为 12 字节绝对跳转（不依赖 ±2GB 相对范围，ASLR 下仍然有效）：
///     48 B8 <imm64>            mov  rax, Detour         (10)
///     FF E0                    jmp  rax                 (2)
///
/// trampoline（26 字节，可执行内存）：
///     <原始 12 字节>
///     FF 25 00 00 00 00        jmp  qword ptr [rip+0]
///     <8 字节：原函数入口 +12 的绝对地址>
///
/// 因此这一处补丁同时覆盖：CM_Open 内部 5 条直接 E8 调用、
/// CM_OpenEx 的间接路径、以及 C# 侧 GetProcAddress 缓存的函数指针调用。
/// IAT hook / 导出表改写对「同模块内部直接 call」无效，必须改函数体。
/// </summary>
internal sealed class InlineHook : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int ValidationProc(IntPtr handle);

    private const int PatchLength = 12;
    private const int TrampolineLength = 26;

    /// <summary>已知版本的入口特征；版本漂移时拒绝安装，避免打坏别的函数。</summary>
    internal static readonly byte[] KnownPrefix =
    {
        0x48, 0x89, 0x4C, 0x24, 0x08,
        0x48, 0x81, 0xEC, 0x08, 0x01, 0x00, 0x00
    };

    private readonly byte[] _saved = new byte[PatchLength];
    private IntPtr _target;
    private IntPtr _trampoline;
    private IntPtr _detourPtr;
    private uint _oldProtect;

    // 委托必须长期存活：GetFunctionPointerForDelegate 返回的原生指针不持有 GC 根。
    // 下面这个字段的唯一作用就是当 GC 根，静态分析会误报「未使用」。
#pragma warning disable CS0414
    private ValidationProc? _detour;
#pragma warning restore CS0414
    private ValidationProc? _originalCall;

    private bool _installed;
    private bool _disposed;

    internal IntPtr Target => _target;
    internal bool IsInstalled => _installed;

    /// <summary>通过 trampoline 调用未经 hook 的原始实现，供 Observe / 降级路径使用。</summary>
    internal ValidationProc? OriginalCall => _originalCall;

    internal string PrefixHex => Convert.ToHexString(_saved);

    internal static InlineHook Install(IntPtr target, ValidationProc detour, bool verifyPrefix = true)
    {
        if (!Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("该补丁是 x64 专用；x86 需要另行设计 5 字节相对跳转。");
        if (target == IntPtr.Zero)
            throw new ArgumentException("目标函数地址为空（GetProcAddress 失败？）", nameof(target));
        ArgumentNullException.ThrowIfNull(detour);

        var hook = new InlineHook
        {
            _target = target,
            _detour = detour,
            _detourPtr = Marshal.GetFunctionPointerForDelegate(detour)
        };

        Marshal.Copy(target, hook._saved, 0, PatchLength);

        if (verifyPrefix && !hook._saved.AsSpan(0, PatchLength).SequenceEqual(KnownPrefix))
        {
            throw new InvalidOperationException(
                "CM_ValiLic 入口字节与已知版本不符，拒绝安装。" +
                " 期望 " + Convert.ToHexString(KnownPrefix) +
                "，实际 " + hook.PrefixHex +
                "。请用新版本的入口字节更新 KnownPrefix（并保证前 12 字节是完整指令边界）。");
        }

        hook.BuildTrampoline();
        try
        {
            hook.WritePatch();
            hook._installed = true;
        }
        catch
        {
            if (hook._trampoline != IntPtr.Zero)
                Win32.VirtualFree(hook._trampoline, 0, Win32.MEM_RELEASE);
            hook._trampoline = IntPtr.Zero;
            throw;
        }
        return hook;
    }

    private void BuildTrampoline()
    {
        _trampoline = Win32.ExecutableMemory(64);

        byte[] code = new byte[TrampolineLength];
        _saved.CopyTo(code, 0);

        code[12] = 0xFF;
        code[13] = 0x25;                       // jmp qword ptr [rip+0]
        // code[14..17] 保持 0
        BitConverter.GetBytes(_target.ToInt64() + PatchLength).CopyTo(code, 18);

        Marshal.Copy(code, 0, _trampoline, code.Length);
        Win32.Flush(_trampoline, 64);

        _originalCall = Marshal.GetDelegateForFunctionPointer<ValidationProc>(_trampoline);
    }

    private void WritePatch()
    {
        if (!Win32.VirtualProtect(_target, 16, Win32.PAGE_EXECUTE_READWRITE, out _oldProtect))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualProtect(RWX) 失败");

        byte[] patch = new byte[PatchLength];
        patch[0] = 0x48;                       // mov rax, imm64
        patch[1] = 0xB8;
        BitConverter.GetBytes(_detourPtr.ToInt64()).CopyTo(patch, 2);
        patch[10] = 0xFF;                     // jmp rax
        patch[11] = 0xE0;

        Marshal.Copy(patch, 0, _target, PatchLength);
        Win32.Flush(_target, 16);

        if (!Win32.VirtualProtect(_target, 16, _oldProtect, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "恢复内存保护失败");
    }

    internal void Remove()
    {
        if (!_installed) return;

        if (!Win32.VirtualProtect(_target, 16, Win32.PAGE_EXECUTE_READWRITE, out uint previous))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualProtect(RWX) 失败");

        Marshal.Copy(_saved, 0, _target, PatchLength);
        Win32.Flush(_target, 16);
        Win32.VirtualProtect(_target, 16, previous, out _);
        _installed = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Remove(); } catch { /* 卸载失败不遮蔽主流程 */ }

        if (_trampoline != IntPtr.Zero)
        {
            Win32.VirtualFree(_trampoline, 0, Win32.MEM_RELEASE);
            _trampoline = IntPtr.Zero;
        }
        _originalCall = null;
        _detour = null;
    }
}
