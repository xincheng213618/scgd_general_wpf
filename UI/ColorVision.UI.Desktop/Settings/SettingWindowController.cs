using ColorVision.UI.Properties;
using log4net;
using System.Globalization;
using System.Reflection;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.Settings
{
    internal sealed class SettingWindowController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SettingWindowController));

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
            foreach (var item in sortedSettings)
            {
                try
                {
                    AddSettingEntry(item);
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to add setting: {item.Name ?? item.BindingName}: {ex.Message}");
                }
            }

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
                _currentGroupDescription.Text = string.Empty;
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
            _currentGroupDescription.Text = BuildGroupDescription(groupEntries.Count);

            if (groupEntries.Count == 0)
            {
                _settingsContentPanel.Children.Add(SettingRowFactory.CreateEmptyState(SettingResources.NoMatchingSettings));
                return;
            }

            foreach (var sectionGroup in groupEntries.GroupBy(entry => entry.SectionKey).OrderBy(group => group.Min(entry => entry.SectionOrder)))
            {
                _settingsContentPanel.Children.Add(SettingRowFactory.CreateSectionCard(sectionGroup.First().SectionDisplayName, sectionGroup.ToList()));
            }
        }

        private string BuildGroupDescription(int count)
        {
            string query = _searchTextBox.Text?.Trim() ?? string.Empty;
            string format = string.IsNullOrWhiteSpace(query) ? SettingResources.CountFormat : SettingResources.MatchingCountFormat;
            return string.Format(CultureInfo.CurrentCulture, format, count);
        }
    }
}