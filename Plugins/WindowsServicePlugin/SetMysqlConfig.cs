using System.Windows;
using WindowsServicePlugin.ServiceManager;

namespace WindowsServicePlugin
{
    public partial class SetServiceConfig
    {
        public static class SetMysqlConfig
        {
            public static bool Import(Window owner, out string message)
            {
                if (!LegacyServiceConfig.EnsureAppConfigPath(owner, out string configPath))
                {
                    message = "没有找到旧版 CVWinSMS.exe 或它旁边的 config\\App.config。";
                    return false;
                }

                return LegacyServiceConfig.Import(configPath, out message);
            }
        }
    }
}
