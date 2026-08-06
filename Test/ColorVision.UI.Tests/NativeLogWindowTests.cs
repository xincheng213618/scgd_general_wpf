#pragma warning disable CA1707
using ColorVision.NativeLogging;
using ColorVision.UI.Menus;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;

namespace ColorVision.UI.Tests;

public sealed class NativeLogWindowTests
{
    [Fact]
    public void Menu_IsDiscoverableUnderHelp()
    {
        MenuNativeLog menu = new();

        Assert.Equal(MenuItemConstants.Help, menu.OwnerGuid);
        Assert.Equal(MenuItemConstants.GlobalTarget, menu.TargetName);
        Assert.Equal(10006, menu.Order);
        Assert.False(string.IsNullOrWhiteSpace(menu.Header));
        Assert.NotNull(menu.Command);
    }

    [Fact]
    public void Window_CompiledXamlIsEmbedded()
    {
        Assert.True(ContainsCompiledXaml(
            typeof(NativeLogWindow).Assembly,
            "nativelogging/nativelogwindow.baml"));
    }

    private static bool ContainsCompiledXaml(Assembly assembly, string resourceKey)
    {
        foreach (string resourceName in assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".g.resources", StringComparison.Ordinal)))
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                continue;
            }

            using ResourceReader reader = new(stream);
            foreach (DictionaryEntry entry in reader)
            {
                if (string.Equals(entry.Key as string, resourceKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
