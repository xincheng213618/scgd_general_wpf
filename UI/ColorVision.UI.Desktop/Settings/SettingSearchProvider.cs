using ColorVision.Common.MVVM;
using System.Windows;

namespace ColorVision.UI.Desktop.Settings;

public sealed class SettingSearchProvider : ISearchProvider
{
    public IEnumerable<ISearch> GetSearchItems()
    {
        return CreateItems(ConfigSettingManager.GetInstance().GetAllSettings(), SettingNavigation.Show);
    }

    internal static IReadOnlyList<ISearch> CreateItems(IEnumerable<ConfigSettingMetadata> settings, Action<string> navigate)
    {
        return SettingEntryCatalog.Create(settings)
            .DistinctBy(entry => entry.Id)
            .Select(entry => (ISearch)new SearchMeta
            {
                GuidId = entry.Id,
                Type = SearchType.Menu,
                CategoryKey = "Settings",
                Header = entry.Title,
                Description = string.IsNullOrWhiteSpace(entry.Description)
                    ? $"{entry.GroupDisplayName} / {entry.SectionDisplayName}"
                    : $"{entry.Description} · {entry.GroupDisplayName}",
                Aliases = new[] { entry.SearchText, entry.GroupDisplayName, entry.SectionDisplayName },
                Command = new RelayCommand(_ => navigate(entry.Id))
            }).ToArray();
    }
}

internal static class SettingNavigation
{
    public static void Show(string? settingId)
    {
        SettingWindow? existing = Application.Current.Windows.OfType<SettingWindow>().FirstOrDefault(window => window.IsVisible);
        if (existing != null)
        {
            existing.Activate();
            if (settingId != null) existing.NavigateToSetting(settingId);
            return;
        }

        var window = new SettingWindow { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        if (settingId != null) window.NavigateToSetting(settingId);
        window.ShowDialog();
        ConfigService.Instance.SaveConfigs();
    }
}
