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
                Assert.Equal("error", alerts[0].Severity);
                Assert.Equal("warning", alerts[1].Severity);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
