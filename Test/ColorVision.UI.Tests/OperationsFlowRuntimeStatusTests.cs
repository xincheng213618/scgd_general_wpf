using ColorVision.UI.Desktop.Operations;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsFlowRuntimeStatusTests
    {
        [Fact]
        public void FactoryDistinguishesPreparingRunningFinalizingAndIdle()
        {
            DateTimeOffset now = new(2026, 8, 12, 8, 0, 0, TimeSpan.Zero);
            OperationsFlowRuntimeSourceSnapshot source = new()
            {
                Available = true,
                HasConfiguredFlow = true,
                LifecycleActive = true,
                BatchIsCurrentLifecycle = true,
                ProgressAvailable = true,
                BatchCreatedAt = now.AddSeconds(-12),
                ProgressPercent = 42.34,
            };

            OperationsFlowRuntimeStatus preparing = OperationsFlowRuntimeStatusFactory.Create(source, now);
            Assert.Equal("preparing", preparing.Phase);
            Assert.Equal(0d, preparing.ProgressPercent);
            Assert.Equal(12000, preparing.ElapsedMilliseconds);

            OperationsFlowRuntimeStatus preparingWithPreviousBatch = OperationsFlowRuntimeStatusFactory.Create(new OperationsFlowRuntimeSourceSnapshot
            {
                Available = true,
                LifecycleActive = true,
                BatchStatus = "Completed",
                BatchCreatedAt = now.AddHours(-2),
                BatchDurationMilliseconds = 950,
            }, now);
            Assert.Equal("preparing", preparingWithPreviousBatch.Phase);
            Assert.Null(preparingWithPreviousBatch.ElapsedMilliseconds);
            Assert.Equal("completed", preparingWithPreviousBatch.LastRunStatus);

            OperationsFlowRuntimeStatus running = OperationsFlowRuntimeStatusFactory.Create(new OperationsFlowRuntimeSourceSnapshot
            {
                Available = true,
                HasConfiguredFlow = true,
                LifecycleActive = true,
                EngineRunning = true,
                BatchIsCurrentLifecycle = true,
                ProgressAvailable = true,
                BatchCreatedAt = now.AddSeconds(-12),
                ProgressPercent = 42.34,
            }, now);
            Assert.Equal("running", running.Phase);
            Assert.Equal(42.3, running.ProgressPercent);
            Assert.True(running.ProgressIsHistoricalEstimate);

            OperationsFlowRuntimeStatus finalizing = OperationsFlowRuntimeStatusFactory.Create(new OperationsFlowRuntimeSourceSnapshot
            {
                Available = true,
                LifecycleActive = true,
                BatchIsCurrentLifecycle = true,
                ProgressAvailable = true,
                BatchStatus = "Completed",
            }, now);
            Assert.Equal("finalizing", finalizing.Phase);
            Assert.Equal(100d, finalizing.ProgressPercent);

            OperationsFlowRuntimeStatus idle = OperationsFlowRuntimeStatusFactory.Create(new OperationsFlowRuntimeSourceSnapshot
            {
                Available = true,
                BatchStatus = "Failed",
                BatchDurationMilliseconds = 1450,
            }, now);
            Assert.Equal("idle", idle.Phase);
            Assert.Equal("failed", idle.LastRunStatus);
            Assert.Equal(1450, idle.LastRunDurationMilliseconds);
            Assert.Null(idle.ProgressPercent);
        }

        [Fact]
        public void PublicStatusContainsNoFlowIdentityOrInspectionData()
        {
            OperationsFlowRuntimeStatus status = OperationsFlowRuntimeStatusFactory.Create(new OperationsFlowRuntimeSourceSnapshot
            {
                Available = true,
                HasConfiguredFlow = true,
                BatchStatus = "Completed",
                BatchDurationMilliseconds = 800,
            });

            string json = JsonSerializer.Serialize(status, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            Assert.False(root.TryGetProperty("flowName", out _));
            Assert.False(root.TryGetProperty("templateId", out _));
            Assert.False(root.TryGetProperty("batchSerialNumber", out _));
            Assert.False(root.TryGetProperty("nodeName", out _));
            Assert.False(root.TryGetProperty("parameters", out _));
            Assert.False(root.TryGetProperty("resultText", out _));
            Assert.DoesNotContain(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.UserName, json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
