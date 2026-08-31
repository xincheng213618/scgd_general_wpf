using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base.File;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class BuiltInShortcutDefaultsTests
{
    private static readonly Type[] ShellMenuTypes =
    [
        typeof(Desktop.Settings.MenuOptions),
        typeof(LogImp.MenuLogWindow),
        typeof(ColorVision.Update.MenuCheckAndUpdateV1),
        typeof(ColorVision.ExportMenuViewStatusBar),
        typeof(ColorVision.AboutMsgExport),
        typeof(ColorVision.Solution.Workspace.MenuResetLayout),
    ];

    [Fact]
    public void SettingsUsesConventionalControlCommaWithoutLegacyAlias()
    {
        HotKeys declaration = new Desktop.Settings.MenuOptions().HotKeys;

        Assert.Equal(new Hotkey(Key.OemComma, ModifierKeys.Control), Assert.Single(declaration.GetBindings()));
        Assert.Equal(declaration.GetBindings(), declaration.GetDefaultBindings());
        Assert.Equal(BuiltInHotkeyDescriptions.OpenSettings, declaration.Description);
    }

    [Theory]
    [InlineData(typeof(ColorVision.Update.MenuCheckAndUpdateV1))]
    [InlineData(typeof(ColorVision.ExportMenuViewStatusBar))]
    [InlineData(typeof(ColorVision.AboutMsgExport))]
    public void InfrequentActionsRemainDiscoverableButUnassigned(Type providerType)
    {
        var source = Assert.IsAssignableFrom<IMenuItem>(Activator.CreateInstance(providerType));
        HotKeys declaration = Assert.IsAssignableFrom<IHotKey>(source).HotKeys;

        Assert.Empty(declaration.GetBindings());
        Assert.Empty(declaration.GetDefaultBindings());
        Assert.False(declaration.IsGlobal);
        Assert.NotNull(declaration.HotKeyHandler);
        Assert.False(string.IsNullOrWhiteSpace(declaration.Name));
        Assert.False(string.IsNullOrWhiteSpace(declaration.Description));
        Assert.True(string.IsNullOrEmpty(source.InputGestureText));
    }

    [Theory]
    [InlineData(typeof(LogImp.MenuLogWindow), Key.L, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(typeof(ColorVision.Solution.MenuOpenSolution), Key.O, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(typeof(ColorVision.Solution.Workspace.MenuResetLayout), Key.R, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)]
    public void NewlyAssignedActionsExposeDefaultsAndMenuHintsFollowEditsClearAndReset(Type providerType, Key key, ModifierKeys modifiers)
    {
        WpfTestHost.Invoke(() =>
        {
            var source = Assert.IsAssignableFrom<IMenuItem>(Activator.CreateInstance(providerType));
            HotKeys declaration = Assert.IsAssignableFrom<IHotKey>(source).HotKeys;
            var expected = new Hotkey(key, modifiers);
            Assert.Equal(expected, Assert.Single(declaration.GetBindings()));
            Assert.Equal(expected, Assert.Single(declaration.GetDefaultBindings()));
            Assert.Equal(HotKeyKinds.Windows, declaration.Kinds);
            Assert.Equal(HotKeyKinds.Windows, declaration.DefaultKinds);
            Assert.False(string.IsNullOrWhiteSpace(declaration.Description));
            Assert.NotNull(declaration.HotKeyHandler);

            // Reading declarations and attaching presentation never registers or invokes an action.
            declaration.Id = string.IsNullOrWhiteSpace(declaration.Id) ? providerType.FullName! : declaration.Id;
            var menu = new MenuItem { Header = source.Header };
            HotkeyMenuGestureBinding.Attach(menu, source, new ObservableCollection<HotKeys> { declaration });
            Assert.Equal(HotkeyInput.Format(expected), menu.InputGestureText);

            declaration.SetBindings([new Hotkey(Key.F11, ModifierKeys.Control | ModifierKeys.Shift)]);
            Assert.Equal("Ctrl+Shift+F11", menu.InputGestureText);
            declaration.SetBindings([]);
            Assert.Empty(menu.InputGestureText);
            declaration.SetBindings(declaration.GetDefaultBindings());
            Assert.Equal(HotkeyInput.Format(expected), menu.InputGestureText);
            Assert.Equal(expected, Assert.Single(declaration.GetDefaultBindings()));
            Assert.False(declaration.IsRegistered);
        });
    }

    [Fact]
    public void ShellFileAndSearchDefaultsAreUniqueAndDoNotReserveCommonEditingKeys()
    {
        Type[] fileAndSearchTypes =
        [
            typeof(MenuFileOpen), typeof(ColorVision.Solution.MenuOpenFolder), typeof(ColorVision.Solution.MenuOpenSolution),
            typeof(MenuSave), typeof(MenuSaveAs), typeof(MenuClose), typeof(ColorVision.MenuCommandSearch), typeof(ColorVision.MenuContextualFind)
        ];
        var declarations = ShellMenuTypes.Concat(fileAndSearchTypes)
            .Select(type => Assert.IsAssignableFrom<IHotKey>(Activator.CreateInstance(type)).HotKeys).ToArray();
        Hotkey[] defaults = declarations.SelectMany(declaration => declaration.GetDefaultBindings()).ToArray();

        Assert.Equal(14, declarations.Length);
        Assert.Equal(12, defaults.Length);
        Assert.Equal(defaults.Length, defaults.Distinct().Count());
        Assert.DoesNotContain(defaults, key => key.Modifiers == ModifierKeys.Control && key.Key is Key.I or Key.U or Key.L);
        Assert.All(declarations, declaration => Assert.Equal(HotKeyKinds.Windows, declaration.DefaultKinds));
        Assert.All(declarations, declaration => Assert.NotSame(declaration.Hotkey, declaration.DefaultHotkey));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ResetLayoutMenuAndShortcutRequireConfirmationBeforeTheSameResetAction(bool confirmed, bool useShortcut)
    {
        int confirmations = 0;
        int resets = 0;
        var source = new ColorVision.Solution.Workspace.MenuResetLayout(
            () => { confirmations++; return confirmed; }, () => resets++);
        ICommand menuCommand = Assert.IsAssignableFrom<ICommand>(source.Command);
        HotKeys shortcut = source.HotKeys;
        Assert.Equal(0, confirmations);
        Assert.Equal(0, resets);

        // Both routes use injected delegates, never a real layout manager or confirmation window.
        if (useShortcut) shortcut.HotKeyHandler!();
        else menuCommand.Execute(null);

        Assert.Equal(1, confirmations);
        Assert.Equal(confirmed ? 1 : 0, resets);
    }

    [Theory]
    [InlineData("zh-CN", "版本", "保存", "搜索命令与功能", "不查找文档正文")]
    [InlineData("en-US", "version", "save", "Search commands and features", "not document text")]
    public void NewDescriptionsExplainActionsAndTheirBoundaries(string culture, string aboutMarker, string resetMarker, string searchName, string searchBoundary)
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            Assert.Contains(aboutMarker, new ColorVision.AboutMsgExport().HotKeys.Description, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(resetMarker, new ColorVision.Solution.Workspace.MenuResetLayout().HotKeys.Description, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(resetMarker, BuiltInHotkeyDescriptions.ResetLayoutConfirmation, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(searchName, BuiltInHotkeyDescriptions.SearchCommandsName);
            Assert.Contains(searchBoundary, BuiltInHotkeyDescriptions.SearchCommands, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
