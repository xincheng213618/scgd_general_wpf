using ColorVision.Common.Utilities;
using log4net;
using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Menus
{
    public class MenuLog : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "Log";
        public override int Order => 3;
        public override string Header => Properties.Resources.Log;
    }


    public class MenuOpenConfigFile : MenuItemBase
    {
        public override string OwnerGuid => "Log";
        public override string GuidId => "OpenConfigFile";
        public override int Order => 1;
        public override string Header => Properties.Resources.OpenConfigFile;
        public override void Execute()
        {
            Process.Start("explorer.exe", $"{Path.GetDirectoryName(ConfigHandler.GetInstance().ConfigFilePath)}");
        }
    }


    public class MenuOpenConfigFolder : MenuItemBase
    {
        public override string OwnerGuid => "Log";
        public override string GuidId => "OpenConfigFolder";
        public override int Order => 2;
        public override string Header => Properties.Resources.OpenConfigFolder;
        public override void Execute()
        {
            string fileName = ConfigHandler.GetInstance().ConfigFilePath;
            bool result = Tool.HasDefaultProgram(fileName);
            if (!result)
                Process.Start(result ? "explorer.exe" : "notepad.exe", fileName);
        }
    }



    public class MenuOpenLogFolder : MenuItemBase
    {
        public override string OwnerGuid => "Log";
        public override string GuidId => "OpenLogFolder";
        public override int Order => 3;
        public override string Header => Properties.Resources.OpenLogFolder;
        public override void Execute()
        {
            var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
            if (fileAppender != null)
            {
                Process.Start("explorer.exe", $"{Path.GetDirectoryName(fileAppender.File)}");
            }
        }
    }
}
