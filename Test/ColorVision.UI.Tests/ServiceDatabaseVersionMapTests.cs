using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

public class ServiceDatabaseVersionMapTests
{
    [Theory]
    [InlineData("3.6.5.1202", ServiceDatabaseVersionMap.LegacyDatabaseName)]
    [InlineData("3.9.9.9", ServiceDatabaseVersionMap.LegacyDatabaseName)]
    [InlineData("4.0.0.0", ServiceDatabaseVersionMap.Version4DatabaseName)]
    [InlineData("4.0.3.227", ServiceDatabaseVersionMap.Version4DatabaseName)]
    [InlineData("5.0.0.0", ServiceDatabaseVersionMap.Version4DatabaseName)]
    public void GetDatabaseName_UsesMajorVersionBoundary(string versionText, string expectedDatabase)
    {
        Assert.Equal(expectedDatabase, ServiceDatabaseVersionMap.GetDatabaseName(Version.Parse(versionText)));
    }

    [Fact]
    public void GetDatabaseName_ProvidesDifferentSourceAndTargetForBothMigrationDirections()
    {
        Version legacyVersion = Version.Parse("3.6.5.1202");
        Version version4 = Version.Parse("4.1.1.709");

        Assert.Equal(ServiceDatabaseVersionMap.LegacyDatabaseName, ServiceDatabaseVersionMap.GetDatabaseName(legacyVersion));
        Assert.Equal(ServiceDatabaseVersionMap.Version4DatabaseName, ServiceDatabaseVersionMap.GetDatabaseName(version4));

        Assert.Equal(
            (ServiceDatabaseVersionMap.LegacyDatabaseName, ServiceDatabaseVersionMap.Version4DatabaseName),
            (ServiceDatabaseVersionMap.GetDatabaseName(legacyVersion), ServiceDatabaseVersionMap.GetDatabaseName(version4)));
        Assert.Equal(
            (ServiceDatabaseVersionMap.Version4DatabaseName, ServiceDatabaseVersionMap.LegacyDatabaseName),
            (ServiceDatabaseVersionMap.GetDatabaseName(version4), ServiceDatabaseVersionMap.GetDatabaseName(legacyVersion)));
    }

    [Theory]
    [InlineData(@"H:\ColorVision\Tool\CVWindowsService\CVWindowsService[3.6.5.1202].zip", "3.6.5.1202")]
    [InlineData(@"H:\ColorVision\Tool\CVWindowsService\CVWindowsService[4.1.1.709].zip", "4.1.1.709")]
    public void TryParsePackageVersion_ReadsVersionedPackageName(string packagePath, string expectedVersion)
    {
        Assert.True(ServiceDatabaseVersionMap.TryParsePackageVersion(packagePath, out Version version));
        Assert.Equal(Version.Parse(expectedVersion), version);
    }

    [Fact]
    public void TryGetPackageVersion_PrefersEmbeddedServiceExecutableVersion()
    {
        string assemblyPath = typeof(ServiceDatabaseVersionMap).Assembly.Location;
        string fileVersion = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion!;
        string packagePath = Path.Combine(Path.GetTempPath(), $"CVWindowsService[9.9.9.9]-{Guid.NewGuid():N}.zip");

        try
        {
            using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("RegWindowsService/RegWindowsService.exe");
                using Stream input = File.OpenRead(assemblyPath);
                using Stream output = entry.Open();
                input.CopyTo(output);
            }

            Assert.True(ServicePackageVersionResolver.TryGetPackageVersion(packagePath, out Version version));
            Assert.Equal(Version.Parse(fileVersion), version);
        }
        finally
        {
            File.Delete(packagePath);
        }
    }

    [Theory]
    [InlineData(null, null, ServiceDatabaseVersionMap.Version4DatabaseName)]
    [InlineData(null, "custom_database", "custom_database")]
    [InlineData("3.6.5.1202", "custom_database", ServiceDatabaseVersionMap.LegacyDatabaseName)]
    [InlineData("4.1.1.709", "custom_database", ServiceDatabaseVersionMap.Version4DatabaseName)]
    public void ResolveDatabaseName_UsesVersionBeforeConfiguredFallback(
        string? versionText,
        string? configuredDatabase,
        string expectedDatabase)
    {
        Version? version = versionText == null ? null : Version.Parse(versionText);

        Assert.Equal(expectedDatabase, ServiceDatabaseVersionMap.ResolveDatabaseName(version, configuredDatabase));
    }
}
