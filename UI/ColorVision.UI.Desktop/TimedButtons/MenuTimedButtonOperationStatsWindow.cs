using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Linq;
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

    public class MenuTimedButtonOperationStatsWindow : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "按钮耗时统计";
        public override int Order => 1011;

        public override void Execute()
        {
            new TimedButtonOperationStatsWindowLauncher().OpenWindow();
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