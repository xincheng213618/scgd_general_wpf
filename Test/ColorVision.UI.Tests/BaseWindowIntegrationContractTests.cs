using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ColorVision.UI.Tests;

public sealed class BaseWindowIntegrationContractTests
{
    [Fact]
    public void BaseWindowUsesTheWindows11Build22000Boundary()
    {
        string source = ReadRepositoryText("UI/ColorVision.Themes/Themes/Window/BaseWindow.cs");

        Assert.Contains("OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Version(10, 0, 21996)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BaseWindowUnsubscribesFromTheThemeManagerThatPublishedTheSubscription()
    {
        string source = ReadRepositoryText("UI/ColorVision.Themes/Themes/Window/BaseWindow.cs");

        Assert.Contains("_subscribedThemeManager = ThemeManager.Current;", source, StringComparison.Ordinal);
        Assert.Contains("_subscribedThemeManager.CurrentUIThemeChanged += _themeChangedHandler;", source, StringComparison.Ordinal);
        Assert.Contains("_subscribedThemeManager.CurrentUIThemeChanged -= _themeChangedHandler;", source, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.BeginInvoke(new Action(ApplyBlurTheme));", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutVersionDisplayDoesNotTakeInitialKeyboardFocus()
    {
        string xaml = ReadRepositoryText("ColorVision/AboutMsg.xaml");
        string codeBehind = ReadRepositoryText("ColorVision/AboutMsg.xaml.cs");
        Match versionButton = Regex.Match(xaml, @"<Button\s+x:Name=""CloseButton""[^>]*>");

        Assert.True(versionButton.Success);
        Assert.Contains("Focusable=\"False\"", versionButton.Value, StringComparison.Ordinal);
        Assert.Contains("IsTabStop=\"False\"", versionButton.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("CloseButton.Focus()", codeBehind, StringComparison.Ordinal);
    }

    private static string ReadRepositoryText(string relativePath)
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            string candidate = Path.Combine(current, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            current = Directory.GetParent(current)?.FullName;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }
}
