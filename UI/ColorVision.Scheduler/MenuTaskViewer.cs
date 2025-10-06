using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Scheduler
{
    public class MenuTaskViewer :  MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override int Order => 10;

        public override string Header => Properties.Resources.TaskViewerWindow;

        public override void Execute()
        {
            new TaskViewerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

    public class TaskViewerInitializer : InitializerBase
    {

        public override string Name => nameof(TaskViewerInitializer);

        public override int Order => 1;

        public override async Task InitializeAsync()
        {
            QuartzSchedulerManager.GetInstance();
            await Task.Delay(0);
        }
    }
}
