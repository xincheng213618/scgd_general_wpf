using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows;
using System.Windows.Input;

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
        private readonly IMessageUpdater log;

        public TaskViewerInitializer(IMessageUpdater messageUpdater)
        {
            log = messageUpdater;
        }

        public override string Name => nameof(TaskViewerInitializer);

        public override int Order => 1;

        public override async Task InitializeAsync()
        {
            log.Update("初始化定时任务");
            QuartzSchedulerManager.GetInstance();
            await Task.Delay(0);
        }
    }
}
