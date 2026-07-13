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
        }

        [Fact]
        public void Catalog_OnlyExposesWorkflowWritesAndKeepsPrivilegedExecutionBlocked()
        {
            var capabilities = OperationsCapabilityCatalog.GetAll();

            Assert.All(capabilities.Where(capability => capability.Available
                    && capability.RiskLevel != OperationsRiskLevels.ReadOnly),
                capability => Assert.Contains(capability.Category, new[] { "jobs", "approvals", "deployment", "support", "maintenance", "diagnostics" }));

            var privileged = Assert.Single(capabilities, capability => capability.RiskLevel == OperationsRiskLevels.Privileged);
            Assert.True(privileged.Available);
            Assert.True(string.IsNullOrEmpty(privileged.BlockedReason));
            Assert.True(privileged.Approval.RequiresLocalCoSign);
            Assert.Equal("service-host", privileged.Execution.Target);
        }
    }
}
