using ColorVision.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            foreach (var provider in AssemblyService.Instance.LoadImplementations<IThirdPartyAppProvider>())
            {
                foreach (var app in provider.GetThirdPartyApps())
                {
                    app.RefreshStatus();
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
    }
}
