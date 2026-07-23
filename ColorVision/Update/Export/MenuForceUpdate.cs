using ColorVision.Properties;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.Threading.Tasks;

namespace ColorVision.Update
{
    public class MenuForceUpdate : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuForceUpdate));

        public override string OwnerGuid => nameof(MenuUpdate);
        public override string Header => Resources.ForceUpdate;
        public override int Order => 100;

        public override void Execute()
        {
            _ = ExecuteAsync();
        }

        private static async Task ExecuteAsync()
        {
            try
            {
                await AutoUpdater.ForceUpdate();
            }
            catch (OperationCanceledException)
            {
                log.Debug("Force update canceled.");
            }
            catch (Exception ex)
            {
                log.Error("Force update failed.", ex);
            }
        }
    }
}
