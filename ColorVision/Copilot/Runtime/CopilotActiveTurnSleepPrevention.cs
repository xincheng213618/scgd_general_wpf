using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotSystemSleepPreventionRuntimeSnapshot(
        int ActiveLeaseCount,
        int? LastErrorCode,
        string LastFailure)
    {
        public bool IsActive => ActiveLeaseCount > 0;
    }

    internal interface ICopilotSystemSleepRequestFactory
    {
        IDisposable Acquire();
    }

    internal static class CopilotActiveTurnSleepPrevention
    {
        public static IDisposable Acquire(
            CopilotProjectInstructionDiscoveryOptions? options,
            ICopilotSystemSleepRequestFactory? requestFactory = null)
        {
            if (options?.HasPreventIdleSleepOverride != true
                || !options.ConfiguredPreventIdleSleep)
            {
                return CopilotNoopSleepRequest.Instance;
            }

            return (requestFactory ?? CopilotWindowsSystemSleepRequestFactory.Instance).Acquire();
        }

        public static CopilotSystemSleepPreventionRuntimeSnapshot CaptureRuntimeSnapshot() =>
            CopilotWindowsSystemSleepRequestFactory.Instance.CaptureSnapshot();
    }

    internal sealed class CopilotWindowsSystemSleepRequestFactory : ICopilotSystemSleepRequestFactory
    {
        private readonly object _gate = new();
        private int _activeLeaseCount;
        private int? _lastErrorCode;
        private string _lastFailure = string.Empty;

        public static CopilotWindowsSystemSleepRequestFactory Instance { get; } = new();

        private CopilotWindowsSystemSleepRequestFactory()
        {
        }

        public IDisposable Acquire()
        {
            if (!OperatingSystem.IsWindows())
                return RecordFailure(null, "当前平台不支持 Windows Power Request。");

            try
            {
                if (!CopilotWindowsPowerRequest.TryCreate(out var handle, out var errorCode))
                    return RecordFailure(errorCode, "Windows Power Request 创建失败。");

                lock (_gate)
                {
                    _activeLeaseCount++;
                    _lastErrorCode = null;
                    _lastFailure = string.Empty;
                }
                return new CopilotWindowsSystemSleepRequestLease(handle, OnReleased);
            }
            catch (Exception exception)
            {
                return RecordFailure(null, $"Windows Power Request 不可用：{exception.Message}");
            }
        }

        public CopilotSystemSleepPreventionRuntimeSnapshot CaptureSnapshot()
        {
            lock (_gate)
            {
                return new CopilotSystemSleepPreventionRuntimeSnapshot(
                    _activeLeaseCount,
                    _lastErrorCode,
                    _lastFailure);
            }
        }

        private CopilotNoopSleepRequest RecordFailure(int? errorCode, string failure)
        {
            var normalizedFailure = (failure ?? string.Empty).Trim();
            lock (_gate)
            {
                _lastErrorCode = errorCode;
                _lastFailure = normalizedFailure;
            }
            Trace.TraceWarning(errorCode.HasValue
                ? $"Copilot sleep prevention failed ({errorCode.Value}): {normalizedFailure}"
                : $"Copilot sleep prevention failed: {normalizedFailure}");
            return CopilotNoopSleepRequest.Instance;
        }

        private void OnReleased(int? errorCode, string? failure)
        {
            var normalizedFailure = (failure ?? string.Empty).Trim();
            lock (_gate)
            {
                if (_activeLeaseCount > 0)
                    _activeLeaseCount--;
                if (errorCode.HasValue || normalizedFailure.Length > 0)
                {
                    _lastErrorCode = errorCode;
                    _lastFailure = normalizedFailure.Length > 0
                        ? normalizedFailure
                        : "Windows Power Request 释放失败。";
                }
            }
            if (errorCode.HasValue || normalizedFailure.Length > 0)
            {
                Trace.TraceWarning(errorCode.HasValue
                    ? $"Copilot sleep prevention release failed ({errorCode.Value}): {normalizedFailure}"
                    : $"Copilot sleep prevention release failed: {normalizedFailure}");
            }
        }
    }

    internal sealed class CopilotWindowsSystemSleepRequestLease : IDisposable
    {
        private CopilotWindowsPowerRequest.SafePowerRequestHandle? _handle;
        private Action<int?, string?>? _onReleased;

        public CopilotWindowsSystemSleepRequestLease(
            CopilotWindowsPowerRequest.SafePowerRequestHandle handle,
            Action<int?, string?> onReleased)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            _onReleased = onReleased ?? throw new ArgumentNullException(nameof(onReleased));
        }

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, null);
            var onReleased = Interlocked.Exchange(ref _onReleased, null);
            if (handle == null || onReleased == null)
                return;

            int? errorCode = null;
            var failure = string.Empty;
            try
            {
                if (!CopilotWindowsPowerRequest.Clear(handle))
                    errorCode = Marshal.GetLastWin32Error();
            }
            catch (Exception exception)
            {
                failure = $"Windows Power Request 释放失败：{exception.Message}";
            }
            finally
            {
                try
                {
                    handle.Dispose();
                }
                catch (Exception exception)
                {
                    failure = $"Windows Power Request 句柄释放失败：{exception.Message}";
                }
                onReleased(errorCode, failure);
            }
        }
    }

    internal static class CopilotWindowsPowerRequest
    {
        private const uint ReasonContextVersion = 0;
        private const uint ReasonContextSimpleString = 1;

        private enum PowerRequestType
        {
            SystemRequired = 0,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PowerRequestContext
        {
            public uint Version;
            public uint Flags;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string SimpleReasonString;
        }

        public static bool TryCreate(
            out SafePowerRequestHandle handle,
            out int errorCode)
        {
            var context = new PowerRequestContext
            {
                Version = ReasonContextVersion,
                Flags = ReasonContextSimpleString,
                SimpleReasonString = "ColorVision Copilot 正在执行活动轮次",
            };
            handle = PowerCreateRequest(ref context);
            if (handle.IsInvalid)
            {
                errorCode = Marshal.GetLastWin32Error();
                handle.Dispose();
                return false;
            }
            if (!PowerSetRequest(handle, PowerRequestType.SystemRequired))
            {
                errorCode = Marshal.GetLastWin32Error();
                handle.Dispose();
                return false;
            }

            errorCode = 0;
            return true;
        }

        public static bool Clear(SafePowerRequestHandle handle) =>
            PowerClearRequest(handle, PowerRequestType.SystemRequired);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafePowerRequestHandle PowerCreateRequest(
            ref PowerRequestContext context);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PowerSetRequest(
            SafePowerRequestHandle powerRequest,
            PowerRequestType requestType);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PowerClearRequest(
            SafePowerRequestHandle powerRequest,
            PowerRequestType requestType);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        internal sealed class SafePowerRequestHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafePowerRequestHandle()
                : base(ownsHandle: true)
            {
            }

            protected override bool ReleaseHandle() => CloseHandle(handle);
        }
    }

    internal sealed class CopilotNoopSleepRequest : IDisposable
    {
        public static CopilotNoopSleepRequest Instance { get; } = new();

        private CopilotNoopSleepRequest()
        {
        }

        public void Dispose()
        {
        }
    }
}
