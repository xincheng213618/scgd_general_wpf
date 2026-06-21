using ColorVision.UI.LogImp.Controls;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.LogImp
{
    public sealed class ModuleLogViewerBinder : IDisposable
    {
        private const string ModuleLogPattern = "%date %-5level %message%newline";

        private readonly Hierarchy _hierarchy;
        private readonly LogViewerAppender _appender;
        private bool _disposed;

        public ModuleLogViewerBinder(LogViewerControl logViewer, params string[] loggerPrefixes)
        {
            ArgumentNullException.ThrowIfNull(logViewer);

            string[] prefixes = loggerPrefixes
                .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
                .Select(prefix => prefix.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (prefixes.Length == 0)
            {
                throw new ArgumentException("At least one logger prefix is required.", nameof(loggerPrefixes));
            }

            _hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
            var layout = new PatternLayout(ModuleLogPattern);
            layout.ActivateOptions();

            _appender = new LogViewerAppender(logViewer)
            {
                Name = $"ModuleLogViewerAppender-{Guid.NewGuid():N}",
                Layout = layout,
                IgnoreAutoRefresh = true,
            };
            _appender.AddFilter(new LoggerPrefixFilter(prefixes));
            _appender.AddFilter(new DenyAllFilter());
            _hierarchy.Root.AddAppender(_appender);
            log4net.Config.BasicConfigurator.Configure(_hierarchy);
            logViewer.SetViewerContextMenu(CreateContextMenu(logViewer));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _hierarchy.Root.RemoveAppender(_appender);
            _appender.Dispose();
            log4net.Config.BasicConfigurator.Configure(_hierarchy);
        }

        private ContextMenu CreateContextMenu(LogViewerControl logViewer)
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = Properties.Resources.MenuCopy, Command = ApplicationCommands.Copy, CommandTarget = logViewer, InputGestureText = "Ctrl+C" });
            contextMenu.Items.Add(new MenuItem { Header = Properties.Resources.MenuSelectAll, Command = ApplicationCommands.SelectAll, CommandTarget = logViewer, InputGestureText = "Ctrl+A" });
            contextMenu.Items.Add(new Separator());

            var autoRefreshMenuItem = new MenuItem { Header = Properties.Resources.AutoRefresh, IsCheckable = true, IsChecked = _appender.AutoRefresh };
            autoRefreshMenuItem.Click += (_, _) => _appender.AutoRefresh = autoRefreshMenuItem.IsChecked;
            contextMenu.Items.Add(autoRefreshMenuItem);
            contextMenu.Opened += (_, _) => autoRefreshMenuItem.IsChecked = _appender.AutoRefresh;
            return contextMenu;
        }

        private sealed class LoggerPrefixFilter : FilterSkeleton
        {
            private readonly string[] _loggerPrefixes;

            public LoggerPrefixFilter(string[] loggerPrefixes)
            {
                _loggerPrefixes = loggerPrefixes;
            }

            public override FilterDecision Decide(LoggingEvent loggingEvent)
            {
                string? loggerName = loggingEvent.LoggerName;
                if (string.IsNullOrWhiteSpace(loggerName))
                {
                    return FilterDecision.Neutral;
                }

                foreach (string prefix in _loggerPrefixes)
                {
                    if (string.Equals(loggerName, prefix, StringComparison.Ordinal) ||
                        loggerName.StartsWith(prefix + ".", StringComparison.Ordinal))
                    {
                        return FilterDecision.Accept;
                    }
                }

                return FilterDecision.Neutral;
            }
        }
    }
}
