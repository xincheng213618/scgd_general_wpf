using System;
namespace ColorVision.UI
{
    public static class ConfigSettingConstants
    {
        public const string Universal  = "Universal";
        public const string SectionBasic = "Basic";
        public const string SectionSearch = "Search";
        public const string SectionFileArchive = "FileArchive";
        public const string SectionAdvancedServices = "Services";
        public const string SectionLowLevelPaths = "Paths";
        public const string SectionExtensions = "Extensions";
        public const string SectionOther = "Other";

    }




    public class ConfigSettingMetadata
    {

        /// <summary>
        /// 如果需要变更顺序，可以通过Order来控制
        /// </summary>
        public int Order { get; set; } = 999;

        public string Group { get; set; } = ConfigSettingConstants.Universal;

        /// <summary>
        /// Display title shown in the setting row. If empty, the property display metadata is used.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description shown under the setting row title.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Section name used to group settings inside a navigation group.
        /// </summary>
        public string Section { get; set; } = string.Empty;

        public ConfigSettingType Type { get; set; } = ConfigSettingType.Property;
        /// <summary>
        /// Bool
        /// </summary>
        public string BindingName { get; set; }
        public object Source { get; set; }

        /// <summary>
        /// The type of the UserControl to lazily instantiate when the setting panel is displayed.
        /// </summary>
        public Type ViewType { get; set; }
    }
}
