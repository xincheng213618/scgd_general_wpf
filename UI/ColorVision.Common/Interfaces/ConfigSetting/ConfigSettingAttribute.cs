using System;

namespace ColorVision.UI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class ConfigSettingAttribute : Attribute
    {
        public string Name { get; set; }
        public string Group { get; set; } = ConfigSettingConstants.Universal;
        public int Order { get; set; } = 999;
        public string Description { get; set; }
        public ConfigSettingType Type { get; set; } = ConfigSettingType.Property;

        /// <summary>
        /// The type of the UserControl to be lazily instantiated when the setting is displayed.
        /// </summary>
        public Type ViewType { get; set; }

        public ConfigSettingAttribute(string name)
        {
            Name = name;
        }

        public ConfigSettingAttribute() { }
    }
}
