using ColorVision.Database;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class MySqlBackupRestoreSafetyTests
{
    [Fact]
    public void MainBackupAndRestoreArgumentsUseUtf8Mb4AndKeepPasswordOutOfCommandLine()
    {
        string executablePath = Path.Combine(Path.GetTempPath(), $"mysql-test-{Guid.NewGuid():N}.exe");
        File.WriteAllBytes(executablePath, [0]);
        try
        {
            MySqlConfig config = new()
            {
                UserName = "operator name",
                UserPwd = "secret with spaces & symbols",
                Host = "127.0.0.1",
                Port = 3307,
                Database = "colorvision",
            };

            var startInfo = MySqlLocalServicesManager.CreateMySqlProcessStartInfo(executablePath, config, redirectStandardInput: true);
            string[] arguments = startInfo.ArgumentList.ToArray();

            Assert.False(startInfo.UseShellExecute);
            Assert.True(startInfo.RedirectStandardInput);
            Assert.True(startInfo.RedirectStandardOutput);
            Assert.True(startInfo.RedirectStandardError);
            Assert.All(arguments, argument => Assert.DoesNotContain(config.UserPwd, argument, StringComparison.Ordinal));
            Assert.DoesNotContain(arguments, argument => argument.StartsWith("--password", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(config.UserPwd, startInfo.Environment["MYSQL_PWD"]);
            Assert.Equal(["--user", config.UserName, "--host", config.Host, "--port", "3307", MySqlProtocolDefaults.DefaultCharacterSetArgument], arguments);
        }
        finally
        {
            File.Delete(executablePath);
        }
    }

    [Fact]
    public void SharedScriptRendererUsesUtf8Mb4BeforeBusinessSql()
    {
        const string dependencySql = "UPDATE sample SET `name` = '通用传感器';";

        string sql = MySqlProtocolDefaults.CreateScript("-- dependencies", dependencySql);

        int setNamesIndex = sql.IndexOf(MySqlProtocolDefaults.SetNamesStatement, StringComparison.Ordinal);
        int dependencyIndex = sql.IndexOf(dependencySql, StringComparison.Ordinal);
        Assert.Equal(0, setNamesIndex);
        Assert.True(dependencyIndex > setNamesIndex);
        Assert.Equal("utf8mb4", MySqlProtocolDefaults.CharacterSet);
    }

    [Fact]
    public void BackupAndRestoreExposeExactSuccessfulPath()
    {
        MethodInfo backup = typeof(MySqlLocalServicesManager).GetMethod(nameof(MySqlLocalServicesManager.BackupAllMysql))!;
        MethodInfo restore = typeof(MySqlLocalServicesManager).GetMethod(nameof(MySqlLocalServicesManager.RestoreMysql))!;

        Assert.Equal(typeof(string), backup.ReturnType);
        Assert.Equal(typeof(string), restore.ReturnType);
    }

    [Fact]
    public async Task DatabaseMaintenanceGateSerializesOperationsAndAllowsNestedWork()
    {
        using ManualResetEventSlim firstEntered = new(false);
        using ManualResetEventSlim releaseFirst = new(false);
        Task<int> first = Task.Run(() => MySqlLocalServicesManager.RunDatabaseMaintenance(() =>
        {
            firstEntered.Set();
            Assert.True(releaseFirst.Wait(TimeSpan.FromSeconds(5)));
            return MySqlLocalServicesManager.RunDatabaseMaintenance(() => 1);
        }));

        try
        {
            Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));
            Task<int> second = MySqlLocalServicesManager.RunDatabaseMaintenanceAsync(() => 2);
            await Task.Delay(100);
            Assert.False(second.IsCompleted);

            releaseFirst.Set();
            Assert.Equal(1, await first.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, await second.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(3, await MySqlLocalServicesManager.RunDatabaseMaintenanceAsync(
                () => MySqlLocalServicesManager.RunDatabaseMaintenance(() => 3)));
        }
        finally
        {
            releaseFirst.Set();
        }
    }

    [Fact]
    public void BothRestoreEntriesUseSharedSafeWorkflow()
    {
        string source = File.ReadAllText(FindManagerSourcePath());

        Assert.Contains("GetInstance().RestoreAndRestartAsync(FilePath)", source, StringComparison.Ordinal);
        Assert.Contains("RestoreAndRestartAsync(filePath)", source, StringComparison.Ordinal);
        Assert.Contains("RedirectStandardInput = redirectStandardInput", source, StringComparison.Ordinal);
        Assert.Contains("FileMode.CreateNew", source, StringComparison.Ordinal);
        Assert.Contains("File.Move(partFile, backupFile)", source, StringComparison.Ordinal);
        Assert.Contains("MySqlProtocolDefaults.AddCharacterSetArgument", source, StringComparison.Ordinal);
        Assert.Contains("MYSQL_PWD", source, StringComparison.Ordinal);
        Assert.Contains("return RunDatabaseMaintenance(() => CreateMySqlBackup", source, StringComparison.Ordinal);
        Assert.Contains("return RunDatabaseMaintenance(() => RestoreMysqlCore(backupFile))", source, StringComparison.Ordinal);
        Assert.Contains("AsyncLocal<int>", source, StringComparison.Ordinal);
        Assert.Contains("CopyToAsync", source, StringComparison.Ordinal);
        Assert.Contains("WaitForExitAsync", source, StringComparison.Ordinal);
        Assert.Contains("MySqlCommandTimeout", source, StringComparison.Ordinal);
        Assert.Contains("process.Kill(entireProcessTree: true)", source, StringComparison.Ordinal);
        Assert.Contains("数据库及服务已恢复，但应用自动重启失败", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteCommandAsAdmin", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteCommandUI", source, StringComparison.Ordinal);
        Assert.DoesNotContain("restoreCommand", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsServiceBackupAndRestoreUseTheSharedProtocolDefaults()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "Plugins",
            "WindowsServicePlugin",
            "ServiceManager",
            "MySqlServiceHelper.cs"));

        Assert.Contains("MySqlProtocolDefaults.AddCharacterSetArgument", source, StringComparison.Ordinal);
        Assert.Contains("psi.ArgumentList.Add($\"-p{password}\")", source, StringComparison.Ordinal);
        Assert.Contains("MySqlProtocolDefaults.CreateScript", source, StringComparison.Ordinal);
        Assert.Contains("MySqlProtocolDefaults.CharacterSet", source, StringComparison.Ordinal);
        Assert.Contains("bool restored = ExecuteSqlFile", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Tool.ExecuteCommandUI", source, StringComparison.Ordinal);
        Assert.DoesNotContain("--default-character-set=utf8mb4", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SET NAMES utf8mb4", source, StringComparison.Ordinal);
    }

    private static string FindManagerSourcePath([CallerFilePath] string testSourcePath = "")
    {
        return FindRepositoryFile(
            ["Engine", "ColorVision.Engine", "Mysql", "MySqlLocalServicesManager.cs"],
            testSourcePath);
    }

    private static string FindRepositoryFile(params string[] relativeParts)
    {
        return FindRepositoryFile(relativeParts, string.Empty);
    }

    private static string FindRepositoryFile(string[] relativeParts, [CallerFilePath] string testSourcePath = "")
    {
        string? testDirectory = Path.GetDirectoryName(testSourcePath);
        if (!string.IsNullOrWhiteSpace(testDirectory))
        {
            string sourceRelativeCandidate = Path.GetFullPath(Path.Combine(testDirectory, "..", "..", Path.Combine(relativeParts)));
            if (File.Exists(sourceRelativeCandidate))
                return sourceRelativeCandidate;
        }

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
