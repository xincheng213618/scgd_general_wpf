using log4net;
using System;
using System.Windows;

namespace ColorVision.Update
{
    public static class ApplicationUpdateShutdown
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ApplicationUpdateShutdown));

        public static void Request()
        {
            Application? application = Application.Current;
            if (application == null)
            {
                log.Warn("WPF application is unavailable during update handoff; falling back to immediate process exit.");
                Environment.Exit(0);
                return;
            }

            log.Info("Graceful WPF application shutdown requested for update handoff.");
            if (application.Dispatcher.CheckAccess())
            {
                application.Shutdown();
                return;
            }

            application.Dispatcher.Invoke(() => application.Shutdown());
        }
    }
}
