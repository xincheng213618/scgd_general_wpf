using ColorVision.Common.ThirdPartyApps;
using ColorVision.Wizards;

namespace ColorVision.UI.Tests
{
    public class RecommendedSoftwareWizardStepTests
    {
        [Fact]
        public async Task Refresh_SelectsMissingSoftwareByDefault()
        {
            FakeRecommendedSoftwareService service = new();
            RecommendedSoftwareWizardStep step = new(service);

            await step.RefreshAsync();

            Assert.False(step.IsRequired);
            Assert.True(step.RunsBeforeInitializers);
            Assert.False(step.ConfigurationStatus);
            Assert.All(step.Choices, choice =>
            {
                Assert.True(choice.IsEnabled);
                Assert.True(choice.IsSelected);
            });
        }

        [Fact]
        public async Task Apply_WithAllChoicesCleared_CompletesWithoutInstalling()
        {
            FakeRecommendedSoftwareService service = new();
            RecommendedSoftwareWizardStep step = new(service);
            await step.RefreshAsync();
            foreach (var choice in step.Choices)
                choice.IsSelected = false;

            bool result = await step.ApplyAsync();

            Assert.True(result);
            Assert.True(step.ConfigurationStatus);
            Assert.Empty(service.InstallCalls);
        }

        [Fact]
        public async Task Apply_InstallsSelectedSoftwareInOrder()
        {
            FakeRecommendedSoftwareService service = new();
            RecommendedSoftwareWizardStep step = new(service);
            await step.RefreshAsync();

            bool result = await step.ApplyAsync();

            Assert.True(result);
            Assert.True(step.ConfigurationStatus);
            Assert.Equal(new[] { "Everything", "WinRAR" }, service.InstallCalls);
            Assert.All(step.Choices, choice =>
            {
                Assert.False(choice.IsEnabled);
                Assert.False(choice.IsSelected);
            });
        }

        private sealed class FakeRecommendedSoftwareService : IRecommendedSoftwareService
        {
            private readonly List<ThirdPartyAppInfo> _apps =
            [
                new ThirdPartyAppInfo { Name = "Everything", Order = -900, InstallerPath = "Everything.exe" },
                new ThirdPartyAppInfo { Name = "WinRAR", Order = -899, InstallerPath = "WinRAR.exe" }
            ];

            public List<string> InstallCalls { get; } = new();

            public IReadOnlyList<ThirdPartyAppInfo> CreateApps() => _apps;

            public void RefreshStatus(ThirdPartyAppInfo app)
            {
            }

            public bool InstallerExists(ThirdPartyAppInfo app) => true;

            public Task InstallAsync(ThirdPartyAppInfo app, CancellationToken cancellationToken)
            {
                InstallCalls.Add(app.Name);
                app.IsInstalled = true;
                return Task.CompletedTask;
            }
        }
    }
}
