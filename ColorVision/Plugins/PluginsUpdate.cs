using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Plugins
{
    public class PluginsUpdate : IMainWindowInitialized
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginsUpdate));

        public async Task Initialize()
        {
            if (!PluginWindowConfig.Instance.IsAutoUpdate) return;
            
            log.Info("PluginsInitializedCheck");
            try
            {
                PluginManager.GetInstance();
                await Task.Delay(6000);
                foreach (var item in PluginManager.GetInstance().Plugins)
                {
                    if (item.LastVersion > item.AssemblyVersion)
                    {
                        if (MessageBox.Show(Application.Current.GetActiveWindow(),"检测到存在可以更新的插件，是否更新?", item.AssemblyName,MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            new PluginManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return;
        }
    }
}
