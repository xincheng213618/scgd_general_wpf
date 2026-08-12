using ColorVision.UI.Desktop.Operations;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public class OperationsRuntimePerformanceServiceTests
    {
        [Fact]
        public void CpuCalculationIsNormalizedAndBounded()
        {
            Assert.Equal(25d, OperationsRuntimePerformanceService.CalculateCpuPercent(
                TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), processorCount: 2));
            Assert.Equal(100d, OperationsRuntimePerformanceService.CalculateCpuPercent(
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1), processorCount: 1));
            Assert.Equal(0d, OperationsRuntimePerformanceService.CalculateCpuPercent(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), processorCount: 1));
        }

        [Fact]
        public void CaptureReturnsOnlyAggregateBoundedCounters()
        {
            OperationsRuntimePerformanceService service = new(() => new OperationsUiResponsivenessSnapshot
            {
                Available = true,
                State = "responsive",
                LatencyMilliseconds = 12,
            });

            OperationsRuntimePerformanceSnapshot snapshot = service.Capture(10);

            Assert.InRange(snapshot.CpuPercent, 0, 100);
            Assert.True(snapshot.SampleMilliseconds >= 1);
            Assert.True(snapshot.WorkingSetMb > 0);
            Assert.True(snapshot.PrivateMemoryMb > 0);
            Assert.True(snapshot.ManagedHeapMb > 0);
            Assert.True(snapshot.ThreadCount > 0);
            Assert.True(snapshot.HandleCount > 0);
            Assert.Equal("responsive", snapshot.MainUi.State);
            string json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.UserName, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.ProcessPath ?? string.Empty, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.CurrentDirectory, json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
