using ColorVision.UI.Properties;
using log4net;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.Settings
{
    internal sealed class SettingWindowController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SettingWindowController));
        private const string AutoUpdateBindingName = "IsAutoUpdate";
        private const string ApplicationAutoUpdateConfigTypeName = "AutoUpdateConfig";
        private const string PluginAutoUpdateConfigTypeName = "MarketplaceWindowConfig";

        private readonly TextBox _searchTextBox;
        private readonly ListBox _navigationListBox;
        private readonly StackPanel _settingsContentPanel;
        private readonly TextBlock _currentGroupTitle;
        private readonly TextBlock _currentGroupDescription;
        private readonly List<SettingEntry> _settingEntries = new();

        private List<NavigationEntry> _navigationEntries = new();
        private string? _selectedGroup;
        private bool _isRefreshingNavigation;

        public SettingWindowController(TextBox searchTextBox, ListBox navigationListBox, StackPanel settingsContentPanel, TextBlock currentGroupTitle, TextBlock currentGroupDescription)
        {
            _searchTextBox = searchTextBox;
            _navigationListBox = navigationListBox;
            _settingsContentPanel = settingsContentPanel;
            _currentGroupTitle = currentGroupTitle;
            _currentGroupDescription = currentGroupDescription;
        }

        public void LoadConfigSettings()
        {
            _settingEntries.Clear();
            _settingsContentPanel.Children.Clear();

            var sortedSettings = ConfigSettingManager.GetInstance().GetAllSettings();
            ConfigSettingMetadata? applicationUpdateSetting = null;
            ConfigSettingMetadata? pluginUpdateSetting = null;

            foreach (var item in sortedSettings)
            {
                try
                {
                    switch (GetUpdateSettingKind(item))
                    {
                        case UpdateSettingKind.Application:
                            applicationUpdateSetting = item;
                            continue;
                        case UpdateSettingKind.Plugin:
                            pluginUpdateSetting = item;
                            continue;
                    }

                    AddSettingEntry(item);
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to add setting: {item.Name ?? item.BindingName}: {ex.Message}");
                }
            }

            AddStartupUpdateSetting(applicationUpdateSetting, pluginUpdateSetting);

            RefreshNavigationAndContent();
        }

        public void RefreshNavigationAndContent()
        {
            var visibleEntries = GetVisibleEntries().ToList();
            _navigationEntries = visibleEntries
                .GroupBy(entry => entry.Group)
                .Select(group => new NavigationEntry
                {
                    Group = group.Key,
                    DisplayName = group.First().GroupDisplayName,
                    Count = group.Count(),
                    Order = group.Min(entry => entry.Metadata.Order)
                })
                .OrderBy(entry => entry.Order)
                .ThenBy(entry => entry.DisplayName)
                .ToList();

            if (string.IsNullOrWhiteSpace(_selectedGroup) || !_navigationEntries.Any(entry => entry.Group == _selectedGroup))
            {
                _selectedGroup = _navigationEntries.FirstOrDefault()?.Group;
            }

            _isRefreshingNavigation = true;
            _navigationListBox.ItemsSource = null;
            _navigationListBox.ItemsSource = _navigationEntries;
            _navigationListBox.SelectedItem = _navigationEntries.FirstOrDefault(entry => entry.Group == _selectedGroup);
            _isRefreshingNavigation = false;

            RenderSelectedGroup();
        }

        public void SelectGroup(string group)
        {
            if (_isRefreshingNavigation) return;

            _selectedGroup = group;
            RenderSelectedGroup();
        }

        private void AddSettingEntry(ConfigSettingMetadata configSetting)
        {
            PropertyInfo? propertyInfo = null;
            if (configSetting.Type == ConfigSettingType.Property)
            {
                if (configSetting.Source == null || string.IsNullOrWhiteSpace(configSetting.BindingName)) return;

                propertyInfo = configSetting.Source.GetType().GetProperty(configSetting.BindingName);
                if (propertyInfo == null)
                {
                    Log.Warn($"Setting property not found: {configSetting.Source.GetType().Name}.{configSetting.BindingName}");
                    return;
                }
            }

            _settingEntries.Add(SettingMetadataResolver.CreateEntry(configSetting, propertyInfo));
        }

        private void AddStartupUpdateSetting(ConfigSettingMetadata? applicationUpdateSetting, ConfigSettingMetadata? pluginUpdateSetting)
        {
            var targets = new List<AggregatedBoolSettingTarget>();
            AddAggregatedTarget(targets, applicationUpdateSetting);
            AddAggregatedTarget(targets, pluginUpdateSetting);

            if (targets.Count == 0) return;

            var source = new AggregatedBoolSetting(targets);
            var propertyInfo = typeof(AggregatedBoolSetting).GetProperty(nameof(AggregatedBoolSetting.IsChecked));
            if (propertyInfo == null) return;

            var orderValues = new[] { applicationUpdateSetting?.Order, pluginUpdateSetting?.Order }
                .Where(order => order.HasValue)
                .Select(order => order!.Value)
                .ToList();

            var metadata = new ConfigSettingMetadata
            {
                Order = orderValues.Count == 0 ? 500 : orderValues.Min(),
                Group = ConfigSettingConstants.Universal,
                Name = SettingResources.StartupCheckUpdates,
                Description = SettingResources.StartupCheckUpdatesDescription,
                Section = ConfigSettingConstants.SectionBasic,
                Type = ConfigSettingType.Property,
                BindingName = nameof(AggregatedBoolSetting.IsChecked),
                Source = source
            };

            var entry = SettingMetadataResolver.CreateEntry(metadata, propertyInfo);
            entry.SearchText = string.Join(" ", entry.SearchText, SettingResources.StartupCheckUpdatesSearchAliases,
                "CheckUpdatesOnStartup CheckPluginUpdates AutoUpdateConfig MarketplaceWindowConfig IsAutoUpdate application plugin theme update startup detect").ToLowerInvariant();
            _settingEntries.Add(entry);
        }

        private static void AddAggregatedTarget(List<AggregatedBoolSettingTarget> targets, ConfigSettingMetadata? setting)
        {
            if (setting?.Source == null || string.IsNullOrWhiteSpace(setting.BindingName)) return;

            var propertyInfo = setting.Source.GetType().GetProperty(setting.BindingName, BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo == null || propertyInfo.PropertyType != typeof(bool)) return;

            targets.Add(new AggregatedBoolSettingTarget(setting.Source, propertyInfo));
        }

        private static UpdateSettingKind GetUpdateSettingKind(ConfigSettingMetadata setting)
        {
            if (setting.Type != ConfigSettingType.Property) return UpdateSettingKind.None;
            if (!string.Equals(setting.BindingName, AutoUpdateBindingName, StringComparison.Ordinal)) return UpdateSettingKind.None;
            if (setting.Source == null) return UpdateSettingKind.None;

            string sourceTypeName = setting.Source.GetType().Name;
            if (string.Equals(sourceTypeName, ApplicationAutoUpdateConfigTypeName, StringComparison.Ordinal)) return UpdateSettingKind.Application;
            if (string.Equals(sourceTypeName, PluginAutoUpdateConfigTypeName, StringComparison.Ordinal)) return UpdateSettingKind.Plugin;

            return UpdateSettingKind.None;
        }

        private enum UpdateSettingKind
        {
            None,
            Application,
            Plugin
        }

        private IEnumerable<SettingEntry> GetVisibleEntries()
        {
            string query = _searchTextBox.Text?.Trim() ?? string.Empty;

            foreach (var entry in _settingEntries)
            {
                if (!string.IsNullOrWhiteSpace(query) && !entry.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

                yield return entry;
            }
        }

        private void RenderSelectedGroup()
        {
            _settingsContentPanel.Children.Clear();

            if (string.IsNullOrWhiteSpace(_selectedGroup))
            {
                _currentGroupTitle.Text = Resources.Options;
                SetGroupDescription(string.Empty);
                _settingsContentPanel.Children.Add(SettingRowFactory.CreateEmptyState(SettingResources.NoMatchingSettings));
                return;
            }

            var groupEntries = GetVisibleEntries()
                .Where(entry => entry.Group == _selectedGroup)
                .OrderBy(entry => entry.SectionOrder)
                .ThenBy(entry => entry.Metadata.Order)
                .ToList();

            var navigationEntry = _navigationEntries.FirstOrDefault(entry => entry.Group == _selectedGroup);
            _currentGroupTitle.Text = navigationEntry?.DisplayName ?? SettingMetadataResolver.ResolveGroupDisplayName(_selectedGroup);

            if (groupEntries.Count == 0)
            {
                SetGroupDescription(string.Empty);
                _settingsContentPanel.Children.Add(SettingRowFactory.CreateEmptyState(SettingResources.NoMatchingSettings));
                return;
            }

            var propertyEntries = groupEntries.Where(entry => entry.Metadata.Type == ConfigSettingType.Property).ToList();
            var customEntries = groupEntries.Where(entry => entry.Metadata.Type != ConfigSettingType.Property).ToList();

            SetGroupDescription(BuildGroupDescription(groupEntries));

            if (propertyEntries.Count == 0)
            {
                RenderCustomPages(customEntries);
                return;
            }

            foreach (var sectionGroup in propertyEntries.GroupBy(entry => entry.SectionKey).OrderBy(group => group.Min(entry => entry.SectionOrder)))
            {
                _settingsContentPanel.Children.Add(SettingRowFactory.CreateSectionCard(sectionGroup.First().SectionDisplayName, sectionGroup.ToList()));
            }

            RenderCustomPages(customEntries);
        }

        private void RenderCustomPages(List<SettingEntry> customEntries)
        {
            for (int index = 0; index < customEntries.Count; index++)
            {
                var customPage = SettingRowFactory.CreateCustomPage(customEntries[index], showTitle: customEntries.Count > 1);
                if (index > 0)
                {
                    customPage.Margin = new Thickness(0, 12, 0, 0);
                }

                _settingsContentPanel.Children.Add(customPage);
            }
        }

        private void SetGroupDescription(string description)
        {
            _currentGroupDescription.Text = description;
            _currentGroupDescription.Visibility = string.IsNullOrWhiteSpace(description) ? Visibility.Collapsed : Visibility.Visible;
        }

        private string BuildGroupDescription(List<SettingEntry> groupEntries)
        {
            string query = _searchTextBox.Text?.Trim() ?? string.Empty;
            string format = string.IsNullOrWhiteSpace(query) ? SettingResources.CountFormat : SettingResources.MatchingCountFormat;
            string countText = string.Format(CultureInfo.CurrentCulture, format, groupEntries.Count);
            string pageDescription = GetPageDescription(groupEntries);

            return string.IsNullOrWhiteSpace(pageDescription)
                ? countText
                : string.Format(CultureInfo.CurrentCulture, "{0} - {1}", pageDescription, countText);
        }

        private static string GetPageDescription(List<SettingEntry> groupEntries)
        {
            var descriptions = groupEntries
                .Where(entry => entry.Metadata.Type != ConfigSettingType.Property)
                .Select(entry => entry.Description)
                .Where(description => !string.IsNullOrWhiteSpace(description))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return descriptions.Count == 1 ? descriptions[0] : string.Empty;
        }
    }
}