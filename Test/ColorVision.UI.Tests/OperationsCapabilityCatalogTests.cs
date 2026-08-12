using ColorVision.UI.Desktop.Operations;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsCapabilityCatalogTests
    {
        [Fact]
        public void Catalog_HasUniqueStableCapabilityIds()
        {
            var capabilities = OperationsCapabilityCatalog.GetAll();

            Assert.NotEmpty(capabilities);
            Assert.All(capabilities, capability =>
            {
                Assert.Equal(OperationsCapabilityCatalog.SchemaVersion, capability.SchemaVersion);
                Assert.StartsWith("ops.", capability.Id, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(capability.Permission));
                Assert.True(capability.Audit.Required);
            });
            Assert.Equal(capabilities.Count, capabilities.Select(capability => capability.Id).Distinct(StringComparer.Ordinal).Count());
            OperationsCapabilityDescriptor monitor = Assert.Single(capabilities, capability => capability.Id == "ops.monitor.read");
            Assert.True(monitor.Available);
            Assert.Equal(OperationsRiskLevels.ReadOnly, monitor.RiskLevel);
            Assert.Equal("ops.diagnostics.read", monitor.Permission);
        }

        [Fact]
        public void Catalog_OnlyExposesBoundedWindowActionsOrWorkflowWrites()
        {
            var capabilities = OperationsCapabilityCatalog.GetAll();

            var availableWrites = capabilities.Where(capability => capability.Available
                && capability.RiskLevel != OperationsRiskLevels.ReadOnly).ToList();
            Assert.All(availableWrites.Where(capability => capability.Category == "desktop-control"), capability =>
            {
                Assert.Contains(capability.Id, new[] { "ops.window.show", "ops.window.minimize" });
                Assert.Equal(OperationsRiskLevels.LowRisk, capability.RiskLevel);
                Assert.Equal("safe", capability.Idempotency);
                Assert.Equal("ops.window.control", capability.Permission);
            });
            Assert.All(availableWrites.Where(capability => capability.Category != "desktop-control"),
                capability => Assert.Contains(capability.Category, new[] { "jobs", "approvals", "deployment", "support", "maintenance", "diagnostics" }));

            var privileged = Assert.Single(capabilities, capability => capability.RiskLevel == OperationsRiskLevels.Privileged);
            Assert.True(privileged.Available);
            Assert.True(string.IsNullOrEmpty(privileged.BlockedReason));
            Assert.True(privileged.Approval.RequiresLocalCoSign);
            Assert.Equal("service-host", privileged.Execution.Target);
        }
    }
}
