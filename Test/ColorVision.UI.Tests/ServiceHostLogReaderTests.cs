using ColorVision.ServiceHost;
using System;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests
{
    public sealed class ServiceHostLogReaderTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ColorVision-ServiceHostLogs-{Guid.NewGuid():N}");

        public ServiceHostLogReaderTests()
        {
            Directory.CreateDirectory(_directory);
        }

        [Fact]
        public void ReadsLatestLogContentWhileWriterKeepsFileOpen()
        {
            string path = Path.Combine(_directory, "ColorVisionServiceHost.log");
            using FileStream writer = new(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            using StreamWriter textWriter = new(writer, new UTF8Encoding(false), 1024, leaveOpen: true);
            for (int index = 0; index < 80; index++)
                textWriter.WriteLine($"entry-{index:000}");
            textWriter.Flush();

            ServiceHostLogSnapshot snapshot = ServiceHostLogReader.ReadTail(path, 160);

            Assert.True(snapshot.Exists);
            Assert.Empty(snapshot.Error);
            Assert.Contains("entry-079", snapshot.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("entry-000", snapshot.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void LatestFailedInstallationRemainsVisibleUntilACompletion()
        {
            string failed = "[1] Service host installation completed.\n[2] Service host installation failed: missing System.Management.dll";
            string recovered = failed + "\n[3] Service host installation completed.";

            Assert.Contains("System.Management.dll", ServiceHostLogReader.GetLatestInstallationFailure(failed), StringComparison.Ordinal);
            Assert.Empty(ServiceHostLogReader.GetLatestInstallationFailure(recovered));
        }

        [Fact]
        public void ReadsLegacyChineseInstallLogLinesWithoutGarbledText()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string path = Path.Combine(_directory, "install.log");
            File.WriteAllBytes(path, Encoding.GetEncoding(936).GetBytes("[2026/07/17 周五 6:15:28] Repair started.\r\n"));

            ServiceHostLogSnapshot snapshot = ServiceHostLogReader.ReadTail(path, 4096);

            Assert.Empty(snapshot.Error);
            Assert.Contains("周五", snapshot.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("�", snapshot.Text, StringComparison.Ordinal);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
