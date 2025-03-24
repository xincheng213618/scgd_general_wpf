using ColorVision.Themes.Controls;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.Update
{


    public class MenuUpdateReInstall : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);

        public override int Order => 10003;
        private static string AssemblyCompany => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision";
        private static string CurrentInstallFile => Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),AssemblyCompany), $"ColorVision-{Assembly.GetExecutingAssembly().GetName().Version}.exe");
        
        public override string Header => "重新安装当前版本";
        public override void Execute()
        {
            if (File.Exists(CurrentInstallFile))
            {
                AutoUpdater.RestartApplication(CurrentInstallFile);
            }
            else
            {
                AutoUpdater.GetInstance().Update(Assembly.GetExecutingAssembly().GetName().Version, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyCompany), false);
            }
        }
    }

    public class MenuUpdateReInstallClear : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override string GuidId => nameof(MenuUpdateReInstallClear);
        public override int Order => 14;
        private static string AssemblyCompany => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision";
        public override string Header => "清除安装包缓存";
        public override void Execute()
        {
            string[] updateFiles = Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyCompany), "ColorVision-*.exe");
            foreach (string updateFile in updateFiles)
            {
                try
                {
                    File.Delete(updateFile);
                }
                catch (Exception ex)
                {
                    MessageBox1.Show(ex.ToString());
                    return;
                }
            }
            MessageBox1.Show("执行结束");
        }
    }
}
