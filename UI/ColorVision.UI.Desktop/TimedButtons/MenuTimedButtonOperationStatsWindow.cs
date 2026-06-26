using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Desktop.Properties;
using System.Windows;

namespace ColorVision.UI.Desktop.TimedButtons
{
    public sealed class TimedButtonOperationStatsWindowLauncher : ITimedButtonOperationStatsWindowLauncher
    {
        public bool CanOpen => Application.Current != null;

        public void OpenWindow(string? operationKey = null)
        {
            if (Application.Current == null)
            {
                return;
            }

            TimedButtonOperationStatsWindow? existingWindow = Application.Current.Windows
                .OfType<TimedButtonOperationStatsWindow>()
                .FirstOrDefault();

            if (existingWindow != null)
            {
                existingWindow.ApplySearchFilter(operationKey);
                if (existingWindow.WindowState == WindowState.Minimized)
                {
                    existingWindow.WindowState = WindowState.Normal;
                }

                existingWindow.Activate();
                return;
            }

            Window? owner = Application.Current.GetActiveWindow();
            TimedButtonOperationStatsWindow window = new TimedButtonOperationStatsWindow(operationKey)
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            };
            window.Show();
            window.Activate();
        }
    }

    public class TimedButtonOperationStatsAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new[]
            {
                new ThirdPartyAppInfo
                {
                    Name = Resources.MenuTimedButtonOperationStats,
                    Group = "ColorVision",
                    Order = 11,
                    LaunchAction = () => new TimedButtonOperationStatsWindowLauncher().OpenWindow(),
                    GetIconPath = () => Environment.ProcessPath
                }
            };
        }
    }

    public sealed class TimedButtonOperationStatsWindowLauncherInitializer : InitializerBase
    {
        public override int Order => 13;
        public override string Name => nameof(TimedButtonOperationStatsWindowLauncherInitializer);

        public override Task InitializeAsync()
        {
            TimedButtonOperationStatsWindowLauncherProvider.SetLauncher(new TimedButtonOperationStatsWindowLauncher());
            return Task.CompletedTask;
        }
    }
}
