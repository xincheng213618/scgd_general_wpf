using Conoscope.Core;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Conoscope.Tests;

public class ArchitectureSmokeTests
{
    private static string RepoRoot => FindRepoRoot();

    [Fact]
    public void PluginVersionMatchesManifest()
    {
        string projectPath = Path.Combine(RepoRoot, "Plugins", "Conoscope", "Conoscope.csproj");
        string manifestPath = Path.Combine(RepoRoot, "Plugins", "Conoscope", "manifest.json");
        Match version = Regex.Match(File.ReadAllText(projectPath), @"<VersionPrefix>([^<]+)</VersionPrefix>");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));

        Assert.True(version.Success);
        Assert.Equal(version.Groups[1].Value, manifest.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void ApplicationCodeDoesNotShowMessageBoxes()
    {
        string applicationDirectory = Path.Combine(RepoRoot, "Plugins", "Conoscope", "Application");

        foreach (string file in Directory.GetFiles(applicationDirectory, "*.cs", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("MessageBox.Show", File.ReadAllText(file));
        }
    }

    [Fact]
    public void DefaultGaussianKernelCanBeRaisedByUser()
    {
        ConoscopeConfig config = new();

        Assert.Equal(ImageFilterType.Gaussian, config.FilterType);
        Assert.Equal(7, config.FilterKernelSize);

        config.FilterKernelSize = 55;
        Assert.Equal(55, config.FilterKernelSize);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Plugins", "Conoscope", "Conoscope.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the ColorVision repository root.");
    }
}
