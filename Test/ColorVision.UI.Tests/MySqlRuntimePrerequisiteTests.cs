using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

public class MySqlRuntimePrerequisiteTests
{
    [Theory]
    [InlineData("5.7.37.0", false, true)]
    [InlineData("5.7.37.0", true, false)]
    [InlineData("5.7.38.0", false, true)]
    [InlineData("5.7.39.0", false, true)]
    [InlineData("5.7.40.0", false, false)]
    [InlineData("5.7.44.0", false, false)]
    [InlineData("8.0.40.0", false, false)]
    [InlineData("8.4.0.0", false, false)]
    public void Validation_OnlyRequiresVc2013ForAffectedVersions(string versionText, bool installed, bool shouldBlock)
    {
        string? message = MySqlRuntimePrerequisite.GetValidationMessage(Version.Parse(versionText), installed);

        Assert.Equal(shouldBlock, message != null);
        if (shouldBlock)
        {
            Assert.Contains("2013", message);
            Assert.Contains("x64", message);
            Assert.Contains(versionText, message);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownVersion_IsNotTreatedAsAValidatedMySql8(bool installed)
    {
        Assert.False(string.IsNullOrWhiteSpace(MySqlRuntimePrerequisite.GetValidationMessage(null, installed)));
    }

    [Fact]
    public void ReadVersion_ReadsExistingExecutableWithoutRunningIt()
    {
        string assemblyPath = typeof(MySqlRuntimePrerequisite).Assembly.Location;
        Version expected = Version.Parse(FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion!);

        Assert.Equal(expected, MySqlRuntimePrerequisite.ReadVersion(assemblyPath));
    }

    [Fact]
    public void ReadVersion_UsesPackagedExecutableEvenWhenArchiveNameClaimsAnotherVersion()
    {
        string assemblyPath = typeof(MySqlRuntimePrerequisite).Assembly.Location;
        Version expected = Version.Parse(FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion!);
        string packagePath = Path.Combine(Path.GetTempPath(), $"mysql-5.7.37-winx64-{Guid.NewGuid():N}.zip");
        try
        {
            using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                archive.CreateEntryFromFile(assemblyPath, "mysql-5.7.37-winx64/bin/mysqld.exe");

            Assert.Equal(expected, MySqlRuntimePrerequisite.ReadVersion(packagePath));
        }
        finally
        {
            File.Delete(packagePath);
        }
    }

    [Fact]
    public void ReadVersion_ZipWithoutServerDoesNotTrustPackageName()
    {
        string packagePath = Path.Combine(Path.GetTempPath(), $"mysql-8.0.40-winx64-{Guid.NewGuid():N}.zip");
        try
        {
            using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                archive.CreateEntry("mysql-8.0.40-winx64/README");

            Assert.Null(MySqlRuntimePrerequisite.ReadVersion(packagePath));
        }
        finally
        {
            File.Delete(packagePath);
        }
    }

    [Fact]
    public void ReadVersion_CorruptZipProducesValidationFailure()
    {
        string packagePath = Path.Combine(Path.GetTempPath(), $"mysql-5.7.37-{Guid.NewGuid():N}.zip");
        try
        {
            File.WriteAllText(packagePath, "not a ZIP archive");
            Version? version = MySqlRuntimePrerequisite.ReadVersion(packagePath);
            Assert.Null(version);
            Assert.NotNull(MySqlRuntimePrerequisite.GetValidationMessage(version, vc2013Installed: false));
        }
        finally
        {
            File.Delete(packagePath);
        }
    }
}
