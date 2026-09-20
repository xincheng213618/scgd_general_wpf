using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.UI.Desktop.Feedback
{
    /// <summary>
    /// Menu item that opens the Send Feedback window from the Help menu.
    /// </summary>
    public class MenuSendFeedback : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 8000;
        public override string Header => LocalizedMenuAccessKey.Format(Resources.SendFeedback, 'F', requiresInput: true);

        public override void Execute()
        {
            new FeedbackWindow()
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }
    }
}
