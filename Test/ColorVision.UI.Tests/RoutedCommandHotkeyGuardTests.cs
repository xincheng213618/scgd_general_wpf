using ColorVision.UI.HotKey;
using ColorVision.UI.HotKey.WindowHotKey;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ColorVision.UI.Tests;

/// <summary>Isolated hidden WPF hosts, no desktop key injection or production configuration.</summary>
[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class RoutedCommandHotkeyGuardTests
{
    [Fact]
    public void ClearedManagedDefaultAlsoBlocksAnEditorsHardcodedFallback()
    {
        WpfTestHost.Invoke(() =>
        {
            using HiddenHost host = new();
            var action = host.AddAction("snapshot", Bindings(Key.F24), () => { });
            using RoutedCommandHotkeyGuard guard = new(host.Window, host.Service, [host.NativeCommand]);
            host.ReplaceBindings(action, []);
            int localFallbacks = 0;
            host.Window.PreviewKeyDown += (_, e) => { if (e.Key == Key.F24) localFallbacks++; };
            Assert.True(host.RaiseKeyDown(Key.F24).Handled);
            Assert.Equal(0, localFallbacks);
        });
    }

    [Fact]
    public void ClearingOrChangingAnActionBlocksItsNativeDefaultGesture()
    {
        WpfTestHost.Invoke(() =>
        {
            using HiddenHost host = new();
            int invoked = 0;
            HotKeys action = host.AddAction("open", Bindings(Key.F23), () => invoked++);
            using RoutedCommandHotkeyGuard guard = new(host.Window, host.Service, [host.NativeCommand]);
            Assert.False(guard.ShouldSuppress(Key.F23, ModifierKeys.None));

            host.ReplaceBindings(action, []);
            Assert.Contains(action, host.Service.HotKeys);
            Assert.False(action.IsRegistered);
            Assert.True(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
            Assert.True(host.RaiseKeyDown(Key.F23).Handled);
            Assert.Equal(0, invoked);
            Assert.Equal(0, host.NativeInvoked);

            host.ReplaceBindings(action, Bindings(Key.F24));
            Assert.True(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
            Assert.False(guard.ShouldSuppress(Key.F24, ModifierKeys.None));
            Assert.True(host.RaiseKeyDown(Key.F23).Handled);
            Assert.True(host.RaiseKeyDown(Key.F24).Handled);
            Assert.Equal(1, invoked);
            Assert.Equal(0, host.NativeInvoked);
        });
    }

    [Fact]
    public void RestoringAnActionAfterRegistrationRebuildAllowsItsCurrentBinding()
    {
        WpfTestHost.Invoke(() =>
        {
            using HiddenHost host = new();
            int invoked = 0;
            HotKeys action = host.AddAction("open", Bindings(Key.F23), () => invoked++);
            using RoutedCommandHotkeyGuard guard = new(host.Window, host.Service, [host.NativeCommand]);

            // Clearing the sole action detaches the old backend scope. Its replacement
            // will subscribe after the guard, so event subscription order cannot be relied on.
            host.ReplaceBindings(action, []);
            Assert.True(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
            host.Service.SetDefault();
            Assert.True(host.Service.LastApplyResult!.Success, host.Service.LastApplyResult.Message);
            Assert.False(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
            Assert.True(host.RaiseKeyDown(Key.F23).Handled);
            Assert.Equal(1, invoked);
            Assert.Equal(0, host.NativeInvoked);
        });
    }

    [Fact]
    public void RetainedBindingMetadataDoesNotAllowAnUnregisteredActionToFallThrough()
    {
        WpfTestHost.Invoke(() =>
        {
            using HiddenHost host = new();
            HotKeys action = host.AddAction("open", Bindings(Key.F23), () => { });
            using RoutedCommandHotkeyGuard guard = new(host.Window, host.Service, [host.NativeCommand]);
            host.Service.UnregisterAll();
            Assert.NotEmpty(action.GetBindings());
            Assert.False(action.IsRegistered);
            Assert.True(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
            Assert.True(host.RaiseKeyDown(Key.F23).Handled);
            Assert.Equal(0, host.NativeInvoked);
        });
    }

    [Fact]
    public void AnotherActionCanReuseTheNativeGestureIncludingAsAnAdditionalBinding()
    {
        WpfTestHost.Invoke(() =>
        {
            using HiddenHost host = new();
            int originalInvoked = 0;
            int replacementInvoked = 0;
            using RoutedCommandHotkeyGuard guard = new(host.Window, host.Service, [host.NativeCommand]);
            HotKeys original = host.AddAction("open", Bindings(Key.F24), () => originalInvoked++);
            HotKeys replacement = host.AddAction("other", Bindings(Key.F22).Concat(Bindings(Key.F23)), () => replacementInvoked++);

            Assert.False(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
            Assert.True(host.RaiseKeyDown(Key.F23).Handled);
            Assert.Equal(0, originalInvoked);
            Assert.Equal(1, replacementInvoked);
            Assert.Equal(0, host.NativeInvoked);

            host.ReplaceBindings(replacement, Bindings(Key.F22));
            Assert.True(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
            Assert.True(host.RaiseKeyDown(Key.F23).Handled);
            Assert.Equal(1, replacementInvoked);
        });
    }

    [Fact]
    public void OtherWindowRegistrationsDoNotUnlockTheHostOrLoseTheirOwnNativeCommands()
    {
        WpfTestHost.Invoke(() =>
        {
            using HiddenHost host = new();
            using HiddenHost other = new();
            int otherInvoked = 0;
            HotKeys action = host.AddAction("other-window", Bindings(Key.F23), () => otherInvoked++, other.Window);
            using RoutedCommandHotkeyGuard guard = new(host.Window, host.Service, [host.NativeCommand]);

            Assert.True(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
            Assert.True(host.RaiseKeyDown(Key.F23).Handled);
            Assert.Equal(0, host.NativeInvoked);
            Assert.Equal(0, otherInvoked);
            Assert.True(other.RaiseKeyDown(Key.F23).Handled);
            Assert.Equal(1, otherInvoked);

            host.ReplaceBindings(action, []);
            other.RaiseKeyDown(Key.F23);
            Assert.Equal(1, other.NativeInvoked);
            Assert.Equal(1, otherInvoked);
        });
    }

    [Fact]
    public void DisposingTheGuardRestoresNativeCommandRouting()
    {
        WpfTestHost.Invoke(() =>
        {
            using HiddenHost host = new();
            host.RaiseKeyDown(Key.F23);
            Assert.Equal(1, host.NativeInvoked);
            using RoutedCommandHotkeyGuard guard = new(host.Window, host.Service, [host.NativeCommand]);
            Assert.True(host.RaiseKeyDown(Key.F23).Handled);
            Assert.Equal(1, host.NativeInvoked);

            guard.Dispose();
            guard.Dispose();
            host.RaiseKeyDown(Key.F23);
            Assert.Equal(2, host.NativeInvoked);
        });
    }

    [Fact]
    public void CaptureLeavesPreviewInputAvailableToTheShortcutRecorder()
    {
        WpfTestHost.Invoke(() =>
        {
            using HiddenHost host = new();
            using RoutedCommandHotkeyGuard guard = new(host.Window, host.Service, [host.NativeCommand]);
            Func<Key, bool> previousKeyReader = HotkeyDispatchGate.KeyStateReader;
            HotkeyDispatchGate.KeyStateReader = _ => false;
            try
            {
                Assert.True(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
                using (HotkeyCaptureLease lease = host.Service.BeginCapture())
                {
                    Assert.True(HotkeyDispatchGate.IsSuspended);
                    Assert.False(host.RaiseKeyDown(Key.F23, bubble: false).Handled);
                }
                Assert.False(HotkeyDispatchGate.IsSuspended);
                Assert.True(host.RaiseKeyDown(Key.F23, bubble: false).Handled);
                Assert.Equal(0, host.NativeInvoked);
            }
            finally { HotkeyDispatchGate.KeyStateReader = previousKeyReader; }
        });
    }

    [Fact]
    public void CapturedHeldGestureCannotFallThroughToItsNativeCommandAfterTheRecorderCloses()
    {
        WpfTestHost.Invoke(() =>
        {
            using HiddenHost host = new();
            int invoked = 0;
            using RoutedCommandHotkeyGuard guard = new(host.Window, host.Service, [host.NativeCommand]);
            host.AddAction("open", Bindings(Key.F23), () => invoked++);
            Assert.False(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
            Func<Key, bool> previousKeyReader = HotkeyDispatchGate.KeyStateReader;
            bool held = true;
            HotkeyDispatchGate.KeyStateReader = key => held && key == Key.F23;
            try
            {
                using (HotkeyCaptureLease lease = host.Service.BeginCapture())
                    Assert.False(host.RaiseKeyDown(Key.F23, bubble: false).Handled);
                Assert.True(HotkeyDispatchGate.HasPendingKeyRelease);
                Assert.True(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
                Assert.True(host.RaiseKeyDown(Key.F23).Handled);
                Assert.Equal(0, invoked);
                Assert.Equal(0, host.NativeInvoked);

                held = false;
                host.RaiseKeyUp(Key.F23);
                Assert.False(HotkeyDispatchGate.HasPendingKeyRelease);
                Assert.False(guard.ShouldSuppress(Key.F23, ModifierKeys.None));
                Assert.True(host.RaiseKeyDown(Key.F23).Handled);
                Assert.Equal(1, invoked);
                Assert.Equal(0, host.NativeInvoked);
            }
            finally
            {
                HotkeyDispatchGate.ShouldSuppress(Key.F23, isKeyUp: true);
                HotkeyDispatchGate.KeyStateReader = previousKeyReader;
            }
        });
    }

    private static IEnumerable<Hotkey> Bindings(Key key)
        => Enumerable.Range(0, 16).Select(value => new Hotkey(key, (ModifierKeys)value));

    private sealed class HiddenHost : IDisposable
    {
        public Window Window { get; } = new() { Width = 1, Height = 1, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
        public HotkeyService Service { get; }
        public RoutedCommand NativeCommand { get; } = new();
        public int NativeInvoked { get; private set; }
        private readonly HwndSource _source;

        public HiddenHost()
        {
            try
            {
                _source = Assert.IsType<HwndSource>(HwndSource.FromHwnd(new WindowInteropHelper(Window).EnsureHandle()));
                Assert.False(Window.IsVisible);
                HotKeyConfig config = new();
                Service = new HotkeyService((control, action) => WindowHotKeyManager.GetInstance(control).TryRegisterHandle(action),
                    (_, _) => throw new InvalidOperationException("Guard tests must not persist settings."), () => config);
                foreach (Hotkey binding in Bindings(Key.F23))
                    NativeCommand.InputGestures.Add(new KeyGesture(binding.Key, binding.Modifiers));
                Window.CommandBindings.Add(new CommandBinding(NativeCommand, (_, _) => NativeInvoked++, (_, e) => e.CanExecute = true));
            }
            catch { Window.Close(); throw; }
        }

        public HotKeys AddAction(string id, IEnumerable<Hotkey> bindings, HotKeyCallBackHanlder callback, Window? owner = null)
        {
            HotKeys action = new(id, new(), callback) { Id = id };
            action.SetBindings(bindings);
            action.SetDefaultBindings(action.GetBindings());
            Assert.Equal(action.GetBindings().Count > 0, Service.AddHotKeys(owner ?? Window, action));
            return action;
        }

        public void ReplaceBindings(HotKeys action, IEnumerable<Hotkey> bindings)
        {
            HotkeySetting setting = HotkeySetting.FromHotKeys(action);
            setting.SetBindings(bindings);
            Service.ApplySettings([setting]);
            Assert.True(Service.LastApplyResult!.Success, Service.LastApplyResult.Message);
        }

        public KeyEventArgs RaiseKeyDown(Key key, bool bubble = true)
        {
            KeyEventArgs arguments = new(Keyboard.PrimaryDevice, _source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            Window.RaiseEvent(arguments);
            if (bubble)
            {
                arguments.RoutedEvent = Keyboard.KeyDownEvent;
                Window.RaiseEvent(arguments);
            }
            return arguments;
        }

        public void RaiseKeyUp(Key key)
        {
            KeyEventArgs arguments = new(Keyboard.PrimaryDevice, _source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyUpEvent };
            Window.RaiseEvent(arguments);
        }

        public void Dispose()
        {
            Service.UnregisterAll();
            Window.Close();
        }
    }
}
