using ColorVision.UI.HotKey;
using ColorVision.UI.HotKey.GlobalHotKey;
using ColorVision.UI.HotKey.WindowHotKey;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Xunit;
using NativeHotkeys = ColorVision.UI.HotKey.GlobalHotKey.GlobalHotKey;
using RoutedHotkeys = ColorVision.UI.HotKey.WindowHotKey.WindowHotKey;

namespace ColorVision.UI.Tests;

public class HotkeyMultipleBindingTests
{
    private const ModifierKeys NativeModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;

    [Fact]
    public void MultipleCurrentAndDefaultBindingsAreIndependentSnapshots()
    {
        Hotkey primary = new(Key.I, ModifierKeys.Control);
        Hotkey alternate = new(Key.O, ModifierKeys.Control | ModifierKeys.Shift);
        HotKeys entry = new("options", primary, () => { });
        entry.SetBindings([primary, alternate]);
        entry.SetDefaultBindings([primary, alternate]);
        primary.Key = Key.P;
        alternate.Key = Key.Q;
        Assert.Equal([Key.I, Key.O], entry.GetBindings().Select(binding => binding.Key));
        Assert.Equal([Key.I, Key.O], entry.GetDefaultBindings().Select(binding => binding.Key));

        entry.GetBindings()[1].Key = Key.A;
        entry.AdditionalHotkeys[0].Key = Key.B;
        Assert.Equal(Key.O, entry.DefaultAdditionalHotkeys[0].Key);
        HotkeySetting setting = HotkeySetting.FromHotKeys(entry);
        setting.AdditionalHotkeys[0].Key = Key.C;
        Assert.Equal(Key.B, entry.AdditionalHotkeys[0].Key);

        entry.SetBindings([]);
        Assert.Empty(entry.GetBindings());
        Assert.True(entry.Hotkey.IsEmpty);
        Assert.Empty(entry.AdditionalHotkeys);
        Assert.Equal(2, entry.GetDefaultBindings().Count);
        Assert.True(Hotkey.None.IsEmpty);
    }

    [Fact]
    public void DefinitionCreatesDistinctCurrentAndDefaultAlternates()
    {
        Hotkey alternate = new(Key.O, ModifierKeys.Control | ModifierKeys.Shift);
        HotkeyDefinition definition = new("options", "Options", new(Key.I, ModifierKeys.Control), () => { })
        {
            AdditionalDefaultHotkeys = [alternate]
        };
        alternate.Key = Key.Q;
        HotKeys first = definition.CreateRuntimeHotKeys();
        HotKeys second = definition.CreateRuntimeHotKeys();
        first.AdditionalHotkeys[0].Key = Key.K;
        first.DefaultAdditionalHotkeys[0].Key = Key.L;
        Assert.Equal(Key.O, definition.AdditionalDefaultHotkeys[0].Key);
        Assert.Equal(Key.O, second.AdditionalHotkeys[0].Key);
        Assert.Equal(Key.O, second.DefaultAdditionalHotkeys[0].Key);
        Assert.Equal(Key.I, second.Hotkey.Key);
    }

    [Fact]
    public void NewConfigurationRoundTripsAllBindingsWithoutSharingOrAppendingValues()
    {
        HotkeySetting first = new() { Id = "first" };
        first.SetBindings([new(Key.I, ModifierKeys.Control), new(Key.O, ModifierKeys.Control | ModifierKeys.Shift)]);
        HotkeySetting second = new() { Id = "second" };
        second.SetBindings([new(Key.L, ModifierKeys.Control), new(Key.U, ModifierKeys.Control)]);
        string json = JsonConvert.SerializeObject(new[] { first, second });
        HotkeySetting[] result = JsonConvert.DeserializeObject<HotkeySetting[]>(json)!;
        Assert.Equal([Key.I, Key.O], result[0].GetBindings().Select(binding => binding.Key));
        Assert.Equal([Key.L, Key.U], result[1].GetBindings().Select(binding => binding.Key));
        result[0].AdditionalHotkeys[0].Key = Key.K;
        Assert.Equal(Key.U, result[1].AdditionalHotkeys[0].Key);
        Assert.Equal(Key.O, first.AdditionalHotkeys[0].Key);
        Assert.True(Hotkey.None.IsEmpty);

        JsonConvert.PopulateObject(JsonConvert.SerializeObject(second), first);
        Assert.Single(first.AdditionalHotkeys);
        Assert.Equal(Key.U, first.AdditionalHotkeys[0].Key);
    }

