using ColorVision.Common.MVVM;
using ColorVision.Properties;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.UI.ServiceHost;
using log4net;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update.Export
{
    public class MenuFileAssociation : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override int Order => 1000;
        public override string Header => Resources.MenuFileAssociation;
        public override Visibility Visibility => Visibility.Collapsed;

        public override async void Execute()
        {
            bool success = await FileAssociationHelper.RegisterAssociationsAsync().ConfigureAwait(true);
            MessageBox.Show(
                Application.Current.GetActiveWindow(),
                success ? Resources.RegistryAppliedSuccess : Resources.ComRegistrationFailed,
                "ColorVision",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
    }

    public class RegConfig : ViewModelBase, IConfig
    {
        public Version Version { get => _Version; set { _Version = value; OnPropertyChanged(); } }
        private Version _Version = new(0, 0, 0);
    }

    public class RegInitialized : MainWindowInitializedBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RegInitialized));

        public static Version Version { get; set; } = new(1, 0, 3, 0);

        public override Task Initialize()
        {
            RegConfig regConfig = ConfigService.Instance.GetRequiredService<RegConfig>();
            if (regConfig.Version < Version)
            {
                regConfig.Version = Version;
                ConfigService.Instance.SaveConfigs();
                log.Info("File association auto registration is disabled; use ColorVisionServiceHost from the update menu.");
            }

            return Task.CompletedTask;
        }
    }

    public static class FileAssociationHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FileAssociationHelper));

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        public static bool RegisterAssociations()
        {
            return RegisterAssociationsAsync().GetAwaiter().GetResult();
        }

        public static async Task<bool> RegisterAssociationsAsync()
        {
            try
            {
                string appPath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to resolve executable path.");
                ServiceHostResponse response = await ColorVisionServiceHostClient.Default
                    .RegisterFileAssociationsAsync(appPath)
                    .ConfigureAwait(false);

                if (!response.Success)
                {
                    log.Warn($"RegisterAssociations failed: {response.ToDisplayText()}");
                    return false;
                }

                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                log.Info("RegisterAssociations completed through ColorVisionServiceHost.");
                return true;
            }
            catch (Exception ex)
            {
                log.Error("RegisterAssociations failed.", ex);
                return false;
            }
        }
    }
}
