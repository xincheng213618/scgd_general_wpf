using ColorVision.UI;
using ColorVision.UI.Configs;
using System.Reflection;
using SystemMonitor;

namespace ColorVision.UI.Tests
{
    public class SystemMonitorLifecycleTests
    {
        private static readonly MethodInfo ShouldRunPeriodicUpdates =
            typeof(SystemMonitors).GetMethod(
                "ShouldRunPeriodicUpdates",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(SystemMonitors).FullName, "ShouldRunPeriodicUpdates");
        private static readonly ConstructorInfo StatusBarProviderConstructor =
            typeof(SystemMonitorIStatusBarProvider).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                [typeof(Func<SystemMonitorSetting>), typeof(Func<ISystemMonitorStatusSource>), typeof(IConfigReloadNotifier)],
                modifiers: null)
            ?? throw new MissingMethodException(typeof(SystemMonitorIStatusBarProvider).FullName, ".ctor");

        [Fact]
        public void HiddenStatusItems_DoNotRequirePeriodicUpdates()
        {
            var config = new SystemMonitorSetting();

            Assert.False(InvokeShouldRunPeriodicUpdates(false, config));
        }

        [Theory]
        [InlineData(nameof(SystemMonitorSetting.IsShowTime))]
        [InlineData(nameof(SystemMonitorSetting.IsShowRAM))]
        [InlineData(nameof(SystemMonitorSetting.IsShowCPU))]
        [InlineData(nameof(SystemMonitorSetting.IsShowUptime))]
        public void VisibleDynamicStatusItem_RequiresPeriodicUpdates(string propertyName)
        {
            var config = new SystemMonitorSetting();
            typeof(SystemMonitorSetting).GetProperty(propertyName)!.SetValue(config, true);

            Assert.True(InvokeShouldRunPeriodicUpdates(false, config));
        }

        [Fact]
        public void VisibleDiskStatusAlone_DoesNotRequirePeriodicUpdates()
        {
            var config = new SystemMonitorSetting { IsShowDisk = true };

            Assert.False(InvokeShouldRunPeriodicUpdates(false, config));
        }

        [Fact]
        public void ActiveDetailView_RequiresPeriodicUpdates()
        {
            var config = new SystemMonitorSetting();

            Assert.True(InvokeShouldRunPeriodicUpdates(true, config));
        }

        [Fact]
        public void HiddenStatusItems_DoNotMaterializeSystemMonitor()
        {
            var config = new SystemMonitorSetting();
            int materializationCount = 0;
            var provider = CreateStatusBarProvider(() => config, () =>
            {
                materializationCount++;
                throw new InvalidOperationException("The hidden status bar must not initialize system monitoring.");
            });

            Assert.Empty(provider.GetStatusBarIconMetadata());
            Assert.Equal(0, materializationCount);
        }

        [Theory]
        [InlineData(nameof(SystemMonitorSetting.IsShowTime), "SystemMonitor_Time", nameof(SystemMonitors.Time), 9999)]
        [InlineData(nameof(SystemMonitorSetting.IsShowRAM), "SystemMonitor_RAM", nameof(SystemMonitors.RAMStatusText), 9997)]
        [InlineData(nameof(SystemMonitorSetting.IsShowCPU), "SystemMonitor_CPU", nameof(SystemMonitors.CPUStatusText), 9998)]
        [InlineData(nameof(SystemMonitorSetting.IsShowUptime), "SystemMonitor_Uptime", nameof(SystemMonitors.GetUptime), 2)]
        [InlineData(nameof(SystemMonitorSetting.IsShowDisk), "SystemMonitor_Disk", nameof(SystemMonitors.TotalDiskFree), 9996)]
        public void VisibleStatusItem_ProducesExpectedMetadata(
            string propertyName,
            string expectedId,
            string expectedBindingName,
            int expectedOrder)
        {
            var config = new SystemMonitorSetting();
            typeof(SystemMonitorSetting).GetProperty(propertyName)!.SetValue(config, true);
            var monitor = new TestMonitorStatusSource();
            var provider = CreateStatusBarProvider(() => config, () => monitor);

            var item = Assert.Single(provider.GetStatusBarIconMetadata());

            Assert.Equal(expectedId, item.Id);
            Assert.Equal(expectedBindingName, item.BindingName);
            Assert.Equal(expectedOrder, item.Order);
            Assert.Same(monitor, item.Source);
            Assert.True(item.IsVisible);
        }

