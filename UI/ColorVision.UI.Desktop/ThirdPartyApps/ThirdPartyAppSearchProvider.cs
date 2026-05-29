using ColorVision.Common.ThirdPartyApps;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    public class ThirdPartyAppSearchProvider : ISearchProvider
    {
        public IEnumerable<ISearch> GetSearchItems()
        {
            var manager = ThirdPartyAppManager.GetInstance();
            manager.Refresh();

            foreach (var app in manager.Apps
                .Where(app => app.IsInstalled && !string.IsNullOrWhiteSpace(app.Name))
                .OrderBy(app => app.Order)
                .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                yield return new SearchMeta
                {
                    GuidId = BuildSearchId(app),
                    Header = BuildHeader(app),
                    Icon = app.IconSource,
                    Command = app.DoubleClickCommand,
                };
            }
        }

        private static string BuildHeader(ThirdPartyAppInfo app)
        {
            if (string.IsNullOrWhiteSpace(app.Group))
                return app.Name;

            return $"{app.Name} ({app.Group})";
        }

        private static string BuildSearchId(ThirdPartyAppInfo app)
        {
            string launchTarget = app.LaunchPath
                ?? app.InstalledExePath
                ?? app.PortableExePath
                ?? app.InstallerPath
                ?? app.Name;

            return $"thirdparty:{app.Group}:{app.Name}:{launchTarget}";
        }
    }
}