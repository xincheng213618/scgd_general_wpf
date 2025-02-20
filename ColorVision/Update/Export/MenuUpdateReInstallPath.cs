using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System;
using System.IO;
using System.Reflection;

namespace ColorVision.Update
{
    public class MenuUpdateReInstallPath : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override string GuidId => nameof(MenuUpdateReInstallPath);
        public override int Order => 10004;
        private static string AssemblyCompany => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision";
        public override string Header => "打开缓存文件夹";
        public override void Execute()
        {
            PlatformHelper.Open(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyCompany));
        }
    }
}
