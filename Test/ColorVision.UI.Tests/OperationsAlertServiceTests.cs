using ColorVision.UI.Desktop.Operations;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsAlertServiceTests
    {
        [Fact]
        public void AlertsAreBoundedFilteredAndRedacted()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ColorVision.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                File.WriteAllLines(Path.Combine(directory, "20260713.txt"),
                [
                    "2026-07-13 10:00:00,000 [1] INFO  Host - ready",
                    $"2026-07-13 10:01:00,000 [1] WARN  Camera - retry file={profile}\\capture.raw token=visible",
                    "2026-07-13 10:02:00,000 [1] ERROR Broker - request failed?access_token=visible",
                ]);

                IReadOnlyList<OperationsAlert> alerts = new OperationsAlertService(directory).GetRecent(10);

                Assert.Equal(2, alerts.Count);
                Assert.DoesNotContain(alerts, item => item.Summary.Contains("visible", StringComparison.Ordinal));
                Assert.DoesNotContain(alerts, item => !string.IsNullOrWhiteSpace(profile)
                    && item.Summary.Contains(profile, StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(alerts, item => item.Summary.Contains("capture.raw", StringComparison.OrdinalIgnoreCase));
                Assert.Equal("error", alerts[0].Severity);
                Assert.Equal("warning", alerts[1].Severity);
                Assert.Equal("消息服务", alerts[0].Source);
                Assert.Equal("设备与图像", alerts[1].Source);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void DigestReturnsOnlyBoundedCategorizedAndRedactedEvents()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ColorVision.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllLines(Path.Combine(directory, "20260713.txt"),
                [
                    "2026-07-13 10:00:00,000 [1] INFO  Host - ready",
                    "2026-07-13 10:01:00,000 [1] WARN  LanRemoteControlService - endpoint 192.168.1.8:8788 http://host.local/api user@example.com",
                    "2026-07-13 10:02:00,000 [1] ERROR Broker - token=visible file=D:\\Customer Data\\capture.raw remaining text",
                    "2026-07-13 10:03:00,000 [1] FATAL OperationsSecureHostService - correlation 123e4567-e89b-12d3-a456-426614174000",
                ]);

                OperationsLogDigest digest = new OperationsAlertService(directory).GetDigest();

                Assert.True(digest.Available);
                Assert.Equal(4, digest.ScannedLineCount);
                Assert.Equal(4, digest.ParsedEventCount);
                Assert.Equal(1, digest.InfoCount);
                Assert.Equal(1, digest.WarningCount);
                Assert.Equal(1, digest.ErrorCount);
                Assert.Equal(1, digest.CriticalCount);
                Assert.False(digest.TailWasBounded);
                Assert.Equal(3, digest.RecentEvents.Count);
                Assert.Contains(digest.Categories, item => item.Category == "安全运维" && item.Count == 2);
                Assert.Contains(digest.Categories, item => item.Category == "消息服务" && item.Count == 1);

                string serialized = System.Text.Json.JsonSerializer.Serialize(digest);
                Assert.DoesNotContain("visible", serialized, StringComparison.Ordinal);
                Assert.DoesNotContain("192.168.1.8", serialized, StringComparison.Ordinal);
                Assert.DoesNotContain("host.local", serialized, StringComparison.Ordinal);
                Assert.DoesNotContain("user@example.com", serialized, StringComparison.Ordinal);
                Assert.DoesNotContain("D:\\Customer", serialized, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("capture.raw", serialized, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("123e4567-e89b-12d3-a456-426614174000", serialized, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("20260713.txt", serialized, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
