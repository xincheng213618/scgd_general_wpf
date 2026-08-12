using ColorVision.UI.Desktop.Operations;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsFailureEvidenceTests
    {
        [Fact]
        public void FactoryKeepsOnlyFixedSevenDayAggregateEvidence()
        {
            DateTimeOffset now = new(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);

            OperationsFailureEvidenceSnapshot snapshot = OperationsFailureEvidenceSnapshotFactory.Create(
            [
                new(now.AddHours(-1), OperationsFailureKinds.ApplicationCrash),
                new(now.AddHours(-2), OperationsFailureKinds.ApplicationHang),
                new(now.AddHours(-3), OperationsFailureKinds.ManagedRuntimeFailure),
                new(now.AddHours(-4), OperationsFailureKinds.WindowsErrorReport),
                new(now.AddDays(-8), OperationsFailureKinds.ApplicationCrash),
                new(now.AddMinutes(1), OperationsFailureKinds.ApplicationCrash),
            ],
            [
                new(now.AddMinutes(-30)),
                new(now.AddDays(-9)),
            ],
            eventLogAvailable: true,
            dumpFolderAvailable: true,
            observedAt: now);

            Assert.True(snapshot.Available);
            Assert.True(snapshot.HasEvidence);
            Assert.Equal(4, snapshot.FailureEventCount);
            Assert.Equal(1, snapshot.CrashCount);
            Assert.Equal(1, snapshot.HangCount);
            Assert.Equal(1, snapshot.ManagedRuntimeFailureCount);
            Assert.Equal(1, snapshot.WindowsErrorReportCount);
            Assert.Equal(1, snapshot.DumpCount);
            Assert.Equal(now.AddMinutes(-30), snapshot.LatestEvidenceAt);
            Assert.Equal(now.AddDays(-7), snapshot.WindowStartedAt);
            Assert.Contains("excludes", snapshot.PrivacyNotice, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("event messages", snapshot.PrivacyNotice, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dump contents", snapshot.PrivacyNotice, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FactoryReportsPartialAvailabilityWithoutInventingEvidence()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            OperationsFailureEvidenceSnapshot snapshot = OperationsFailureEvidenceSnapshotFactory.Create(
                [], [], eventLogAvailable: false, dumpFolderAvailable: true, observedAt: now);

            Assert.True(snapshot.Available);
            Assert.False(snapshot.EventLogAvailable);
            Assert.True(snapshot.DumpFolderAvailable);
            Assert.False(snapshot.HasEvidence);
            Assert.Equal(0, snapshot.FailureEventCount);
            Assert.Equal(0, snapshot.DumpCount);
        }

    }
}