    [Fact]
    public void AdditionalEmptySlotsRemainVisibleToValidation()
    {
        HotkeySetting setting = new() { AdditionalHotkeys = [new(), null!, new(Key.F23, ModifierKeys.None)] };
        Assert.Equal(3, setting.GetBindings().Count);
        Assert.True(setting.GetBindings()[0].IsEmpty);
        Assert.True(setting.GetBindings()[1].IsEmpty);
        HotKeys entry = new() { AdditionalHotkeys = setting.AdditionalHotkeys };
        Assert.Equal(3, entry.GetBindings().Count);
        Assert.True(Hotkey.None.IsEmpty);
    }

    [Fact]
    public void GroupRegistrationFailureReleasesEarlierBindingsAndNeverDispatches()
    {
        List<FakeRegistration> children = new();
        int invoked = 0;
        HotkeyRegistrationAttempt attempt = HotkeyRegistrationGroup.TryRegister(TestBindings(), () => invoked++, (binding, callback) =>
        {
            callback(); // A backend that synchronously emits while registering must not dispatch a partial group.
            if (children.Count > 0) return new(null, "second binding occupied");
            FakeRegistration registration = new(binding, callback);
            children.Add(registration);
            return new(registration);
        });
        Assert.Null(attempt.Registration);
        Assert.Contains("occupied", attempt.Error);
        Assert.False(children[0].IsRegistered);
        Assert.Equal(1, children[0].DisposeCount);
        Assert.Equal(0, invoked);
    }

    [Fact]
    public void GroupDisposalReleasesEveryChildAndCanRetryFailedOnes()
    {
        List<FakeRegistration> children = new();
        int invoked = 0;
        HotkeyRegistrationGroup group = Assert.IsType<HotkeyRegistrationGroup>(HotkeyRegistrationGroup.TryRegister(TestBindings(), () => invoked++,
            (binding, callback) => { FakeRegistration child = new(binding, callback); children.Add(child); return new(child); }).Registration);
        children.ForEach(child => child.Callback());
        Assert.Equal(2, invoked);
        children[0].FailNextDispose = true;
        Assert.Throws<AggregateException>(group.Dispose);
        Assert.False(group.IsRegistered);
        Assert.True(children[0].IsRegistered);
        Assert.False(children[1].IsRegistered);
        Assert.Equal(1, children[1].DisposeCount);
        children[0].Callback();
        Assert.Equal(2, invoked);
        group.Dispose();
        Assert.False(children[0].IsRegistered);
        Assert.Equal(2, children[0].DisposeCount);
        Assert.Equal(1, children[1].DisposeCount);
    }

    [Fact]
    public void FailedRegistrationReturnsOwnershipWhenRollbackNeedsRetry()
    {
        FakeRegistration? first = null;
        int invoked = 0;
        HotkeyRegistrationAttempt attempt = HotkeyRegistrationGroup.TryRegister(TestBindings(), () => invoked++, (binding, callback) =>
        {
            if (first != null) return new(null, "occupied");
            first = new(binding, callback) { FailNextDispose = true };
            return new(first);
        });
        IHotkeyRegistration retained = Assert.IsAssignableFrom<IHotkeyRegistration>(attempt.Registration);
        Assert.False(retained.IsRegistered);
        Assert.True(first!.IsRegistered);
        Assert.Contains("occupied", attempt.Error);
        Assert.Contains("清理部分注册失败", attempt.Error);
        first.Callback();
        Assert.Equal(0, invoked);
        retained.Dispose();
        Assert.False(first.IsRegistered);
    }

