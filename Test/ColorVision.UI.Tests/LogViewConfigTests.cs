using ColorVision.UI.LogImp;
using ColorVision.UI.LogImp.Controls;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ColorVision.UI.Tests
{
    public class LogViewConfigTests
    {
        [Fact]
        public void LogPanelUsesIndependentOneThousandEntryLimit()
        {
            var completeLogConfig = new LogConfig();
            var logPanelConfig = new LogPanelConfig();

            Assert.Equal(10000, LogConstants.DefaultMaxEntries);
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
            Assert.Null(typeof(TestRealtimeLogConfig).GetProperty(nameof(LogConfig.LogLevel)));
        }

        [Fact]
        public void LogPanelViewConfigDoesNotContainGlobalLogLevel()
        {
            Assert.Null(typeof(LogPanelConfig).GetProperty(nameof(LogConfig.LogLevel)));
        }

        [Fact]
        public void ExistingRealtimeViewerConstructorsUseSafeLocalDefaults()
        {
            Assert.NotNull(typeof(LogOutput).GetConstructor(new[] { typeof(string) }));
            Assert.NotNull(typeof(LogViewerAppender).GetConstructor(new[] { typeof(LogViewerControl) }));

            RunInSta(() =>
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
            });
        }

        private sealed class TestRealtimeLogConfig : RealtimeLogViewConfig
        {
        }

        private static void RunInSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The STA log-view configuration test did not finish.");

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
