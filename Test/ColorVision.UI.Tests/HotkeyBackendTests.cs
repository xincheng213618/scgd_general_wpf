using ColorVision.UI.HotKey;
using ColorVision.UI.HotKey.GlobalHotKey;
using ColorVision.UI.HotKey.WindowHotKey;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using NativeHotkeys = ColorVision.UI.HotKey.GlobalHotKey.GlobalHotKey;
using RoutedHotkeys = ColorVision.UI.HotKey.WindowHotKey.WindowHotKey;

namespace ColorVision.UI.Tests;

/// <summary>
/// Real backends, hidden test-owned HWNDs, and harmless counters only. No Show,
/// SendInput, PostMessage keyboard input, application startup, or persisted config.
/// </summary>
[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class HotkeyBackendTests
{
    private const ModifierKeys NativeModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;

    [Fact]
    public void NativeRegistrationRejectsAnotherHwndAndCanBeReacquiredAfterDispose()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            using HiddenWindow contender = new();
            int invoked = 0;
            using IHotkeyRegistration original = RequireNativeRegistration(owner, Key.F23, () => invoked++);
            int oldId = RegistrationId(original);
            owner.SendHotkey(original);
            Assert.Equal(1, invoked);

            HotkeyRegistrationAttempt conflict = NativeHotkeys.TryRegister(contender.Handle, NativeModifiers, Key.F23, () => { });
            using (conflict.Registration)
            {
                Assert.Null(conflict.Registration);
                Assert.Contains("1409", conflict.Error);
                Assert.False(HasNativeScope(contender.Handle));
            }

            original.Dispose();
            original.Dispose();
            Assert.False(original.IsRegistered);
            Assert.False(HasNativeScope(owner.Handle));
            owner.SendHotkey(oldId, Key.F23);
            Assert.Equal(1, invoked);

            using IHotkeyRegistration reacquired = RequireNativeRegistration(contender, Key.F23, () => invoked++);
            contender.SendHotkey(reacquired);
            Assert.Equal(2, invoked);
        });
    }

    [Fact]
    public void NativeManagerReusesUnchangedHandleButReplacesChangedCallbackAndClosesCleanly()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            using HiddenWindow contender = new();
            int oldInvoked = 0;
            int newInvoked = 0;
            HotKeys entry = CreateEntry("native-callback", Key.F24, NativeModifiers, () => oldInvoked++, HotKeyKinds.Global);
            GlobalHotKeyManager manager = GlobalHotKeyManager.GetInstance(owner.Window);
            IHotkeyRegistration original = RequireRegistration(manager.TryRegisterHandle(entry), Key.F24);
            Assert.Same(original, manager.RegisterHandle(entry));

            entry.HotKeyHandler = () => newInvoked++;
            IHotkeyRegistration replacement = RequireRegistration(manager.TryRegisterHandle(entry), Key.F24);
            Assert.NotSame(original, replacement);
            Assert.False(original.IsRegistered);
            Assert.Same(replacement, manager.RegisterHandle(entry));
            owner.SendHotkey(original);
            Assert.Equal(0, oldInvoked);
            Assert.Equal(0, newInvoked);
            owner.SendHotkey(replacement);
            Assert.Equal(0, oldInvoked);
            Assert.Equal(1, newInvoked);

            IntPtr oldHwnd = owner.Handle;
            owner.Close();
            Assert.False(replacement.IsRegistered);
            Assert.False(entry.IsRegistered);
            Assert.Null(entry.Registration);
            Assert.False(GlobalHotKeyManager.Instances.ContainsKey(oldHwnd));
            Assert.False(HasNativeScope(oldHwnd));
            using IHotkeyRegistration reacquired = RequireNativeRegistration(contender, Key.F24, () => { });
        });
    }

    [Fact]
    public void WindowManagerSnapshotsMutableKeysAndUsesTheNewCallback()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            int oldInvoked = 0;
            int newInvoked = 0;
            WindowHotKeyManager manager = WindowHotKeyManager.GetInstance(owner.Window);
            // Cover every modifier state without changing the user's actual keyboard.
            List<HotKeys> entries = AllModifiers().Select(modifiers =>
                CreateEntry($"window-{modifiers}", Key.F23, modifiers, () => oldInvoked++)).ToList();
            foreach (HotKeys entry in entries)
            {
                IHotkeyRegistration original = RequireRegistration(manager.TryRegisterHandle(entry), Key.F23);
                Assert.Same(original, manager.RegisterHandle(entry));
                entry.HotKeyHandler = () => newInvoked++;
                IHotkeyRegistration replacement = RequireRegistration(manager.TryRegisterHandle(entry), Key.F23);
                Assert.NotSame(original, replacement);
                Assert.False(original.IsRegistered);
            }
            Assert.True(owner.RaiseKeyUp(owner.Window, Key.F23).Handled);
            Assert.Equal(0, oldInvoked);
            Assert.Equal(1, newInvoked);

            foreach (HotKeys entry in entries)
            {
                IHotkeyRegistration original = entry.Registration!;
                entry.Hotkey.Key = Key.F24;
                Assert.Equal(Key.F23, original.Hotkey.Key);
                IHotkeyRegistration replacement = RequireRegistration(manager.TryRegisterHandle(entry), Key.F24);
                Assert.NotSame(original, replacement);
                Assert.False(original.IsRegistered);
            }
            Assert.False(owner.RaiseKeyUp(owner.Window, Key.F23).Handled);
            Assert.True(owner.RaiseKeyUp(owner.Window, Key.F24).Handled);
            Assert.Equal(2, newInvoked);

            owner.Close();
            Assert.All(entries, entry => { Assert.False(entry.IsRegistered); Assert.Null(entry.Registration); });
            Assert.False(WindowHotKeyManager.Instances.ContainsKey(owner.Window));
        });
    }

    [Fact]
    public void WindowPreviewRouteHandlesOnlyTheFirstMatchingScopeAndDisposalDetachesIt()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            Button child = new();
            UserControl parent = new() { Content = child };
            owner.Window.Content = parent;
            int parentInvoked = 0;
            int childInvoked = 0;
            using RegistrationSet parentRegistrations = RegisterWindowCombinations(parent, Key.F23, () => parentInvoked++);
            using RegistrationSet childRegistrations = RegisterWindowCombinations(child, Key.F23, () => childInvoked++);
            using IHotkeyRegistration? duplicate = RoutedHotkeys.Register(parent, new(Key.F23, ModifierKeys.None), () => { });
            Assert.Null(duplicate);

            Assert.True(owner.RaiseKeyUp(child, Key.F23).Handled);
            Assert.Equal(1, parentInvoked);
            Assert.Equal(0, childInvoked);
            parentRegistrations.Dispose();
            Assert.True(owner.RaiseKeyUp(child, Key.F23).Handled);
            Assert.Equal(1, parentInvoked);
            Assert.Equal(1, childInvoked);
            childRegistrations.Dispose();
            Assert.False(owner.RaiseKeyUp(child, Key.F23).Handled);
            Assert.Equal(1, childInvoked);
        });
    }

    [Fact]
    public void CaptureReleasesRealGlobalSlotAndSuppressesBothBackendDispatchersUntilRestore()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            using HiddenWindow contender = new();
            HotkeyService service = CreateIsolatedService();
            int managedInvoked = 0;
            int directInvoked = 0;
            int windowInvoked = 0;
            HotKeys entry = CreateEntry("capture", Key.F23, NativeModifiers, () => managedInvoked++, HotKeyKinds.Global);
            try
            {
                Assert.True(service.AddHotKeys(owner.Window, entry), NativeUnavailableMessage(Key.F23));
                IHotkeyRegistration original = entry.Registration!;
                using IHotkeyRegistration direct = RequireNativeRegistration(owner, Key.F24, () => directInvoked++);
                using RegistrationSet local = RegisterWindowCombinations(owner.Window, Key.F24, () => windowInvoked++);
                using HotkeyCaptureLease lease = service.BeginCapture();
                Assert.True(HotkeyDispatchGate.IsSuspended);
                Assert.False(original.IsRegistered);
                Assert.False(entry.IsRegistered);
                owner.SendHotkey(original);
                owner.SendHotkey(direct);
                Assert.False(owner.RaiseKeyUp(owner.Window, Key.F24).Handled);
                Assert.Equal(0, managedInvoked);
                Assert.Equal(0, directInvoked);
                Assert.Equal(0, windowInvoked);
                using (IHotkeyRegistration temporary = RequireNativeRegistration(contender, Key.F23, () => { }))
                    Assert.True(temporary.IsRegistered);

                lease.Dispose();
                Assert.NotNull(lease.RestoreResult);
                Assert.True(lease.RestoreResult.Success, lease.RestoreResult.Message);
                Assert.False(HotkeyDispatchGate.IsSuspended);
                Assert.True(entry.IsRegistered);
                Assert.NotSame(original, entry.Registration);
                HotkeyRegistrationAttempt conflict = NativeHotkeys.TryRegister(contender.Handle, NativeModifiers, Key.F23, () => { });
                using (conflict.Registration) Assert.Null(conflict.Registration);
                owner.SendHotkey(entry.Registration!);
                owner.SendHotkey(direct);
                Assert.True(owner.RaiseKeyUp(owner.Window, Key.F24).Handled);
                Assert.Equal(1, managedInvoked);
                Assert.Equal(1, directInvoked);
                Assert.Equal(1, windowInvoked);
            }
            finally { service.UnregisterAll(); }
        });
    }

    [Fact]
    public void CaptureRestoreReportsRealNativeConflictWithoutDisplacingTheOtherOwner()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            using HiddenWindow contender = new();
            HotkeyService service = CreateIsolatedService();
            int contenderInvoked = 0;
            HotKeys entry = CreateEntry("capture-restore", Key.F23, NativeModifiers, () => { }, HotKeyKinds.Global);
            try
            {
                Assert.True(service.AddHotKeys(owner.Window, entry), NativeUnavailableMessage(Key.F23));
                using HotkeyCaptureLease lease = service.BeginCapture();
                using IHotkeyRegistration competitor = RequireNativeRegistration(contender, Key.F23, () => contenderInvoked++);
                lease.Dispose();
                Assert.NotNull(lease.RestoreResult);
                Assert.False(lease.RestoreResult.Success);
                Assert.Contains(lease.RestoreResult.RestoreErrors, error => error.Message.Contains("1409", StringComparison.Ordinal));
                Assert.False(entry.IsRegistered);
                Assert.False(HotkeyDispatchGate.IsSuspended);
                Assert.True(competitor.IsRegistered);
                contender.SendHotkey(competitor);
                Assert.Equal(1, contenderInvoked);

                competitor.Dispose();
                service.UpdateRegistration(entry);
                Assert.True(entry.IsRegistered);
            }
            finally { service.UnregisterAll(); }
        });
    }

    [Fact]
    public void HeldKeyAfterCaptureSuppressesNativeMessageAndConsumesTheFirstWindowKeyUp()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            int nativeInvoked = 0;
            int windowInvoked = 0;
            using IHotkeyRegistration global = RequireNativeRegistration(owner, Key.F23, () => nativeInvoked++);
            using RegistrationSet local = RegisterWindowCombinations(owner.Window, Key.F23, () => windowInvoked++);
            HotkeyService service = CreateIsolatedService();
            using HotkeyCaptureLease lease = service.BeginCapture();
            keyboard.SetDown(Key.F23, true);
            lease.Dispose();
            Assert.True(lease.RestoreResult!.Success, lease.RestoreResult.Message);
            Assert.True(HotkeyDispatchGate.HasPendingKeyRelease);

            owner.SendHotkey(global);
            Assert.Equal(0, nativeInvoked);
            keyboard.SetDown(Key.F23, false);
            Assert.False(owner.RaiseKeyUp(owner.Window, Key.F23).Handled);
            Assert.Equal(0, windowInvoked);
            Assert.False(HotkeyDispatchGate.HasPendingKeyRelease);
            Assert.True(owner.RaiseKeyUp(owner.Window, Key.F23).Handled);
            owner.SendHotkey(global);
            Assert.Equal(1, windowInvoked);
            Assert.Equal(1, nativeInvoked);
        });
    }

    [Fact]
    public void ReleaseTimerUnblocksNativeDispatchAfterObservedReleaseWithoutAKeyUpEvent()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            int invoked = 0;
            using IHotkeyRegistration global = RequireNativeRegistration(owner, Key.F24, () => invoked++);
            HotkeyService service = CreateIsolatedService();
            using HotkeyCaptureLease lease = service.BeginCapture();
            keyboard.SetDown(Key.F24, true);
            lease.Dispose();
            Assert.True(HotkeyDispatchGate.HasPendingKeyRelease);
            owner.SendHotkey(global);
            Assert.Equal(0, invoked);

            keyboard.SetDown(Key.F24, false);
            PumpUntil(() => !HotkeyDispatchGate.HasPendingKeyRelease);
            DispatcherTimer timer = Assert.IsType<DispatcherTimer>(typeof(HotkeyDispatchGate)
                .GetField("_releaseTimer", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
            Assert.False(timer.IsEnabled);
            owner.SendHotkey(global);
            Assert.Equal(1, invoked);
        });
    }

    private static HotkeyService CreateIsolatedService()
    {
        HotKeyConfig config = new();
        return new HotkeyService((owner, entry) => entry.IsGlobal
            ? GlobalHotKeyManager.GetInstance(Assert.IsType<Window>(owner)).TryRegisterHandle(entry)
            : WindowHotKeyManager.GetInstance(owner).TryRegisterHandle(entry),
            (_, _) => throw new InvalidOperationException("Backend capture tests must never persist configuration."), () => config);
    }

    private static HotKeys CreateEntry(string id, Key key, ModifierKeys modifiers, HotKeyCallBackHanlder callback,
        HotKeyKinds kinds = HotKeyKinds.Windows) => new(id, new(key, modifiers), callback) { Id = id, Kinds = kinds, DefaultKinds = kinds };

    private static IEnumerable<ModifierKeys> AllModifiers() => Enumerable.Range(0, 16).Select(value => (ModifierKeys)value);

    private static RegistrationSet RegisterWindowCombinations(Control control, Key key, HotKeyCallBackHanlder callback)
    {
        RegistrationSet result = new();
        try
        {
            foreach (ModifierKeys modifiers in AllModifiers())
                result.Add(Assert.IsAssignableFrom<IHotkeyRegistration>(RoutedHotkeys.Register(control, new(key, modifiers), callback)));
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    private static IHotkeyRegistration RequireNativeRegistration(HiddenWindow owner, Key key, HotKeyCallBackHanlder callback)
        => RequireRegistration(NativeHotkeys.TryRegister(owner.Handle, NativeModifiers, key, callback), key);

    private static IHotkeyRegistration RequireRegistration(HotkeyRegistrationAttempt attempt, Key key)
    {
        Assert.True(attempt.Registration?.IsRegistered == true, $"{NativeUnavailableMessage(key)} {attempt.Error}");
        return attempt.Registration!;
    }

    private static string NativeUnavailableMessage(Key key)
        => $"Could not acquire the isolated test shortcut Ctrl+Alt+Shift+{key}. It may be owned by another application; this test does not steal it.";

    private static int RegistrationId(IHotkeyRegistration registration)
        => Assert.IsType<int>(registration.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)!.GetValue(registration));

    private static bool HasNativeScope(IntPtr hwnd)
        => Assert.IsAssignableFrom<IDictionary>(typeof(NativeHotkeys).GetField("Scopes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)).Contains(hwnd);

    private static void PumpUntil(Func<bool> condition)
    {
        if (condition()) return;
        Stopwatch elapsed = Stopwatch.StartNew();
        DispatcherFrame frame = new();
        DispatcherTimer poll = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        poll.Tick += (_, _) => { if (condition() || elapsed.Elapsed > TimeSpan.FromSeconds(2)) frame.Continue = false; };
        try { poll.Start(); Dispatcher.PushFrame(frame); }
        finally { poll.Stop(); }
        Assert.True(condition(), "The test dispatcher did not observe hotkey release within two seconds.");
    }

    private sealed class KeyboardStateScope : IDisposable
    {
        private readonly Func<Key, bool> _previous = HotkeyDispatchGate.KeyStateReader;
        private readonly HashSet<Key> _down = new();

        public KeyboardStateScope()
        {
            Assert.False(HotkeyDispatchGate.IsSuspended);
            HotkeyDispatchGate.KeyStateReader = key => _down.Contains(key);
            try { PumpUntil(() => !HotkeyDispatchGate.HasPendingKeyRelease); }
            catch { HotkeyDispatchGate.KeyStateReader = _previous; throw; }
        }

        // Changes only the gate's injected read model, never desktop keyboard state.
        public void SetDown(Key key, bool down) { if (down) _down.Add(key); else _down.Remove(key); }

        public void Dispose()
        {
            _down.Clear();
            try { PumpUntil(() => !HotkeyDispatchGate.HasPendingKeyRelease); }
            finally { HotkeyDispatchGate.KeyStateReader = _previous; }
        }
    }

    private sealed class RegistrationSet : IDisposable
    {
        private readonly List<IHotkeyRegistration> _registrations = new();
        public void Add(IHotkeyRegistration registration) => _registrations.Add(registration);
        public void Dispose()
        {
            foreach (IHotkeyRegistration registration in _registrations) registration.Dispose();
            _registrations.Clear();
        }
    }

    private sealed class HiddenWindow : IDisposable
    {
        private bool _closed;
        public Window Window { get; } = new() { Width = 1, Height = 1, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
        public IntPtr Handle { get; }

        public HiddenWindow()
        {
            try
            {
                Handle = new WindowInteropHelper(Window).EnsureHandle();
                Assert.NotEqual(IntPtr.Zero, Handle);
                Assert.False(Window.IsVisible);
                Assert.False(IsWindowVisible(Handle));
            }
            catch { Window.Close(); throw; }
        }

        public void SendHotkey(IHotkeyRegistration registration) => SendHotkey(RegistrationId(registration), registration.Hotkey.Key);

        public void SendHotkey(int registrationId, Key key)
        {
            Assert.False(_closed);
            Assert.False(IsWindowVisible(Handle));
            GetWindowThreadProcessId(Handle, out uint processId);
            Assert.Equal((uint)Environment.ProcessId, processId);
            IntPtr parameters = new(((int)NativeModifiers & 0xffff) | (KeyInterop.VirtualKeyFromKey(key) << 16));
            SendMessage(Handle, NativeHotkeys.WMHOTKEY, new IntPtr(registrationId), parameters);
        }

        public KeyEventArgs RaiseKeyUp(UIElement target, Key key)
        {
            Assert.False(_closed);
            HwndSource source = Assert.IsType<HwndSource>(HwndSource.FromHwnd(Handle));
            KeyEventArgs arguments = new(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyUpEvent };
            target.RaiseEvent(arguments);
            return arguments;
        }

        public void Close()
        {
            if (_closed) return;
            Window.Close();
            _closed = true;
        }

        public void Dispose() => Close();

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    }
}
