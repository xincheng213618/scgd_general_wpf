using ColorVision.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public class ThirdPartyAppManager
    {
        private static ThirdPartyAppManager? _instance;
        public static ThirdPartyAppManager GetInstance() => _instance ??= new ThirdPartyAppManager();

        public ObservableCollection<ThirdPartyAppInfo> Apps { get; set; } = new ObservableCollection<ThirdPartyAppInfo>();

        private ThirdPartyAppManager()
        {
            LoadApps();
        }

        public void LoadApps()
        {
            Apps.Clear();

            foreach (var provider in AssemblyService.Instance.LoadImplementations<IThirdPartyAppProvider>())
            {
                foreach (var app in provider.GetThirdPartyApps())
                {
                    app.RefreshStatus();
                    Apps.Add(app);
                }
            }

            ScanInstallToolDirectory();
        }

        public void Refresh()
        {
            foreach (var app in Apps)
            {
                app.RefreshStatus();
            }
        }

        private void ScanInstallToolDirectory()
        {
            string installToolDir = Path.Combine("Assets", "InstallTool");
            if (!Directory.Exists(installToolDir)) return;

            var existingInstallers = new HashSet<string>(Apps.Where(a => !string.IsNullOrEmpty(a.InstallerPath)).Select(a => Path.GetFullPath(a.InstallerPath)));

            foreach (var file in Directory.GetFiles(installToolDir, "*.exe"))
            {
                string fullPath = Path.GetFullPath(file);
                if (existingInstallers.Contains(fullPath)) continue;

                var app = new ThirdPartyAppInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    InstallerPath = file,
                };
                app.RefreshStatus();
                Apps.Add(app);
            }
        }
    }
}
