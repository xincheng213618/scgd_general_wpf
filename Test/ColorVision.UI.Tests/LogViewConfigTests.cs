using ColorVision.UI.LogImp;
using ColorVision.UI.LogImp.Controls;

namespace ColorVision.UI.Tests
{
    public class LogViewConfigTests
    {
        [Fact]
        public void LogPanelLimitDoesNotChangeFullLogLimit()
        {
            var completeLogConfig = new LogConfig();
            var logPanelConfig = new LogPanelConfig();

            Assert.Equal(LogConstants.DefaultMaxEntries, completeLogConfig.MaxEntries);
            Assert.Equal(LogConstants.DefaultLogPanelMaxEntries, logPanelConfig.MaxEntries);

            logPanelConfig.MaxEntries = 500;

            Assert.Equal(LogConstants.DefaultMaxEntries, completeLogConfig.MaxEntries);
        }

        [Fact]
        public void RealtimeLogConfigsHaveIndependentOneThousandEntryLimits()
        {
            var first = new TestRealtimeLogConfig();
            var second = new TestRealtimeLogConfig();

            Assert.Equal(LogConstants.DefaultRealtimeLogMaxEntries, first.MaxEntries);
            Assert.Equal(LogConstants.DefaultRealtimeLogMaxEntries, second.MaxEntries);
            Assert.True(first.AutoRefresh);
            Assert.True(second.AutoRefresh);

            first.MaxEntries = 250;
            first.AutoRefresh = false;

            Assert.Equal(250, first.MaxEntries);
            Assert.Equal(LogConstants.DefaultRealtimeLogMaxEntries, second.MaxEntries);
            Assert.False(first.AutoRefresh);
            Assert.True(second.AutoRefresh);
        }

        [Fact]
        public void ExistingRealtimeViewerConstructorsUseSafeLocalDefaults()
        {
            StaTest.Run(() =>
            {
                var firstViewer = new LogViewerControl();
                var secondViewer = new LogViewerControl();
                using var firstAppender = new LogViewerAppender(firstViewer);
                using var secondAppender = new LogViewerAppender(secondViewer);

                Assert.Equal(LogConstants.DefaultRealtimeLogMaxEntries, firstViewer.MaxEntries);
                Assert.Equal(LogConstants.DefaultRealtimeLogMaxEntries, secondViewer.MaxEntries);

                firstViewer.MaxEntries = 250;

                Assert.Equal(250, firstViewer.MaxEntries);
                Assert.Equal(LogConstants.DefaultRealtimeLogMaxEntries, secondViewer.MaxEntries);
            }, TimeSpan.FromSeconds(10), "The STA log-view configuration test did not finish.");
        }

        private sealed class TestRealtimeLogConfig : RealtimeLogViewConfig
        {
        }

    }
}
