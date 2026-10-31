using System.Windows.Controls;

namespace ColorVision.UI.HotKey
{
    public sealed partial class HotkeyService
    {
        public HotkeyCaptureLease BeginCapture()
        {
            lock (_operationLock)
            {
                if (_isApplying) throw new InvalidOperationException("正在应用快捷键，不能开始录入。");
                if (_captureDepth > 0)
                {
                    _captureDepth++;
                    return new HotkeyCaptureLease(EndCapture);
                }
                HotkeyDispatchGate.Enter();
                _capturedGlobals = HotKeys.Where(item => item.IsGlobal && item.Registration?.IsRegistered == true).Select(Snapshot).ToList();
                try
                {
                    foreach (RuntimeSnapshot snapshot in _capturedGlobals) Unregister(snapshot.Entry);
                    _captureDepth = 1;
                    LastCaptureRestoreResult = null;
                    return new HotkeyCaptureLease(EndCapture);
                }
                catch (Exception exception)
                {
                    LastCaptureRestoreResult = new(restoreErrors: RestoreRuntime(_capturedGlobals));
                    _capturedGlobals.Clear();
                    HotkeyDispatchGate.Exit();
                    if (!LastCaptureRestoreResult.Success)
                        throw new InvalidOperationException(exception.Message + Environment.NewLine + LastCaptureRestoreResult.Message, exception);
                    throw;
                }
            }
        }

        private HotkeyApplyResult EndCapture()
        {
            lock (_operationLock)
            {
                if (--_captureDepth > 0) return new();
                try
                {
                    LastCaptureRestoreResult = new(restoreErrors: RestoreRuntime(_capturedGlobals));
                    return LastCaptureRestoreResult;
                }
                finally
                {
                    _capturedGlobals.Clear();
                    HotkeyDispatchGate.Exit();
                }
            }
        }

        private sealed record RuntimeSnapshot(HotKeys Entry, IReadOnlyList<Hotkey> Bindings, HotKeyKinds Kinds, Control? Owner, bool WasRegistered);
    }
}
