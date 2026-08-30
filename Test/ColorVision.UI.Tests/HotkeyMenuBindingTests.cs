using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class HotkeyMenuBindingTests
{
    [Theory]
    [InlineData(typeof(ColorVision.UI.Desktop.Settings.MenuOptions), Key.I, ModifierKeys.Control)]
    [InlineData(typeof(ColorVision.UI.LogImp.MenuLogWindow), Key.L, ModifierKeys.Control)]
    [InlineData(typeof(ColorVision.Update.MenuCheckAndUpdateV1), Key.U, ModifierKeys.Control)]
    [InlineData(typeof(ColorVision.ExportMenuViewStatusBar), Key.B, ModifierKeys.Control | ModifierKeys.Shift)]
    public void BuiltInMenusKeepTheirDefaultDeclarationsAndFollowRuntimeOverrides(Type providerType, Key defaultKey, ModifierKeys defaultModifiers)
    {
        WpfTestHost.Invoke(() =>
        {
            // These four providers have side-effect-free constructors/declarations; do not execute their callbacks.
            var source = Assert.IsAssignableFrom<IMenuItem>(Activator.CreateInstance(providerType));
            HotKeys declaration = Assert.IsAssignableFrom<IHotKey>(source).HotKeys;
            Assert.Equal(new Hotkey(defaultKey, defaultModifiers), declaration.DefaultHotkey);
            string id = string.IsNullOrWhiteSpace(declaration.Id) ? providerType.FullName! : declaration.Id;
            var runtime = Runtime(id, Key.F12);
            var menu = new MenuItem { InputGestureText = source.InputGestureText };

            HotkeyMenuGestureBinding.Attach(menu, source, new ObservableCollection<HotKeys> { runtime });

            Assert.Equal("Ctrl+F12", menu.InputGestureText);
            Assert.Equal(new Hotkey(defaultKey, defaultModifiers), declaration.DefaultHotkey);
            Assert.False(runtime.IsRegistered);
        });
    }

    [Fact]
    public void ExistingRuntimeCombinationIsShownWithoutInvokingOrRegisteringTheAction()
    {
        WpfTestHost.Invoke(() =>
        {
            var source = new ExplicitHotkeyMenu();
            var runtime = Runtime("MENU.ACTION", Key.P);
            var menu = new MenuItem { InputGestureText = source.InputGestureText };

            HotkeyMenuGestureBinding.Attach(menu, source, new ObservableCollection<HotKeys> { runtime });

            Assert.Equal("Ctrl+P", menu.InputGestureText);
            Assert.Equal(1, source.DeclarationReads);
            Assert.Equal(0, source.Executions);
            Assert.False(runtime.IsRegistered);
            Assert.Null(runtime.Control);
            Assert.Equal(Key.I, source.Declaration.DefaultHotkey.Key);
            Assert.Equal("menu.action", source.Declaration.Id);
        });
    }

    [Fact]
    public void LegacyMenuCreatedBeforeDiscoveryUsesTypeIdentityAndNotTheDisplayName()
    {
        WpfTestHost.Invoke(() =>
        {
            var source = new LegacyHotkeyMenu();
            var hotkeys = new ObservableCollection<HotKeys> { Runtime("unrelated", Key.L, source.Header) };
            var menu = new MenuItem { InputGestureText = source.InputGestureText };
            HotkeyMenuGestureBinding.Attach(menu, source, hotkeys);
            Assert.Equal(string.Empty, menu.InputGestureText);

            var runtime = Runtime(typeof(LegacyHotkeyMenu).FullName!, Key.F8, "A different localized name");
            hotkeys.Add(runtime);
            Assert.Equal("Ctrl+F8", menu.InputGestureText);
            runtime.Name = "Renamed after discovery";
            runtime.Hotkey = new(Key.J, ModifierKeys.Control | ModifierKeys.Shift);
            Assert.Equal("Ctrl+Shift+J", menu.InputGestureText);
            Assert.Equal(1, source.DeclarationReads);
            Assert.Equal(0, source.Executions);
        });
    }

    [Fact]
    public void ClearResetAndDefinitionReplacementFollowTheCurrentRuntimeEntry()
    {
        WpfTestHost.Invoke(() =>
        {
            var source = new ExplicitHotkeyMenu();
            var original = Runtime("menu.action", Key.P);
            var hotkeys = new ObservableCollection<HotKeys> { original };
            var menu = new MenuItem();
            HotkeyMenuGestureBinding.Attach(menu, source, hotkeys);

            original.Hotkey = Hotkey.None;
            Assert.Equal(string.Empty, menu.InputGestureText);
            original.Hotkey = new(Key.I, ModifierKeys.Control);
            Assert.Equal("Ctrl+I", menu.InputGestureText);

            var replacement = Runtime("menu.action", Key.R);
            hotkeys[0] = replacement;
            Assert.Equal("Ctrl+R", menu.InputGestureText);
            original.Hotkey = new(Key.X, ModifierKeys.Alt);
            Assert.Equal("Ctrl+R", menu.InputGestureText);

            hotkeys.Clear();
            Assert.Equal(string.Empty, menu.InputGestureText);
            replacement.Hotkey = new(Key.Y, ModifierKeys.Alt);
            Assert.Equal(string.Empty, menu.InputGestureText);
            hotkeys.Add(Runtime("menu.action", Key.F4));
            Assert.Equal("Ctrl+F4", menu.InputGestureText);
        });
    }

    [Fact]
    public void OrdinaryMenuKeepsItsDeclaredGesture()
    {
        WpfTestHost.Invoke(() =>
        {
            var source = new PlainMenu();
            var runtime = Runtime(typeof(PlainMenu).FullName!, Key.P, source.Header);
            var hotkeys = new ObservableCollection<HotKeys> { runtime };
            var menu = new MenuItem { InputGestureText = source.InputGestureText };

            HotkeyMenuGestureBinding.Attach(menu, source, hotkeys);
            runtime.Hotkey = Hotkey.None;
            hotkeys.Clear();

            Assert.Equal("Alt + F4", menu.InputGestureText);
            Assert.Equal(0, source.Executions);
        });
    }

    [Fact]
    public void ReattachingReplacesThePreviousSubscription()
    {
        WpfTestHost.Invoke(() =>
        {
            var first = Runtime("menu.action", Key.P);
            var second = Runtime("menu.other", Key.O);
            var hotkeys = new ObservableCollection<HotKeys> { first, second };
            var menu = new MenuItem();
            HotkeyMenuGestureBinding.Attach(menu, new ExplicitHotkeyMenu(), hotkeys);
            var otherSource = new ExplicitHotkeyMenu();
            otherSource.Declaration.Id = "menu.other";
            HotkeyMenuGestureBinding.Attach(menu, otherSource, hotkeys);

            first.Hotkey = new(Key.X, ModifierKeys.Alt);
            Assert.Equal("Ctrl+O", menu.InputGestureText);
            second.Hotkey = new(Key.K, ModifierKeys.Windows);
            Assert.Equal("Win+K", menu.InputGestureText);
        });
    }

    [Fact]
    public void UnreadableDeclarationDoesNotBreakMenuConstructionOrInvokeAnAction()
    {
        WpfTestHost.Invoke(() =>
        {
            var source = new UnreadableHotkeyMenu();
            var menu = new MenuItem { InputGestureText = source.InputGestureText };

            HotkeyMenuGestureBinding.Attach(menu, source, new());

            Assert.Equal(string.Empty, menu.InputGestureText);
            Assert.Equal(0, source.Executions);
        });
    }

    [Fact]
    public void MultiActionProviderRequiresAnExplicitMenuActionIdentity()
    {
        WpfTestHost.Invoke(() =>
        {
            var source = new MultiActionMenu();
            var hotkeys = new ObservableCollection<HotKeys> { Runtime(typeof(MultiActionMenu).FullName!, Key.P), Runtime("menu.action", Key.F7) };
            var menu = new MenuItem();
            HotkeyMenuGestureBinding.Attach(menu, source, hotkeys);
            Assert.Equal(string.Empty, menu.InputGestureText);

            source.Declaration.Id = "menu.action";
            HotkeyMenuGestureBinding.Attach(menu, source, hotkeys);
            Assert.Equal("Ctrl+F7", menu.InputGestureText);
            Assert.Equal(0, source.EnumerationCalls);
            Assert.Equal(0, source.Executions);
        });
    }

    [Fact]
    public void RuntimeCollectionAndEntryDoNotKeepDiscardedMenuAlive()
    {
        WpfTestHost.Invoke(() =>
        {
            var runtime = Runtime("menu.action", Key.P);
            var hotkeys = new ObservableCollection<HotKeys> { runtime };
            WeakReference menu = CreateDiscardedMenu(hotkeys);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(menu.IsAlive);
            runtime.Hotkey = new(Key.L, ModifierKeys.Control);
            hotkeys.Clear();
            GC.KeepAlive(hotkeys);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDiscardedMenu(ObservableCollection<HotKeys> hotkeys)
    {
        var menu = new MenuItem();
        HotkeyMenuGestureBinding.Attach(menu, new ExplicitHotkeyMenu(), hotkeys);
        return new WeakReference(menu);
    }

    private static HotKeys Runtime(string id, Key key, string name = "Shared display name")
        => new(name, new(key, ModifierKeys.Control), () => throw new InvalidOperationException("Menu binding must not execute an action.")) { Id = id };

    private class PlainMenu : IMenuItem
    {
        public string TargetName => MenuItemConstants.MainWindowTarget;
        public string OwnerGuid => MenuItemConstants.Tool;
        public string GuidId => "shared-menu-guid";
        public int Order => 1;
        public string Header => "Shared display name";
        public string InputGestureText => "Alt + F4";
        public object? Icon => null;
        public ICommand? Command => null;
        public Visibility Visibility => Visibility.Visible;
        public bool? IsChecked => null;
        public int Executions { get; private set; }
        protected void Execute() => Executions++;
    }

    private class LegacyHotkeyMenu : PlainMenu, IHotKey
    {
        public int DeclarationReads { get; private set; }
        public HotKeys Declaration { get; }
        public LegacyHotkeyMenu() => Declaration = new(Header, new(Key.I, ModifierKeys.Control), Execute);
        public HotKeys HotKeys { get { DeclarationReads++; return Declaration; } }
    }

    private sealed class ExplicitHotkeyMenu : LegacyHotkeyMenu
    {
        public ExplicitHotkeyMenu() => Declaration.Id = "menu.action";
    }

    private sealed class UnreadableHotkeyMenu : PlainMenu, IHotKey
    {
        public HotKeys HotKeys => throw new InvalidOperationException("Unavailable declaration");
    }

    private sealed class MultiActionMenu : LegacyHotkeyMenu, IHotkeyProvider
    {
        public int EnumerationCalls { get; private set; }
        public IEnumerable<HotkeyDefinition> GetHotkeyDefinitions()
        {
            EnumerationCalls++;
            throw new InvalidOperationException("Menu binding must not enumerate hotkey providers.");
        }
    }
}
