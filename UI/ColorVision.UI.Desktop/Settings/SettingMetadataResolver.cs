using ColorVision.UI.Properties;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Resources;
using System.Text;

namespace ColorVision.UI.Desktop.Settings
{
    internal sealed class SettingMetadataResolver
    {
        public static SettingEntry CreateEntry(ConfigSettingMetadata metadata, PropertyInfo? propertyInfo)
        {
            string title = ResolveTitle(metadata, propertyInfo);
            string description = ResolveDescription(metadata, propertyInfo);
            string group = ResolveNavigationGroup(metadata, title);
            string groupDisplayName = ResolveGroupDisplayName(metadata, group, title);
            string sectionKey = ResolveSectionKey(metadata);
            string sectionDisplayName = ResolveSectionDisplayName(sectionKey);
            string searchText = BuildSearchText(metadata, propertyInfo, title, description, group, groupDisplayName, sectionKey, sectionDisplayName);

            return new SettingEntry
            {
                Metadata = metadata,
                PropertyInfo = propertyInfo,
                Group = group,
                GroupDisplayName = groupDisplayName,
                SectionKey = sectionKey,
                SectionDisplayName = sectionDisplayName,
                SectionOrder = GetSectionOrder(sectionKey),
                Title = title,
                Description = description,
                SearchText = searchText
            };
        }

        public static string ResolveGroupDisplayName(string group)
        {
            if (string.Equals(group, ConfigSettingConstants.Universal, StringComparison.OrdinalIgnoreCase))
            {
                return Resources.GeneralSettings;
            }

            string? localized = TryGetResourceText(Resources.ResourceManager, group);
            return localized ?? ToFriendlyName(group);
        }

        public static string ResolveSectionDisplayName(string sectionKey)
        {
            string? sectionName = NormalizeLegacySectionKey(sectionKey) switch
            {
                ConfigSettingConstants.SectionBasic => SettingResources.SectionBasic,
                ConfigSettingConstants.SectionSearch => SettingResources.SectionSearch,
                ConfigSettingConstants.SectionFileArchive => SettingResources.SectionFileArchive,
                ConfigSettingConstants.SectionAdvancedServices => SettingResources.SectionServices,
                ConfigSettingConstants.SectionLowLevelPaths => SettingResources.SectionPaths,
                ConfigSettingConstants.SectionExtensions => SettingResources.SectionExtensions,
                ConfigSettingConstants.SectionOther => SettingResources.SectionOther,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(sectionName)) return sectionName;

            return ToFriendlyName(sectionKey);
        }

        public static int GetSectionOrder(string sectionKey)
        {
            return NormalizeLegacySectionKey(sectionKey) switch
            {
                ConfigSettingConstants.SectionBasic => 0,
                ConfigSettingConstants.SectionSearch => 10,
                ConfigSettingConstants.SectionFileArchive => 20,
                ConfigSettingConstants.SectionAdvancedServices => 80,
                ConfigSettingConstants.SectionLowLevelPaths => 85,
                ConfigSettingConstants.SectionExtensions => 90,
                ConfigSettingConstants.SectionOther => 100,
                _ => 200
            };
        }

        private static string ResolveTitle(ConfigSettingMetadata metadata, PropertyInfo? propertyInfo)
        {
            string? title = ResolveResourceText(metadata.Name, metadata.Source);
            ResourceManager? resourceManager = metadata.Source == null ? null : PropertyEditorHelper.GetResourceManager(metadata.Source);

            if (string.IsNullOrWhiteSpace(title) && propertyInfo != null)
            {
                title = GetDisplayAttributeName(propertyInfo);
            }

            if (string.IsNullOrWhiteSpace(title) && propertyInfo != null)
            {
                title = ResolveDisplayNameAttribute(propertyInfo, resourceManager);
            }

            if (string.IsNullOrWhiteSpace(title) && propertyInfo != null)
            {
                title = TryGetResourceText(resourceManager, propertyInfo.Name);
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = NormalizeText(metadata.BindingName) ?? metadata.Type.ToString();
            }

            return ToFriendlyName(title);
        }

        private static string ResolveDescription(ConfigSettingMetadata metadata, PropertyInfo? propertyInfo)
        {
            string? description = ResolveResourceText(metadata.Description, metadata.Source);
            ResourceManager? resourceManager = metadata.Source == null ? null : PropertyEditorHelper.GetResourceManager(metadata.Source);

            if (string.IsNullOrWhiteSpace(description) && propertyInfo != null)
            {
                description = ResolveDescriptionAttribute(propertyInfo, resourceManager);
            }

            if (string.IsNullOrWhiteSpace(description) && propertyInfo != null)
            {
                description = GetDisplayAttributeDescription(propertyInfo);
            }

            return description ?? string.Empty;
        }

