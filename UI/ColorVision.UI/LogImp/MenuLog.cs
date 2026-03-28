using ColorVision.Common.Utilities;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using log4net;
using log4net.Appender;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.LogImp
{
    public class MenuLog : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 3;
        public override string Header => Properties.Resources.Log;
    }
    public class MenuLogWindow : GlobalMenuBase, IHotKey
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 10005;
        public override string Header => Properties.Resources.Log;
        public override string InputGestureText => Hotkey.ToString();

        public static Hotkey Hotkey { get; set; } = new Hotkey(Key.L, ModifierKeys.Control);
        public HotKeys HotKeys => new HotKeys(Properties.Resources.Log, Hotkey, Execute);
        public override void Execute() => new WindowLog() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
    }

    public class MenuOpenConfigFile : GlobalMenuBase
    {
        public override string OwnerGuid => nameof(MenuLog);
        public override int Order => 1;
        public override string Header => Properties.Resources.OpenConfigFile;
        public override void Execute()
        {
            string fileName = ConfigHandler.GetInstance().ConfigFilePath;
            bool result = Tool.HasDefaultProgram(fileName);
            if (!result)
                Process.Start(result ? "explorer.exe" : "notepad.exe", fileName);
            Process.Start("explorer.exe", $"{Path.GetDirectoryName(ConfigHandler.GetInstance().ConfigFilePath)}");
        }
    }


    public class MenuOpenConfigFolder : GlobalMenuBase
    {
        public override string OwnerGuid => nameof(MenuLog);
        public override int Order => 2;
        public override string Header => Properties.Resources.OpenConfigFolder;
        public override void Execute()
        {
            Process.Start("explorer.exe", $"{Path.GetDirectoryName(ConfigHandler.GetInstance().ConfigFilePath)}");
        }
    }

    public class MenuOpenLogFolder : GlobalMenuBase
    {
        public FileAppender? fileAppender { get; set; } = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
        public override string OwnerGuid => nameof(MenuLog);
        public override int Order => 3;
        public override string Header => Properties.Resources.OpenLogFolder;
        public override Visibility Visibility => fileAppender != null ? Visibility.Visible : Visibility.Collapsed;
        public override void Execute()
        {
            if (fileAppender != null)
            {
                Process.Start("explorer.exe", $"{Path.GetDirectoryName(fileAppender.File)}");
            }
            else
            {
                MessageBox.Show(Properties.Resources.NoLocalLog4Output);
            }
        }
    }
}
