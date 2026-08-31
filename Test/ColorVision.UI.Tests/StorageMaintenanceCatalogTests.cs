using ColorVision.Settings.Maintenance;
using ColorVision.UI.Maintenance;
using ColorVision.Update;
using log4net.Appender;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class StorageMaintenanceCatalogTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVisionMaintenanceCatalogTests", Guid.NewGuid().ToString("N"));
        private readonly List<FileAppender> _appenders = new();

        [Theory]
        [InlineData("20260730.txt", "20260831.txt", "yyyyMMdd'.txt'", false, false, true)]
        [InlineData("20260230.txt", "20260831.txt", "yyyyMMdd'.txt'", false, false, false)]
        [InlineData("notes.txt", "20260831.txt", "yyyyMMdd'.txt'", false, false, false)]
        [InlineData("20260831.txt", "20260831.txt", "yyyyMMdd'.txt'", false, false, false)]
        [InlineData("app.log.1", "app.log", ".yyyyMMdd", true, false, true)]
        [InlineData("app.log.old", "app.log", ".yyyyMMdd", true, false, false)]
        [InlineData("app.log.20260730", "app.log", ".yyyyMMdd", true, false, true)]
        [InlineData("app.2.log", "app.log", ".yyyyMMdd", true, true, true)]
        [InlineData("app-other.log.1", "app.log", ".yyyyMMdd", true, false, false)]
        public void LogArchivesMustMatchTheConfiguredRollingName(string candidate, string active, string pattern, bool isStatic, bool preserveExtension, bool expected)
        {
            Assert.Equal(expected, StorageMaintenanceCatalog.IsRollingArchiveName(candidate, active, pattern, isStatic, preserveExtension));
        }

        [Fact]
        public void RulesOnlyIncludeExistingOwnedRootsAndDoNotCreateAnything()
        {
            Directory.CreateDirectory(Path.Combine(_root, "temp"));
            Directory.CreateDirectory(Path.Combine(_root, "packages", "Application", "Full", "Recovery"));
            Directory.CreateDirectory(Path.Combine(_root, "packages", "Plugins"));
            Directory.CreateDirectory(Path.Combine(_root, "cache"));
            string updateDirectory = Path.Combine(_root, "temp", "ColorVisionUpdate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(updateDirectory);
            Directory.CreateDirectory(Path.Combine(_root, "temp", "ColorVisionUpdate-not-a-guid"));
            Directory.CreateDirectory(Path.Combine(_root, "temp", "Unrelated"));

            IReadOnlyList<MaintenanceFileCleanupRule> rules = CreateRules();

            Assert.Contains(rules, rule => rule.Id == "temp" && rule.RootPath == updateDirectory && rule.Recursive);
            Assert.DoesNotContain(rules, rule => rule.RootPath.EndsWith("not-a-guid", StringComparison.Ordinal));
            Assert.DoesNotContain(rules, rule => rule.RootPath.EndsWith("Unrelated", StringComparison.Ordinal));
            Assert.All(rules.Where(rule => rule.Id == "packages"), rule => Assert.False(rule.Recursive));
            Assert.DoesNotContain(rules, rule => rule.RootPath.EndsWith("Recovery", StringComparison.Ordinal));
            Assert.False(Directory.Exists(Path.Combine(_root, "packages", "Tools")));
            Assert.False(Directory.Exists(Path.Combine(_root, "packages", "Application", "Incremental")));
        }

        [Fact]
        public void RealDateRollingAppenderProtectsItsOpenFileAndIgnoresUnrelatedText()
        {
            string logDirectory = Path.Combine(_root, "log");
            Directory.CreateDirectory(logDirectory);
            var appender = new RollingFileAppender
            {
                File = logDirectory + Path.DirectorySeparatorChar,
                DatePattern = "yyyyMMdd'.txt'",
                StaticLogFileName = false,
                RollingStyle = RollingFileAppender.RollingMode.Date,
            };
            _appenders.Add(appender);
            appender.ActivateOptions();
            string oldLog = Path.Combine(logDirectory, DateTime.Today.AddDays(-60).ToString("yyyyMMdd") + ".txt");
            string unrelated = Path.Combine(logDirectory, "notes.txt");
            File.WriteAllText(oldLog, "old log");
            File.WriteAllText(unrelated, "not a log");
            File.SetLastWriteTimeUtc(oldLog, DateTime.UtcNow.AddDays(-60));
            File.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddDays(-60));
            var rules = StorageMaintenanceCatalog.CreateRulesForPaths(
                30, 7, 30, () => [appender], Path.Combine(_root, "temp"), Path.Combine(_root, "packages"), Path.Combine(_root, "cache"),
                () => false, () => false, _ => false);

            var scan = MaintenanceFileCleanup.Scan(rules, CancellationToken.None);

            Assert.Equal(oldLog, Assert.Single(scan.Files).FullPath);
            Assert.DoesNotContain(scan.Files, file => file.FullPath == appender.File);
        }

        [Fact]
        public void ActiveUpdateAndFeedbackGuardsAreReevaluatedAfterScan()
        {
            Directory.CreateDirectory(Path.Combine(_root, "temp"));
            string packageDirectory = Path.Combine(_root, "packages", "Application", "Full");
            Directory.CreateDirectory(packageDirectory);
            string package = Path.Combine(packageDirectory, "ColorVision-1.0.0.exe");
            string feedback = Path.Combine(_root, "temp", "ColorVision_Diagnostics_20260701_120000.zip");
            File.WriteAllText(package, "package");
            File.WriteAllText(feedback, "feedback");
            File.SetLastWriteTimeUtc(package, DateTime.UtcNow.AddDays(-60));
            File.SetLastWriteTimeUtc(feedback, DateTime.UtcNow.AddDays(-60));
            bool protectedNow = false;
            IReadOnlyList<MaintenanceFileCleanupRule> rules = CreateRules(() => protectedNow, () => protectedNow);
            var scan = MaintenanceFileCleanup.Scan(rules, CancellationToken.None);
            Assert.Equal(2, scan.Files.Count);

            protectedNow = true;
            MaintenanceFileCleanup.Cleanup(scan, CancellationToken.None);

            Assert.True(File.Exists(package));
            Assert.True(File.Exists(feedback));
        }

        [Fact]
        public void NewUpdateDirectoryKeepsOldTimestampFilesAndResumablePackagesStayProtected()
        {
            string updateDirectory = Path.Combine(_root, "temp", "ColorVisionUpdate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(updateDirectory);
            string copiedFile = Path.Combine(updateDirectory, "ColorVision.exe");
            File.WriteAllText(copiedFile, "copied old executable");
            File.SetLastWriteTimeUtc(copiedFile, DateTime.UtcNow.AddYears(-1));
            string packageDirectory = Path.Combine(_root, "packages", "Plugins");
            Directory.CreateDirectory(packageDirectory);
            string package = Path.Combine(packageDirectory, "plugin.cvxp");
            File.WriteAllText(package, "partial package");
            File.WriteAllText(package + ".aria2", "resume state");
            File.SetLastWriteTimeUtc(package, DateTime.UtcNow.AddYears(-1));

            var scan = MaintenanceFileCleanup.Scan(CreateRules(), CancellationToken.None);

            Assert.Empty(scan.Files);
        }

        [Fact]
        public void ReadOnlyHandoffProbeProtectsRunningUpdaterAndDoesNotDeleteStaleMarkers()
        {
            string stateDirectory = Path.Combine(_root, "update-state", "installation");
            Directory.CreateDirectory(stateDirectory);
            string marker = Path.Combine(stateDirectory, "update.pending");
            File.WriteAllLines(marker, ["token", _root, Environment.ProcessId.ToString()]);
            File.SetLastWriteTimeUtc(marker, DateTime.UtcNow.AddDays(-1));
            Assert.True(ExitUpdateHandoff.HasActiveUpdateForCleanup(Path.Combine(_root, "update-state")));

            File.WriteAllLines(marker, ["token", _root, "0"]);
            File.SetLastWriteTimeUtc(marker, DateTime.UtcNow.AddDays(-1));
            Assert.False(ExitUpdateHandoff.HasActiveUpdateForCleanup(Path.Combine(_root, "update-state")));
            Assert.True(File.Exists(marker));
        }

        private IReadOnlyList<MaintenanceFileCleanupRule> CreateRules(Func<bool>? updateProtected = null, Func<bool>? feedbackProtected = null)
        {
            return StorageMaintenanceCatalog.CreateRulesForPaths(
                30, 7, 30, () => Array.Empty<FileAppender>(), Path.Combine(_root, "temp"), Path.Combine(_root, "packages"), Path.Combine(_root, "cache"),
                updateProtected ?? (() => false), feedbackProtected ?? (() => false), _ => false);
        }

        public void Dispose()
        {
            foreach (FileAppender appender in _appenders)
                appender.Close();
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
