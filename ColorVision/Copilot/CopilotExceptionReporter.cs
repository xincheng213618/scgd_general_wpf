using System;
using System.Windows;

namespace ColorVision.Copilot
{
    public static class CopilotExceptionReporter
    {
        private static readonly object SyncRoot = new();
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(12);
        private static readonly TimeSpan BurstMergeWindow = TimeSpan.FromSeconds(2);

        private static WeakReference<CopilotExceptionWindow>? _activeWindowReference;
        private static string _lastFingerprint = string.Empty;
        private static DateTimeOffset _lastShownAt = DateTimeOffset.MinValue;

        public static void ShowException(Exception exception, string source)
        {
            if (exception == null)
                return;

            var application = Application.Current;
            if (application?.Dispatcher != null && !application.Dispatcher.CheckAccess())
            {
                application.Dispatcher.BeginInvoke(() => ShowException(exception, source));
                return;
            }

            var fingerprint = BuildFingerprint(exception);
            var now = DateTimeOffset.UtcNow;
            CopilotExceptionWindow? activeWindow;
            var reuseAsDuplicate = false;
            var reuseAsBurst = false;

            lock (SyncRoot)
            {
                activeWindow = TryGetActiveWindow();
                if (activeWindow != null)
                {
                    var elapsed = now - _lastShownAt;
                    if (string.Equals(_lastFingerprint, fingerprint, StringComparison.Ordinal) && elapsed <= DuplicateWindow)
                    {
                        reuseAsDuplicate = true;
                    }
                    else if (elapsed <= BurstMergeWindow)
                    {
                        reuseAsBurst = true;
                    }
                }

                _lastFingerprint = fingerprint;
                _lastShownAt = now;
            }

            if (reuseAsDuplicate && activeWindow != null)
            {
                activeWindow.RegisterDuplicateOccurrence(exception, source);
                return;
            }

            if (reuseAsBurst && activeWindow != null)
            {
                activeWindow.RegisterAdditionalException(exception, source);
                return;
            }

            var window = new CopilotExceptionWindow(exception, source);
            window.Closed += Window_Closed;

            lock (SyncRoot)
            {
                _activeWindowReference = new WeakReference<CopilotExceptionWindow>(window);
            }

            var owner = Application.Current?.GetActiveWindow();
            if (owner != null)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            window.Show();
            window.BringToFront();
        }

        private static string BuildFingerprint(Exception exception)
        {
            return string.Join("|", new[]
            {
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message ?? string.Empty,
                exception.Source ?? string.Empty,
                exception.TargetSite?.DeclaringType?.FullName ?? string.Empty,
                exception.TargetSite?.Name ?? string.Empty,
            });
        }

        private static CopilotExceptionWindow? TryGetActiveWindow()
        {
            if (_activeWindowReference == null || !_activeWindowReference.TryGetTarget(out var window))
                return null;

            return window.IsLoaded ? window : null;
        }

        private static void Window_Closed(object? sender, EventArgs e)
        {
            lock (SyncRoot)
            {
                if (_activeWindowReference != null
                    && _activeWindowReference.TryGetTarget(out var window)
                    && ReferenceEquals(window, sender))
                {
                    _activeWindowReference = null;
                }
            }
        }
    }
}