        [Fact]
        public void VisibleStatusItems_MaterializeSystemMonitorOnlyOnce()
        {
            var config = new SystemMonitorSetting { IsShowTime = true };
            var monitor = new TestMonitorStatusSource();
            int materializationCount = 0;
            var provider = CreateStatusBarProvider(() => config, () =>
            {
                materializationCount++;
                return monitor;
            });

            var first = Assert.Single(provider.GetStatusBarIconMetadata());
            var second = Assert.Single(provider.GetStatusBarIconMetadata());

            Assert.Equal(1, materializationCount);
            Assert.Same(monitor, first.Source);
            Assert.Same(monitor, second.Source);
        }

        [Theory]
        [InlineData(nameof(SystemMonitorSetting.IsShowTime))]
        [InlineData(nameof(SystemMonitorSetting.IsShowRAM))]
        [InlineData(nameof(SystemMonitorSetting.IsShowCPU))]
        [InlineData(nameof(SystemMonitorSetting.IsShowUptime))]
        [InlineData(nameof(SystemMonitorSetting.IsShowDisk))]
        public void VisibilityChange_RaisesStatusBarItemsChangedWithoutPrewarmingMonitor(string propertyName)
        {
            var config = new SystemMonitorSetting();
            int materializationCount = 0;
            var provider = CreateStatusBarProvider(() => config, () =>
            {
                materializationCount++;
                throw new InvalidOperationException("The visibility event must not initialize monitoring.");
            });
            int refreshCount = 0;
            provider.StatusBarItemsChanged += (_, _) => refreshCount++;

            typeof(SystemMonitorSetting).GetProperty(propertyName)!.SetValue(config, true);

            Assert.Equal(1, refreshCount);
            Assert.Equal(0, materializationCount);
        }

        [Fact]
        public void ConfigReload_RebindsProviderAndPeriodicStrategyToTheSameCurrentConfig()
        {
            var oldConfig = new SystemMonitorSetting { IsShowDisk = true };
            var currentConfig = oldConfig;
            var reloadNotifier = new TestConfigReloadNotifier();
            var monitor = new TestMonitorStatusSource();
            var provider = CreateStatusBarProvider(() => currentConfig, () => monitor, reloadNotifier);

            Assert.Single(provider.GetStatusBarIconMetadata());
            Assert.Same(oldConfig, monitor.LastConfiguration);
            Assert.False(InvokeShouldRunPeriodicUpdates(false, monitor.LastConfiguration!));

            var importedConfig = new SystemMonitorSetting { IsShowCPU = true };
            int refreshCount = 0;
            provider.StatusBarItemsChanged += (_, _) => refreshCount++;
            currentConfig = importedConfig;
            reloadNotifier.RaiseConfigsReloaded();

            var item = Assert.Single(provider.GetStatusBarIconMetadata());
            Assert.Equal("SystemMonitor_CPU", item.Id);
            Assert.Same(importedConfig, monitor.LastConfiguration);
            Assert.True(InvokeShouldRunPeriodicUpdates(false, monitor.LastConfiguration!));
            Assert.Equal(1, refreshCount);
        }

        private static bool InvokeShouldRunPeriodicUpdates(bool isDetailViewActive, SystemMonitorSetting config)
        {
            return (bool)ShouldRunPeriodicUpdates.Invoke(null, [isDetailViewActive, config])!;
        }

        private static SystemMonitorIStatusBarProvider CreateStatusBarProvider(
            Func<SystemMonitorSetting> configFactory,
            Func<ISystemMonitorStatusSource> monitorFactory,
            IConfigReloadNotifier? configReloadNotifier = null)
        {
            return (SystemMonitorIStatusBarProvider)StatusBarProviderConstructor.Invoke(
                [configFactory, monitorFactory, configReloadNotifier]);
        }

        private sealed class TestMonitorStatusSource : ISystemMonitorStatusSource, ISystemMonitorConfigurable
        {
            public SystemMonitorSetting? LastConfiguration { get; private set; }

            public double GetMaximumDiskUsage() => 0;

            public void UpdateConfiguration(SystemMonitorSetting config)
            {
                LastConfiguration = config;
            }
        }

        private sealed class TestConfigReloadNotifier : IConfigReloadNotifier
        {
            public event EventHandler? ConfigsReloaded;

            public void RaiseConfigsReloaded()
            {
                ConfigsReloaded?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
