using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Common.ThirdPartyApps
{
    public class ThirdPartyAppManager
    {
        private static ThirdPartyAppManager? _instance;
        public static ThirdPartyAppManager GetInstance() => _instance ??= new ThirdPartyAppManager();

        public ObservableCollection<ThirdPartyAppInfo> Apps { get; set; } = new ObservableCollection<ThirdPartyAppInfo>();
        public bool IsLoaded { get; private set; }

        private ThirdPartyAppManager()
        {
        }

        public void LoadApps(bool forceReload = false, bool forceProviderRefresh = false)
        {
            if (!forceReload && IsLoaded)
                return;

            ReplaceApps(CollectApps(forceProviderRefresh, CancellationToken.None));
        }

        public async Task LoadAppsAsync(
            bool forceReload = false,
            bool forceProviderRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (!forceReload && IsLoaded)
                return;

            var apps = await Task.Run(() => CollectApps(forceProviderRefresh, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ReplaceApps(apps);
        }

        private static List<ThirdPartyAppInfo> CollectApps(bool forceProviderRefresh, CancellationToken cancellationToken)
        {
            var appsByIdentity = new Dictionary<string, ThirdPartyAppInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in AssemblyService.Instance.LoadImplementations<IThirdPartyAppProvider>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var providerApps = provider is IThirdPartyAppCacheAwareProvider cacheAwareProvider
                        ? cacheAwareProvider.GetThirdPartyApps(forceProviderRefresh)
                        : provider.GetThirdPartyApps();

                    foreach (var app in providerApps)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (app == null)
                            continue;

                        app.RefreshStatus();
                        string identity = GetAppIdentity(app);
                        if (!appsByIdentity.TryGetValue(identity, out var existingApp) || ShouldReplace(existingApp, app))
                            appsByIdentity[identity] = app;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                }
            }

            return appsByIdentity.Values
                .OrderBy(a => a.Order)
                .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private void ReplaceApps(IReadOnlyList<ThirdPartyAppInfo> apps)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => ReplaceApps(apps));
                return;
            }

            Apps.Clear();
            foreach (var app in apps)
                Apps.Add(app);

            IsLoaded = true;
        }

        public void Refresh()
        {
            if (!IsLoaded)
            {
                LoadApps();
                return;
            }

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

        private static bool ShouldReplace(ThirdPartyAppInfo existingApp, ThirdPartyAppInfo candidateApp)
        {
            int orderComparison = candidateApp.Order.CompareTo(existingApp.Order);
            if (orderComparison != 0)
                return orderComparison < 0;

            if (candidateApp.IsInstalled != existingApp.IsInstalled)
                return candidateApp.IsInstalled;

            return StringComparer.CurrentCultureIgnoreCase.Compare(candidateApp.Name, existingApp.Name) < 0;
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
