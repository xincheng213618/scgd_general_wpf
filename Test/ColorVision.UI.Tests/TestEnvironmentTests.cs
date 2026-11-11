using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Messages;
using ColorVision.Scheduler.Data;
using ColorVision.SocketProtocol;
using ColorVision.UI.Desktop.TimedButtons;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class TestEnvironmentTests
{
    [Fact]
    public void ConfigReplacementKeepsProcessDiagnosticsInTemporaryStorage()
    {
        IConfigService original = ConfigService.Instance;
        string originalFlowDb = original.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath;
        string originalMessageDb = original.GetRequiredService<MsgRecordManagerConfig>().SqliteDbPath;
        AssertTemporaryPath(originalFlowDb);
        AssertTemporaryPath(originalMessageDb);
        AssertTemporaryPath(ConfigHandler.GetInstance().ConfigFilePath);
        AssertTemporaryPath(SocketMessageManager.SqliteDbPath);
        AssertTemporaryPath(SchedulerDbManager.DbPath);
        AssertTemporaryPath(TimedButtonOperationStatsSqliteRepository.DbPath);

        DirectoryInfo fixture = Directory.CreateTempSubdirectory("ColorVision-ConfigOverride-");
        try
        {
            var replacement = new ConfigHandler { IsAutoSave = false };
            TestEnvironment.SetConfig(replacement);
            Assert.Same(replacement, ConfigService.Instance);
            AssertTemporaryPath(replacement.ConfigFilePath);
            Assert.Equal(originalFlowDb, replacement.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath);
            Assert.Equal(originalMessageDb, replacement.GetRequiredService<MsgRecordManagerConfig>().SqliteDbPath);

            string explicitPath = Path.Combine(fixture.FullName, "fixture.json");
            replacement.ConfigFilePath = explicitPath;
            replacement.GetRequiredService<FlowNodeRecordConfig>().DirectoryPath = fixture.FullName;
            TestEnvironment.SetConfig(replacement);
            Assert.Equal(explicitPath, replacement.ConfigFilePath);
            Assert.Equal(Path.Combine(fixture.FullName, "FlowNodeRecords.db"), replacement.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath);
        }
        finally
        {
            TestEnvironment.SetConfig(original);
            fixture.Delete();
        }
    }

    private static void AssertTemporaryPath(string path)
    {
        string temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(temp, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
    }
}
