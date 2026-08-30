using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.UI.HotKey;

public sealed record HotkeyOperationError(string Id, string Message);

public sealed class HotkeyApplyResult
{
    internal HotkeyApplyResult(IEnumerable<HotkeyOperationError>? errors = null, IEnumerable<HotkeyOperationError>? restoreErrors = null)
    {
        Errors = Array.AsReadOnly(errors?.ToArray() ?? []);
        RestoreErrors = Array.AsReadOnly(restoreErrors?.ToArray() ?? []);
    }

    public bool Success => Errors.Count == 0 && RestoreErrors.Count == 0;
    public IReadOnlyList<HotkeyOperationError> Errors { get; }
    public IReadOnlyList<HotkeyOperationError> RestoreErrors { get; }
    public string Message => string.Join(Environment.NewLine, Errors.Concat(RestoreErrors).Select(error =>
        string.IsNullOrEmpty(error.Id) ? error.Message : $"{error.Id}: {error.Message}"));
}

internal sealed record HotkeyRegistrationAttempt(IHotkeyRegistration? Registration, string? Error = null);
internal sealed record HotkeyPersistenceAttempt(ConfigSavePublicationStatus Status, string? Error = null);

public sealed class HotkeyCaptureLease : IDisposable
{
    private Func<HotkeyApplyResult>? _release;
    internal HotkeyCaptureLease(Func<HotkeyApplyResult> release) => _release = release;
    public HotkeyApplyResult? RestoreResult { get; private set; }
    public void Dispose()
    {
        Func<HotkeyApplyResult>? release = Interlocked.Exchange(ref _release, null);
        if (release != null)
            RestoreResult = release();
    }
}

/// <summary>Shared by both backends, including callbacks registered outside HotkeyService.</summary>
internal static class HotkeyDispatchGate
{
    private static int _captureCount;
    private static readonly HashSet<Key> HeldKeys = new();
    private static DispatcherTimer? _releaseTimer;
    internal static Func<Key, bool> KeyStateReader { get; set; } = Keyboard.IsKeyDown;
    internal static bool HasPendingKeyRelease => HeldKeys.Count > 0;
    internal static bool IsSuspended => Volatile.Read(ref _captureCount) > 0;
    internal static void Enter() => Interlocked.Increment(ref _captureCount);
    internal static void Exit()
    {
        if (Interlocked.Decrement(ref _captureCount) != 0) return;
        // A modal editor may close on key down. Do not dispatch the same gesture's
        // trailing key-up (or a global auto-repeat) after the capture lease ends.
        foreach (Key key in Enum.GetValues<Key>().Distinct())
            if (key is not (Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed) && KeyStateReader(key))
                HeldKeys.Add(key);
        if (HeldKeys.Count == 0) return;
        _releaseTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.ContextIdle,
            (_, _) => RemoveReleasedKeys(), Dispatcher.CurrentDispatcher);
        _releaseTimer.Start();
    }

    internal static bool ShouldSuppress(Key key, bool isKeyUp = false)
    {
        if (IsSuspended) return true;
        if (HeldKeys.Count == 0) return false;
        if (isKeyUp) HeldKeys.Remove(key);
        return true;
    }

    private static void RemoveReleasedKeys()
    {
        HeldKeys.RemoveWhere(key => !KeyStateReader(key));
        if (HeldKeys.Count == 0) _releaseTimer?.Stop();
    }
}