    [Fact]
    public void DuplicateOrEmptyAdditionalBindingDoesNotStartRegistration()
    {
        int attempts = 0;
        HotkeyRegistrationAttempt Register(Hotkey binding, HotKeyCallBackHanlder callback)
        {
            attempts++;
            return new(new FakeRegistration(binding, callback));
        }
        Assert.Null(HotkeyRegistrationGroup.TryRegister([new(Key.F23, ModifierKeys.None), new(Key.F23, ModifierKeys.None)], () => { }, Register).Registration);
        Assert.Null(HotkeyRegistrationGroup.TryRegister([new(Key.F23, ModifierKeys.None), new()], () => { }, Register).Registration);
        Assert.Equal(0, attempts);
    }

    [Fact]
    public void WindowAlternatesDispatchAndReplacementChecksEveryBindingAndCallback()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            WindowHotKeyManager manager = WindowHotKeyManager.GetInstance(owner.Window);
            int oldInvoked = 0;
            int newInvoked = 0;
            List<HotKeys> entries = Enumerable.Range(0, 16).Select(value =>
            {
                HotKeys entry = new("multi-" + value, new(Key.F23, (ModifierKeys)value), () => oldInvoked++);
                entry.AdditionalHotkeys = [new(Key.F24, (ModifierKeys)value)];
                Require(manager.TryRegisterHandle(entry));
                return entry;
            }).ToList();
            try
            {
                foreach (HotKeys entry in entries) Assert.Same(entry.Registration, manager.RegisterHandle(entry));
                Assert.True(owner.RaiseKeyDown(Key.F23).Handled);
                Assert.True(owner.RaiseKeyDown(Key.F24).Handled);
                Assert.False(owner.RaiseKeyUp(Key.F23).Handled);
                Assert.False(owner.RaiseKeyUp(Key.F24).Handled);
                Assert.Equal(2, oldInvoked);
                foreach (HotKeys entry in entries)
                {
                    IHotkeyRegistration original = entry.Registration!;
                    entry.AdditionalHotkeys[0].Key = Key.F22;
                    IHotkeyRegistration replacement = Require(manager.TryRegisterHandle(entry));
                    Assert.NotSame(original, replacement);
                    Assert.False(original.IsRegistered);
                }
                Assert.False(owner.RaiseKeyDown(Key.F24).Handled);
                Assert.True(owner.RaiseKeyDown(Key.F22).Handled);
                Assert.Equal(3, oldInvoked);
                foreach (HotKeys entry in entries)
                {
                    IHotkeyRegistration original = entry.Registration!;
                    entry.HotKeyHandler = () => newInvoked++;
                    Assert.NotSame(original, Require(manager.TryRegisterHandle(entry)));
                    Assert.False(original.IsRegistered);
                }
                Assert.True(owner.RaiseKeyDown(Key.F23).Handled);
                Assert.True(owner.RaiseKeyDown(Key.F22).Handled);
                Assert.False(owner.RaiseKeyUp(Key.F23).Handled);
                Assert.False(owner.RaiseKeyUp(Key.F22).Handled);
                Assert.Equal(3, oldInvoked);
                Assert.Equal(2, newInvoked);
                owner.Close();
                Assert.All(entries, entry => { Assert.False(entry.IsRegistered); Assert.Null(entry.Registration); });
                Assert.False(WindowHotKeyManager.Instances.ContainsKey(owner.Window));
            }
            finally { foreach (HotKeys entry in entries) entry.Registration?.Dispose(); }
        });
    }

    [Fact]
    public void WindowSecondBindingConflictReleasesTheFirstBinding()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            WindowHotKeyManager manager = WindowHotKeyManager.GetInstance(owner.Window);
            using IHotkeyRegistration occupied = Assert.IsAssignableFrom<IHotkeyRegistration>(RoutedHotkeys.Register(owner.Window, new(Key.F24, ModifierKeys.None), () => { }));
            HotKeys entry = new("multi", new(Key.F23, ModifierKeys.None), () => { }) { AdditionalHotkeys = [new(Key.F24, ModifierKeys.None)] };
            HotkeyRegistrationAttempt attempt = manager.TryRegisterHandle(entry);
            using (attempt.Registration)
            {
                Assert.Null(attempt.Registration);
                Assert.False(entry.IsRegistered);
                Assert.NotEmpty(attempt.Error!);
                Assert.True(occupied.IsRegistered);
                using IHotkeyRegistration available = Assert.IsAssignableFrom<IHotkeyRegistration>(RoutedHotkeys.Register(owner.Window, new(Key.F23, ModifierKeys.None), () => { }));
            }
        });
    }

    [Fact]
    public void RealGlobalAlternatesDispatchAndClearingOneReleasesOnlyItsNativeSlot()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            using HiddenWindow contender = new();
            GlobalHotKeyManager manager = GlobalHotKeyManager.GetInstance(owner.Window);
            int invoked = 0;
            HotKeys entry = new("multi", new(Key.F23, NativeModifiers), () => invoked++)
                { Kinds = HotKeyKinds.Global, AdditionalHotkeys = [new(Key.F24, NativeModifiers)] };
            try
            {
                HotkeyRegistrationGroup group = Assert.IsType<HotkeyRegistrationGroup>(Require(manager.TryRegisterHandle(entry)));
                Assert.Same(group, manager.RegisterHandle(entry));
                Assert.Equal(2, group.Registrations.Count);
                foreach (IHotkeyRegistration registration in group.Registrations) owner.SendHotkey(registration);
                Assert.Equal(2, invoked);
                entry.SetBindings([new(Key.F23, NativeModifiers)]);
                IHotkeyRegistration single = Require(manager.TryRegisterHandle(entry));
                Assert.False(group.IsRegistered);
                Assert.All(group.Registrations, child => Assert.False(child.IsRegistered));
                using IHotkeyRegistration releasedAlternate = Require(NativeHotkeys.TryRegister(contender.Handle, NativeModifiers, Key.F24, () => { }));
                using IHotkeyRegistration? stillOwned = NativeHotkeys.TryRegister(contender.Handle, NativeModifiers, Key.F23, () => { }).Registration;
                Assert.Null(stillOwned);
                owner.SendHotkey(single);
                Assert.Equal(3, invoked);
                owner.Close();
                Assert.False(single.IsRegistered);
                Assert.False(entry.IsRegistered);
                Assert.Null(entry.Registration);
                using IHotkeyRegistration releasedPrimary = Require(NativeHotkeys.TryRegister(contender.Handle, NativeModifiers, Key.F23, () => { }));
            }
            finally { entry.Registration?.Dispose(); }
        });
    }

    [Fact]
    public void RealGlobalSecondBindingConflictRollsBackFirstWithoutDisplacingOwner()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            using HiddenWindow contender = new();
            using IHotkeyRegistration occupied = Require(NativeHotkeys.TryRegister(contender.Handle, NativeModifiers, Key.F24, () => { }));
            HotKeys entry = new("multi", new(Key.F23, NativeModifiers), () => { })
                { Kinds = HotKeyKinds.Global, AdditionalHotkeys = [new(Key.F24, NativeModifiers)] };
            GlobalHotKeyManager manager = GlobalHotKeyManager.GetInstance(owner.Window);
            HotkeyRegistrationAttempt attempt = manager.TryRegisterHandle(entry);
            using (attempt.Registration)
            {
                Assert.Null(attempt.Registration);
                Assert.False(entry.IsRegistered);
                Assert.Contains("1409", attempt.Error);
                Assert.True(occupied.IsRegistered);
                using IHotkeyRegistration released = Require(NativeHotkeys.TryRegister(contender.Handle, NativeModifiers, Key.F23, () => { }));
            }
        });
    }

    [Fact]
    public void ClosingHiddenGlobalOwnerReleasesTheEntireGroup()
    {
        WpfTestHost.Invoke(() =>
        {
            using KeyboardStateScope keyboard = new();
            using HiddenWindow owner = new();
            using HiddenWindow contender = new();
            GlobalHotKeyManager manager = GlobalHotKeyManager.GetInstance(owner.Window);
            HotKeys entry = new("multi", new(Key.F23, NativeModifiers), () => { })
                { Kinds = HotKeyKinds.Global, AdditionalHotkeys = [new(Key.F24, NativeModifiers)] };
            try
            {
                HotkeyRegistrationGroup group = Assert.IsType<HotkeyRegistrationGroup>(Require(manager.TryRegisterHandle(entry)));
                owner.Close();
                Assert.False(group.IsRegistered);
                Assert.All(group.Registrations, registration => Assert.False(registration.IsRegistered));
                Assert.Null(entry.Registration);
                Assert.False(entry.IsRegistered);
                Assert.False(GlobalHotKeyManager.Instances.ContainsKey(owner.Handle));
                using IHotkeyRegistration primary = Require(NativeHotkeys.TryRegister(contender.Handle, NativeModifiers, Key.F23, () => { }));
                using IHotkeyRegistration alternate = Require(NativeHotkeys.TryRegister(contender.Handle, NativeModifiers, Key.F24, () => { }));
            }
            finally { entry.Registration?.Dispose(); }
        });
    }

    private static IReadOnlyList<Hotkey> TestBindings() => [new(Key.F23, ModifierKeys.None), new(Key.F24, ModifierKeys.None)];

    private static IHotkeyRegistration Require(HotkeyRegistrationAttempt attempt)
    {
        Assert.True(attempt.Registration?.IsRegistered == true,
            "Could not register the isolated test shortcut. An external owner will not be displaced. " + attempt.Error);
        return attempt.Registration!;
    }

    private sealed class FakeRegistration(Hotkey hotkey, HotKeyCallBackHanlder callback) : IHotkeyRegistration
    {
        public Hotkey Hotkey { get; } = new(hotkey.Key, hotkey.Modifiers);
        public HotKeyCallBackHanlder Callback { get; } = callback;
        public bool IsRegistered { get; private set; } = true;
        public bool FailNextDispose { get; set; }
        public int DisposeCount { get; private set; }
        public void Dispose()
        {
            DisposeCount++;
            if (FailNextDispose) { FailNextDispose = false; throw new InvalidOperationException("Simulated release failure"); }
            IsRegistered = false;
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

        public void SendHotkey(IHotkeyRegistration registration)
        {
            Assert.False(_closed);
            Assert.False(IsWindowVisible(Handle));
            GetWindowThreadProcessId(Handle, out uint processId);
            Assert.Equal((uint)Environment.ProcessId, processId);
            int id = Assert.IsType<int>(registration.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)!.GetValue(registration));
            IntPtr parameters = new(((int)NativeModifiers & 0xffff) | (KeyInterop.VirtualKeyFromKey(registration.Hotkey.Key) << 16));
            SendMessage(Handle, NativeHotkeys.WMHOTKEY, new IntPtr(id), parameters);
        }

        public KeyEventArgs RaiseKeyDown(Key key) => RaiseKeyEvent(key, Keyboard.PreviewKeyDownEvent);

        public KeyEventArgs RaiseKeyUp(Key key) => RaiseKeyEvent(key, Keyboard.PreviewKeyUpEvent);

        private KeyEventArgs RaiseKeyEvent(Key key, RoutedEvent routedEvent)
        {
            HwndSource source = Assert.IsType<HwndSource>(HwndSource.FromHwnd(Handle));
            KeyEventArgs arguments = new(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = routedEvent };
            Window.RaiseEvent(arguments);
            return arguments;
        }

        public void Close() { if (!_closed) { Window.Close(); _closed = true; } }
        public void Dispose() => Close();

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    }

    private sealed class KeyboardStateScope : IDisposable
    {
        private readonly Func<Key, bool> _previous = HotkeyDispatchGate.KeyStateReader;

        public KeyboardStateScope()
        {
            Assert.False(HotkeyDispatchGate.IsSuspended);
            HotkeyDispatchGate.KeyStateReader = _ => false;
            try
            {
                if (!HotkeyDispatchGate.HasPendingKeyRelease) return;
                Stopwatch elapsed = Stopwatch.StartNew();
                DispatcherFrame frame = new();
                DispatcherTimer poll = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
                poll.Tick += (_, _) => { if (!HotkeyDispatchGate.HasPendingKeyRelease || elapsed.Elapsed > TimeSpan.FromSeconds(2)) frame.Continue = false; };
                try { poll.Start(); Dispatcher.PushFrame(frame); }
                finally { poll.Stop(); }
                Assert.False(HotkeyDispatchGate.HasPendingKeyRelease);
            }
            catch { HotkeyDispatchGate.KeyStateReader = _previous; throw; }
        }

        public void Dispose() => HotkeyDispatchGate.KeyStateReader = _previous;
    }
}
