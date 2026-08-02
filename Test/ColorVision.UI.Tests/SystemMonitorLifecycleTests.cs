using ColorVision.UI.Configs;
using System.Reflection;

namespace ColorVision.UI.Tests
{
    public class SystemMonitorLifecycleTests
    {
        private static readonly MethodInfo ShouldRunPeriodicUpdates =
            typeof(SystemMonitors).GetMethod(
                "ShouldRunPeriodicUpdates",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(SystemMonitors).FullName, "ShouldRunPeriodicUpdates");

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

        private static bool InvokeShouldRunPeriodicUpdates(bool isDetailViewActive, SystemMonitorSetting config)
        {
            return (bool)ShouldRunPeriodicUpdates.Invoke(null, [isDetailViewActive, config])!;
        }
    }
}
