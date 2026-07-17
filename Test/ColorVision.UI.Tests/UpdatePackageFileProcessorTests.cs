using ColorVision.Update;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class UpdatePackageFileProcessorTests
{
    [Fact]
    public void IncrementalPackagesAreNotRegisteredAsDirectInstallActions()
    {
        IEnumerable<string> extensions = typeof(PluginPackageFileProcessor).Assembly.GetTypes()
            .Where(type => typeof(IFileOpenActionProcessor).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => type.GetCustomAttribute<FileExtensionAttribute>())
            .OfType<FileExtensionAttribute>()
            .SelectMany(attribute => attribute.Extensions);

        Assert.DoesNotContain(extensions, extension => string.Equals(extension, ".cvx", StringComparison.OrdinalIgnoreCase));
    }
}
