using ColorVision.Engine;
using log4net;
using System;
using System.Windows;

namespace ProjectARVRPro.PluginConfig
{
    public static class ProjectARVRWindowHost
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectARVRWindowHost));

        public static ARVRWindow ShowOrActivate()
        {
            Application.Current.Dispatcher.VerifyAccess();

            ARVRWindow? window = ProjectWindowInstance.WindowInstance;
            if (window == null)
            {
                window = new ARVRWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                ProjectWindowInstance.WindowInstance = window;
                window.Closed += (s, e) =>
                {
                    if (ReferenceEquals(ProjectWindowInstance.WindowInstance, window))
                        ProjectWindowInstance.WindowInstance = null!;
                };
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            if (!window.IsVisible)
            {
                window.Show();
            }

            window.Activate();
            window.Focus();
            return window;
        }

        public static void ShowBatchResult(MeasureBatchModel batch, string flowName)
        {
            ArgumentNullException.ThrowIfNull(batch);
            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    ARVRWindow window = ShowOrActivate();
                    await window.OpenBatchResultAsync(batch, flowName);
                    window.Activate();
                }
                catch (Exception ex)
                {
                    log.Error($"打开 ARVR 批次结果失败: BatchId={batch.Id}, Flow={flowName}", ex);
                }
            });
        }
    }
}
