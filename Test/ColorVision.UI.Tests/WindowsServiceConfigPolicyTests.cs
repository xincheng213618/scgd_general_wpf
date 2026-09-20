using System.Reflection;
using System.IO;
using ColorVision.Database;
using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

public sealed class WindowsServiceConfigPolicyTests
{
    private static readonly MethodInfo BuildMonitorServices = typeof(ServiceManagerViewModel).GetMethod(
        "BuildRegistrationCenterMonitorServices",
        BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Monitor service policy was not found.");

    [Theory]
    [InlineData(false, "MySQL,CVMainService_x64,CVMainService_dev")]
    [InlineData(true, "MySQL,CVMainService_x64,CVMainService_dev,CVArchService")]
    public void MonitorListIncludesArchiveServiceOnlyWhenInstalled(bool archiveServiceInstalled, string expected)
    {
        string actual = Assert.IsType<string>(BuildMonitorServices.Invoke(null, [archiveServiceInstalled]));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BusinessConnectionSyncSelectsAndReusesCvPathWithoutOverwritingRoot()
    {
        MethodInfo synchronizeBusinessConnection = typeof(MySqlServiceManager).GetMethod(
            "SynchronizeBusinessConnection",
            BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Business connection synchronization was not found.");
        MySqlConfig root = new()
        {
            Name = MySqlServiceConfig.RootProfileName,
            UserName = "root",
            UserPwd = "root-secret",
            Database = "old_database"
        };
        MySqlSetting setting = new()
        {
            MySqlConfig = root,
            MySqlConfigs = [root]
        };
        MySqlServiceConfig serviceConfig = new()
        {
            Host = "127.0.0.1",
            Port = 3307,
            AppUser = "cv",
            AppPassword = "business-secret",
            Database = "color_vision_4xx"
        };

        synchronizeBusinessConnection.Invoke(null, [setting, serviceConfig, serviceConfig.Database]);
        synchronizeBusinessConnection.Invoke(null, [setting, serviceConfig, serviceConfig.Database]);

        Assert.Equal(MySqlServiceConfig.BusinessProfileName, setting.MySqlConfig.Name);
        Assert.Equal("127.0.0.1", setting.MySqlConfig.Host);
        Assert.Equal(3307, setting.MySqlConfig.Port);
        Assert.Equal("cv", setting.MySqlConfig.UserName);
        Assert.Equal("business-secret", setting.MySqlConfig.UserPwd);
        Assert.Equal("color_vision_4xx", setting.MySqlConfig.Database);
        Assert.Single(setting.MySqlConfigs, item => item.Name == MySqlServiceConfig.BusinessProfileName);
        Assert.Equal("root", root.UserName);
        Assert.Equal("root-secret", root.UserPwd);
    }

    [Fact]
    public void SuccessfulInstallShowsLocalizedCompletionDialog()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "Plugins",
            "WindowsServicePlugin",
            "ServiceManager",
            "ServiceInstallViewModel.Install.cs"));

        Assert.Contains("Properties.Resources.InstallCompletedMessage", source, StringComparison.Ordinal);
        Assert.Contains("Properties.Resources.InstallCompletedTitle", source, StringComparison.Ordinal);
        Assert.Contains("MessageBoxImage.Information", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] relativeParts)
    {
        foreach (string seed in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(seed);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, Path.Combine(relativeParts));
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Unable to locate {Path.Combine(relativeParts)} from the test working directory.");
    }
}
