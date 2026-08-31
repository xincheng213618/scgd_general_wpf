using ColorVision.UI.HotKey;
using ColorVision.UI.Serach;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class SearchPopupHotkeyBridgeTests
{
    [Fact]
    public void PopupUsesCurrentBindingsAndConsumesRepeatsWithoutInvokingTheAction()
    {
        WpfTestHost.Invoke(() =>
        {
            using Fixture host = new();
            host.Action.SetBindings([new Hotkey(Key.F12, ModifierKeys.Control), new Hotkey(Key.F24, ModifierKeys.None)]);
            Assert.False(host.Bridge.TryRefocus(Key.P, ModifierKeys.Control | ModifierKeys.Shift, false));
            Assert.True(host.Bridge.TryRefocus(Key.F12, ModifierKeys.Control, false));
            Assert.True(host.Bridge.TryRefocus(Key.F12, ModifierKeys.Control, true));
            Assert.True(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false));
            Assert.Equal(2, host.RefocusCalls);
            host.Action.SetBindings([]);
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false));
        });
    }

    [Fact]
    public void UnregisteredGlobalForeignAndUnrelatedActionsAreNotDispatched()
    {
        WpfTestHost.Invoke(() =>
        {
            using Fixture host = new();
            host.Action.IsRegistered = false;
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false));
            host.Action.IsRegistered = true;
            host.Action.IsGlobal = true;
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false));
            host.Action.IsGlobal = false;
            host.Action.Control = new Button();
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false));
            host.Action.Control = host.Owner;
            host.Action.Id = "unrelated";
            Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false));
            Assert.Equal(0, host.RefocusCalls);
        });
    }

    [Fact]
    public void ImeCaptureAndHeldTailNeverRefocusThePopup()
    {
        WpfTestHost.Invoke(() =>
        {
            using Fixture host = new();
            Assert.False(host.Bridge.TryRefocus(Key.ImeProcessed, ModifierKeys.None, false));
            Func<Key, bool> previousReader = HotkeyDispatchGate.KeyStateReader;
            bool held = true;
            HotkeyDispatchGate.KeyStateReader = key => held && key == Key.F24;
            try
            {
                using (host.Service.BeginCapture())
                    Assert.False(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false));
                Assert.True(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false));
                Assert.Equal(0, host.RefocusCalls);
                held = false;
                HotkeyDispatchGate.ShouldSuppress(Key.F24, isKeyUp: true);
                Assert.True(host.Bridge.TryRefocus(Key.F24, ModifierKeys.None, false));
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
        public HotkeyService Service { get; }
        public HotKeys Action { get; }
        public SearchPopupHotkeyBridge Bridge { get; }
        public int RefocusCalls { get; private set; }

        public Fixture()
        {
            Service = new HotkeyService((_, _) => throw new InvalidOperationException("No OS registration in bridge tests."),
                (_, _) => throw new InvalidOperationException("No persistence in bridge tests."), () => new HotKeyConfig());
            Action = new("palette", new Hotkey(Key.F24, ModifierKeys.None), () => throw new InvalidOperationException("The bridge must only refocus."))
            { Id = "palette", Control = Owner, IsRegistered = true };
            Service.HotKeys.Add(Action);
            Bridge = new SearchPopupHotkeyBridge(Owner, new Grid(), Service, ["palette", "contextual-find"], () => RefocusCalls++);
        }

        public void Dispose() { Bridge.Dispose(); Owner.Close(); }
    }
}
