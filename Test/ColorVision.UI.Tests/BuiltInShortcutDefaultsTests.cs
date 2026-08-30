using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Globalization;
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
    [InlineData(typeof(LogImp.MenuLogWindow))]
    [InlineData(typeof(ColorVision.Update.MenuCheckAndUpdateV1))]
    [InlineData(typeof(ColorVision.ExportMenuViewStatusBar))]
    [InlineData(typeof(ColorVision.AboutMsgExport))]
    [InlineData(typeof(ColorVision.Solution.Workspace.MenuResetLayout))]
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

    [Fact]
    public void ShellDefaultsAreUniqueAndDoNotReserveCommonEditingKeys()
    {
        var declarations = ShellMenuTypes.Select(type => Assert.IsAssignableFrom<IHotKey>(Activator.CreateInstance(type)).HotKeys).ToArray();
        Hotkey[] defaults = declarations.SelectMany(declaration => declaration.GetDefaultBindings()).ToArray();

        Assert.Equal(defaults.Length, defaults.Distinct().Count());
        Assert.DoesNotContain(defaults, key => key.Modifiers == ModifierKeys.Control && key.Key is Key.I or Key.U or Key.L);
        Assert.All(declarations, declaration => Assert.Equal(HotKeyKinds.Windows, declaration.DefaultKinds));
        Assert.All(declarations, declaration => Assert.NotSame(declaration.Hotkey, declaration.DefaultHotkey));
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
            Assert.Equal(searchName, BuiltInHotkeyDescriptions.SearchCommandsName);
            Assert.Contains(searchBoundary, BuiltInHotkeyDescriptions.SearchCommands, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
