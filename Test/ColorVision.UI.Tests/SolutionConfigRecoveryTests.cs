using ColorVision.Solution.Explorer;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class SolutionConfigRecoveryTests
{
    [Fact]
    public void TemporarilyUnreadablePrimaryIsNeverReplacedByItsBackup()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-SolutionRecovery-");
        string path = Path.Combine(directory.FullName, "Project.cvsln");
        try
        {
            SolutionConfigStore.Save(path, new SolutionConfig { StartupProject = "old.cvproj" });
            SolutionConfigStore.Save(path, new SolutionConfig { StartupProject = "current.cvproj" });
            byte[] current = File.ReadAllBytes(path);
            string backupPath = SolutionConfigStore.GetBackupPath(path);
            byte[] backup = File.ReadAllBytes(backupPath);
            Exception? error;
            // This sharing mode blocks another reader but permits an atomic path replacement.
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Delete))
                error = Record.Exception(() => SolutionConfigStore.Load(path));

            Assert.Equal(current, File.ReadAllBytes(path));
            Assert.IsAssignableFrom<IOException>(error);
            Assert.Equal(backup, File.ReadAllBytes(backupPath));
            Assert.Equal(2, directory.EnumerateFiles().Count());
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoveryPreservesTheBackupAndArchivesOnlyAnObservedCorruptFile(bool corruptPrimary)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-SolutionRecovery-");
        string path = Path.Combine(directory.FullName, "Project.cvsln");
        string backupPath = SolutionConfigStore.GetBackupPath(path);
        try
        {
            SolutionConfigStore.Save(backupPath, new SolutionConfig { StartupProject = "backup.cvproj" });
            byte[] backup = File.ReadAllBytes(backupPath);
            byte[]? corrupt = null;
            if (corruptPrimary)
            {
                File.WriteAllText(path, "{broken 配置", Encoding.Unicode);
                corrupt = File.ReadAllBytes(path);
            }

            SolutionConfigLoadResult recovered = SolutionConfigStore.Load(path);
            Assert.True(recovered.RecoveredFromBackup);
            Assert.Equal("backup.cvproj", recovered.Config.StartupProject);
            Assert.Equal(backup, File.ReadAllBytes(backupPath));
            Assert.Equal(recovered.DiskContent, File.ReadAllText(path));
            if (corrupt != null)
                Assert.Equal(corrupt, File.ReadAllBytes(Assert.IsType<string>(recovered.CorruptCopyPath)));
            else
                Assert.Null(recovered.CorruptCopyPath);
            Assert.Equal(corruptPrimary ? 3 : 2, directory.EnumerateFiles().Count());
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{broken")]
    public void RecoveryUsesAValidPrimaryThatArrivedAfterTheFailedRead(string? observedPrimary)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-SolutionRecovery-");
        string path = Path.Combine(directory.FullName, "Project.cvsln");
        try
        {
            SolutionConfigStore.Save(path, new SolutionConfig { StartupProject = "backup.cvproj" });
            SolutionConfigStore.Save(path, new SolutionConfig { StartupProject = "new.cvproj" });
            byte[] newPrimary = File.ReadAllBytes(path);
            Exception observedError = observedPrimary == null ? new FileNotFoundException() : new InvalidDataException();

            SolutionConfigLoadResult result = SolutionConfigStore.RecoverFromBackup(path, observedPrimary, observedError);

            Assert.False(result.RecoveredFromBackup);
            Assert.Equal("new.cvproj", result.Config.StartupProject);
            Assert.Equal(newPrimary, File.ReadAllBytes(path));
            Assert.Equal("backup.cvproj", SolutionConfigStore.Load(SolutionConfigStore.GetBackupPath(path)).Config.StartupProject);
            Assert.Equal(2, directory.EnumerateFiles().Count());
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
