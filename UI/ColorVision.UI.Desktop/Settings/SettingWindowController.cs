using ColorVision.UI.Properties;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.Settings
{
    internal sealed class SettingWindowController
    {
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

        public void LoadConfigSettings(IEnumerable<ConfigSettingMetadata>? settings = null)
        {
            _settingEntries.Clear();
            _settingsContentPanel.Children.Clear();

            _settingEntries.AddRange(SettingEntryCatalog.Create(settings ?? ConfigSettingManager.GetInstance().GetAllSettings()));

            RefreshNavigationAndContent();
        }

        public void RefreshNavigationAndContent()
        {
            var visibleEntries = GetVisibleEntries().ToList();
            _navigationEntries = visibleEntries
                .GroupBy(entry => entry.Group)
                .Select(group =>
                {
                    string displayName = group.First().GroupDisplayName;
                    return new NavigationEntry
                    {
                        Group = group.Key,
                        DisplayName = displayName,
                        IconGlyph = ResolveNavigationIcon(group.Key, displayName),
                        Order = GetNavigationOrder(group.Key, group.Min(entry => entry.Metadata.Order))
                    };
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

        public FrameworkElement? NavigateToSetting(string id)
        {
            SettingEntry? entry = _settingEntries.FirstOrDefault(item => item.Id == id);
            if (entry == null) return null;
            _searchTextBox.Clear();
            _selectedGroup = entry.Group;
            RefreshNavigationAndContent();
            return entry.RenderedElement;
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
            foreach (SettingEntry entry in _settingEntries) entry.RenderedElement = null;

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
                customEntries[index].RenderedElement = customPage;
                customPage.Tag = customEntries[index].Id;
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

        private static string BuildGroupDescription(List<SettingEntry> groupEntries)
        {
            return GetPageDescription(groupEntries);
        }

        private static int GetNavigationOrder(string group, int order)
        {
            return string.Equals(group, ConfigSettingConstants.Universal, StringComparison.OrdinalIgnoreCase) ? int.MinValue : order;
        }

        private static string ResolveNavigationIcon(string group, string displayName)
        {
            if (string.Equals(group, ConfigSettingConstants.Universal, StringComparison.OrdinalIgnoreCase))
                return "\uE713";

            string text = $"{group} {displayName}".ToLowerInvariant();
            if (text.Contains("maintenance") || text.Contains("维护")) return "\uE74D";
            if (text.Contains("dump") || text.Contains("转储")) return "\uE9D9";
            if (text.Contains("remote") || text.Contains("局域网")) return "\uE968";
            if (text.Contains("mcp")) return "\uE968";
            if (text.Contains("communication") || text.Contains("protocol") || text.Contains("通信")) return "\uE968";
            if (text.Contains("hot") || text.Contains("key") || text.Contains("快捷")) return "\uE765";
            if (text.Contains("monitor") || text.Contains("performance") || text.Contains("监控")) return "\uE9D9";
            if (text.Contains("import") || text.Contains("export") || text.Contains("导入") || text.Contains("导出")) return "\uE8AB";
            if (text.Contains("plugin") || text.Contains("插件")) return "\uECAA";
            if (text.Contains("desktop") || text.Contains("pet") || text.Contains("桌面")) return "\uE77B";

            return "\uE713";
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