        private static string ResolveNavigationGroup(ConfigSettingMetadata metadata, string title)
        {
            if (metadata.Type != ConfigSettingType.Property && !string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return NormalizeText(metadata.Group) ?? ConfigSettingConstants.Universal;
        }

        private static string ResolveGroupDisplayName(ConfigSettingMetadata metadata, string group, string title)
        {
            if (metadata.Type != ConfigSettingType.Property && !string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return ResolveGroupDisplayName(group);
        }

        private static string ResolveSectionKey(ConfigSettingMetadata metadata)
        {
            string? section = NormalizeText(metadata.Section);
            if (!string.IsNullOrWhiteSpace(section)) return NormalizeLegacySectionKey(section);

            return metadata.Type == ConfigSettingType.Property ? ConfigSettingConstants.SectionOther : ConfigSettingConstants.SectionExtensions;
        }

        private static string BuildSearchText(ConfigSettingMetadata metadata, PropertyInfo? propertyInfo, string title, string description, string group, string groupDisplayName, string sectionKey, string sectionDisplayName)
        {
            var parts = new List<string?>
            {
                title,
                description,
                group,
                groupDisplayName,
                sectionKey,
                sectionDisplayName,
                metadata.Name,
                metadata.Description,
                metadata.Section,
                metadata.BindingName,
                metadata.Type.ToString(),
                metadata.ViewType?.Name,
                metadata.Source?.GetType().Name,
                propertyInfo?.Name,
                propertyInfo?.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName,
                propertyInfo?.GetCustomAttribute<DescriptionAttribute>()?.Description,
                GetDisplayAttributeName(propertyInfo),
                GetDisplayAttributeDescription(propertyInfo)
            };

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).ToLowerInvariant();
        }

        private static string NormalizeLegacySectionKey(string sectionKey)
        {
            return sectionKey switch
            {
                "基础" => ConfigSettingConstants.SectionBasic,
                "搜索" => ConfigSettingConstants.SectionSearch,
                "文件归档" => ConfigSettingConstants.SectionFileArchive,
                "高级服务" => ConfigSettingConstants.SectionAdvancedServices,
                "底层路径" => ConfigSettingConstants.SectionLowLevelPaths,
                "扩展页面" => ConfigSettingConstants.SectionExtensions,
                "其他" => ConfigSettingConstants.SectionOther,
                _ => sectionKey
            };
        }

        private static string? ResolveResourceText(string? value, object? source)
        {
            string? text = NormalizeText(value);
            if (string.IsNullOrWhiteSpace(text)) return null;

            ResourceManager? resourceManager = source == null ? null : PropertyEditorHelper.GetResourceManager(source);
            string? localized = TryGetResourceText(resourceManager, text);
            if (!string.IsNullOrWhiteSpace(localized)) return localized;

            return ShouldUseRawText(text) ? text : null;
        }

        private static string? ResolveDisplayNameAttribute(PropertyInfo propertyInfo, ResourceManager? resourceManager)
        {
            string? displayName = NormalizeText(propertyInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName);
            if (string.IsNullOrWhiteSpace(displayName)) return null;

            string? localized = TryGetResourceText(resourceManager, displayName);
            if (!string.IsNullOrWhiteSpace(localized)) return localized;

            return ShouldUseRawText(displayName) ? displayName : null;
        }

        private static string? ResolveDescriptionAttribute(PropertyInfo propertyInfo, ResourceManager? resourceManager)
        {
            string? description = NormalizeText(propertyInfo.GetCustomAttribute<DescriptionAttribute>()?.Description);
            if (string.IsNullOrWhiteSpace(description)) return null;

            string? localized = TryGetResourceText(resourceManager, description);
            if (!string.IsNullOrWhiteSpace(localized)) return localized;

            return ShouldUseRawText(description) ? description : null;
        }

        private static string? GetDisplayAttributeName(PropertyInfo? propertyInfo)
        {
            var displayAttribute = propertyInfo?.GetCustomAttribute<DisplayAttribute>();
            if (displayAttribute == null) return null;

            try
            {
                return NormalizeText(displayAttribute.GetName());
            }
            catch
            {
                string? raw = NormalizeText(displayAttribute.Name);
                return ShouldUseRawText(raw) ? raw : null;
            }
        }

        private static string? GetDisplayAttributeDescription(PropertyInfo? propertyInfo)
        {
            var displayAttribute = propertyInfo?.GetCustomAttribute<DisplayAttribute>();
            if (displayAttribute == null) return null;

            try
            {
                return NormalizeText(displayAttribute.GetDescription());
            }
            catch
            {
                string? raw = NormalizeText(displayAttribute.Description);
                return ShouldUseRawText(raw) ? raw : null;
            }
        }

        private static string? TryGetResourceText(ResourceManager? resourceManager, string? key)
        {
            if (resourceManager == null || string.IsNullOrWhiteSpace(key)) return null;

            try
            {
                string? value = resourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture);
                return NormalizeText(value);
            }
            catch
            {
                return null;
            }
        }

        private static bool ShouldUseRawText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (!ContainsCjk(text)) return true;

            string cultureName = Thread.CurrentThread.CurrentUICulture.Name;
            return cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsCjk(string text)
        {
            return text.Any(ch => ch >= '\u4e00' && ch <= '\u9fff');
        }

        private static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string ToFriendlyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Any(ch => ch > 127) || value.Contains(' ')) return value;

            string normalized = value.Replace('_', ' ').Replace('-', ' ').Trim();
            var builder = new StringBuilder(normalized.Length + 8);
            for (int i = 0; i < normalized.Length; i++)
            {
                char current = normalized[i];
                char previous = i > 0 ? normalized[i - 1] : '\0';
                char next = i + 1 < normalized.Length ? normalized[i + 1] : '\0';

                if (i > 0 && current != ' ' && previous != ' ' && char.IsUpper(current)
                    && (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && char.IsLower(next))))
                {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            return builder.ToString()
                .Replace(" Url", " URL", StringComparison.Ordinal)
                .Replace(" Url ", " URL ", StringComparison.Ordinal)
                .Replace(" Mb", " MB", StringComparison.Ordinal)
                .Replace(" Id", " ID", StringComparison.Ordinal)
                .Replace(" Rpc", " RPC", StringComparison.Ordinal);
        }
    }
}