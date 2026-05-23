using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ColorVision.Common.ThirdPartyApps
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
            var seenApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in AssemblyService.Instance.LoadImplementations<IThirdPartyAppProvider>())
            {
                foreach (var app in provider.GetThirdPartyApps())
                {
                    app.RefreshStatus();
                    if (seenApps.Add(GetAppIdentity(app)))
                        Apps.Add(app);
                }
            }
        }

        public void Refresh()
        {
            foreach (var app in Apps)
            {
                app.RefreshStatus();
            }
        }

        public IEnumerable<IGrouping<string, ThirdPartyAppInfo>> GetGroupedApps()
        {
            return Apps
                .OrderBy(a => a.Order).ThenBy(a => a.Name)
                .GroupBy(a => a.Group)
                .OrderBy(g => g.Min(a => a.Order));
        }

        private static string GetAppIdentity(ThirdPartyAppInfo app)
        {
            string? path = app.InstalledExePath
                           ?? app.PortableExePath
                           ?? app.LaunchPath
                           ?? app.InstallerPath;

            if (!string.IsNullOrWhiteSpace(path))
                return $"path:{NormalizePath(path)}|args:{app.LaunchArguments}";

            return $"name:{app.Group}|{app.Name}";
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
            }
            catch
            {
                return path;
            }
        }
    }
}
