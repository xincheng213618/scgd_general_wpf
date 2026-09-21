using System.IO;
using System.Text.RegularExpressions;

namespace ColorVision.UI.Tests;

public sealed class WindowCaptionCoverageTests
{
    private static readonly Regex DirectWindowClass = new(
        @"class\s+(?<name>\w+)\s*:\s*(?:System\.Windows\.)?Window\b",
        RegexOptions.Compiled);

    private static readonly Regex PlainWindowCreation = new(
        @"(?:\bWindow\s+(?<name>\w+)\s*=\s*new(?:\s+(?:System\.Windows\.)?Window)?|\bvar\s+(?<name>\w+)\s*=\s*new\s+(?:System\.Windows\.)?Window\b)\s*(?:\(\s*\))?\s*(?:\{[^;]*\})?\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly string[] SourceRoots = ["ColorVision", "Engine", "Plugins", "Projects", "UI"];

    [Fact]
    public void OrdinaryWindowsApplyNativeCaptionTheme()
    {
        string repository = FindRepository();
        List<string> missing = [];

        foreach (string sourceRoot in SourceRoots)
        {
            string directory = Path.Combine(repository, sourceRoot);
            foreach (string sourcePath in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (sourcePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || sourcePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string source = File.ReadAllText(sourcePath);
                foreach (Match match in DirectWindowClass.Matches(source))
                {
                    string className = match.Groups["name"].Value;
                    if (className is "MainWindow" or "BaseWindow" || UsesCustomChromeForSource(sourcePath))
                        continue;

                    if (!source.Contains("ApplyCaption(", StringComparison.Ordinal))
                        missing.Add(Path.GetRelativePath(repository, sourcePath));
                }

                foreach (Match match in PlainWindowCreation.Matches(source))
                {
                    string variable = match.Groups["name"].Value;
                    string following = source.Substring(match.Index, Math.Min(5000, source.Length - match.Index));
                    if (!Regex.IsMatch(following, $@"(?:{Regex.Escape(variable)}\.ApplyCaption\s*\(|ThemeManagerExtensions\.ApplyCaption\s*\(\s*{Regex.Escape(variable)}\s*\))"))
                    {
                        missing.Add($"{Path.GetRelativePath(repository, sourcePath)} (new Window: {variable})");
                    }
                }
            }

            foreach (string xamlPath in Directory.EnumerateFiles(directory, "*.xaml", SearchOption.AllDirectories))
            {
                if (xamlPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || xamlPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string xaml = File.ReadAllText(xamlPath);
                Match root = Regex.Match(xaml, @"<Window(?=\s)(?<attributes>[\s\S]*?)>");
                if (!root.Success)
                    continue;

                string attributes = root.Groups["attributes"].Value;
                Match classMatch = Regex.Match(attributes, "x:Class\\s*=\\s*\"(?<class>[^\"]+)\"");
                string className = classMatch.Groups["class"].Value.Split('.').LastOrDefault() ?? string.Empty;
                if (className is "MainWindow" or "BaseWindow" || UsesCustomChrome(xaml))
                    continue;

                string sourcePath = $"{xamlPath}.cs";
                if (!File.Exists(sourcePath) || !File.ReadAllText(sourcePath).Contains("ApplyCaption(", StringComparison.Ordinal))
                    missing.Add(Path.GetRelativePath(repository, sourcePath));
            }
        }

        Assert.True(missing.Count == 0, "Ordinary windows missing ApplyCaption():" + Environment.NewLine + string.Join(Environment.NewLine, missing.Distinct().Order()));
    }

    private static bool UsesCustomChromeForSource(string sourcePath)
    {
        if (!sourcePath.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase))
            return false;

        string xamlPath = sourcePath[..^3];
        if (!File.Exists(xamlPath))
            return false;

        return UsesCustomChrome(File.ReadAllText(xamlPath));
    }

    private static bool UsesCustomChrome(string xaml)
    {
        Match root = Regex.Match(xaml, @"\A\s*<[\w:]+(?<attributes>[\s\S]*?)>");
        string attributes = root.Groups["attributes"].Value;
        return Regex.IsMatch(attributes, "WindowStyle\\s*=\\s*\"None\"|AllowsTransparency\\s*=\\s*\"True\"");
    }

    private static string FindRepository()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "build.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
