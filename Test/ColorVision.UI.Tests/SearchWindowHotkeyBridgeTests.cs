using ColorVision.UI.HotKey;
using ColorVision.UI.Serach;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class SearchWindowHotkeyBridgeTests
{
    [Fact]
    public void SearchWindowUsesCurrentBindingsAndConsumesRepeatsWithoutInvokingTheAction()
    {
        WpfTestHost.Invoke(() =>
        {
            using Fixture host = new();
            host.Action.SetBindings([new Hotkey(Key.F12, ModifierKeys.Control), new Hotkey(Key.F24, ModifierKeys.None)]);
            Assert.False(host.Bridge.TryRefocus(Key.P, ModifierKeys.Control | ModifierKeys.Shift, false, true));
            Assert.True(host.Bridge.TryRefocus(Key.F12, ModifierKeys.Control, false, true));
            Assert.True(host.Bridge.TryRefocus(Key.F12, ModifierKeys.Control, true, true));
            Assert.True(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
            Assert.Equal(2, host.RefocusCalls);
            host.Action.SetBindings([]);
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
        });
    }

    [Fact]
    public void OnlyTheSearchWindowsActiveStateGatesInputNotTheInactiveMainOwner()
    {
        WpfTestHost.Invoke(() =>
        {
            using Fixture host = new();
            Assert.False(host.Owner.IsActive);
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, false));
            Assert.True(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
            Assert.Equal(1, host.RefocusCalls);
        });
    }

    [Fact]
    public void UnregisteredGlobalForeignAndUnrelatedActionsAreNotDispatched()
    {
        WpfTestHost.Invoke(() =>
        {
            using Fixture host = new();
            host.Action.IsRegistered = false;
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
            host.Action.IsRegistered = true;
            host.Action.IsGlobal = true;
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
            host.Action.IsGlobal = false;
            host.Action.Control = new Button();
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
            host.Action.Control = host.Owner;
            host.Action.Id = "unrelated";
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
            Assert.Equal(0, host.RefocusCalls);
        });
    }

    [Fact]
    public void ImeCaptureAndHeldTailNeverRefocusTheSearchWindow()
    {
        WpfTestHost.Invoke(() =>
        {
            using Fixture host = new();
            Assert.False(host.Bridge.TryRefocus(Key.ImeProcessed, ModifierKeys.None, false, true));
            Func<Key, bool> previousReader = HotkeyDispatchGate.KeyStateReader;
            bool held = true;
            HotkeyDispatchGate.KeyStateReader = key => held && key == Key.F24;
            try
            {
                using (host.Service.BeginCapture())
                    Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
                Assert.True(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
                Assert.Equal(0, host.RefocusCalls);
                held = false;
                HotkeyDispatchGate.ShouldSuppress(Key.F24, isKeyUp: true);
                Assert.True(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false, true));
                Assert.Equal(1, host.RefocusCalls);
            }
            finally
            {
                HotkeyDispatchGate.ShouldSuppress(Key.F24, isKeyUp: true);
                HotkeyDispatchGate.KeyStateReader = previousReader;
            }
        });
    }

    private sealed class Fixture : IDisposable
    {
        public Window Owner { get; } = new();
        public Window SearchWindow { get; } = new();
        public HotkeyService Service { get; }
        public HotKeys Action { get; }
        public SearchWindowHotkeyBridge Bridge { get; }
        public int RefocusCalls { get; private set; }

        public Fixture()
        {
            Service = new HotkeyService((_, _) => throw new InvalidOperationException("No OS registration in bridge tests."),
                (_, _) => throw new InvalidOperationException("No persistence in bridge tests."), () => new HotKeyConfig());
            Action = new("palette", new Hotkey(Key.F24, ModifierKeys.None), () => throw new InvalidOperationException("The bridge must only refocus."))
            { Id = "palette", Control = Owner, IsRegistered = true };
            Service.HotKeys.Add(Action);
            Bridge = new SearchWindowHotkeyBridge(Owner, SearchWindow, Service, ["palette", "contextual-find"], () => RefocusCalls++);
        }

        public void Dispose() { Bridge.Dispose(); SearchWindow.Close(); Owner.Close(); }
    }
}
