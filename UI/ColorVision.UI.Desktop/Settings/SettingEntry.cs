using System.Reflection;
using System.Windows;

namespace ColorVision.UI.Desktop.Settings
{
    internal sealed class SettingEntry
    {
        public ConfigSettingMetadata Metadata { get; set; } = null!;
        public PropertyInfo? PropertyInfo { get; set; }
        public string Group { get; set; } = string.Empty;
        public string GroupDisplayName { get; set; } = string.Empty;
        public string SectionKey { get; set; } = string.Empty;
        public string SectionDisplayName { get; set; } = string.Empty;
        public int SectionOrder { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public FrameworkElement? CustomContent { get; set; }
    }
}