using Conoscope.Core;
using System.IO;

namespace Conoscope.Tests;

public class ArchitectureSmokeTests
{
    private static string RepoRoot => FindRepoRoot();

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
    public void ViewAndWindowAreNotMechanicallySplitAcrossTopLevelPartialFiles()
    {
        string pluginDirectory = Path.Combine(RepoRoot, "Plugins", "Conoscope");
        string[] allowedCodeBehindFiles = ["ConoscopeView.xaml.cs", "ConoscopeWindow.xaml.cs"];
        string[] partialFragments = Directory
            .GetFiles(pluginDirectory, "Conoscope*.cs", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                string fileName = Path.GetFileName(path);
                bool isViewOrWindowFragment = fileName.StartsWith("ConoscopeView.", StringComparison.Ordinal)
                    || fileName.StartsWith("ConoscopeWindow.", StringComparison.Ordinal);
                return isViewOrWindowFragment && !allowedCodeBehindFiles.Contains(fileName, StringComparer.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToArray()!;

        Assert.Empty(partialFragments);
    }

    [Fact]
    public void DocumentOwnerHasNoWpfUiDependency()
    {
        string documentPath = Path.Combine(RepoRoot, "Plugins", "Conoscope", "ConoscopeDocument.cs");
        string source = File.ReadAllText(documentPath);

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBox", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewDoesNotOwnPoiDatabaseOrLegacyImageViewControlAccess()
    {
        string pluginDirectory = Path.Combine(RepoRoot, "Plugins", "Conoscope");
        string viewSource = File.ReadAllText(Path.Combine(pluginDirectory, "ConoscopeView.xaml.cs"));
        string hostSource = File.ReadAllText(Path.Combine(pluginDirectory, "ConoscopeImageHost.xaml.cs"));

        Assert.DoesNotContain("MySqlControl", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlSugar", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PoiMasterDao", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public DrawCanvas ImageShow", hostSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public Zoombox Zoombox1", hostSource, StringComparison.Ordinal);
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
