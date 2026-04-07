using System;

namespace ColorVision.UI
{
    /// <summary>
    /// Marks a class or property as a configuration setting entry that will be
    /// automatically discovered and shown in the settings window.
    /// <para>
    /// When applied to a <b>class</b>, the class itself is registered as a settings panel
    /// (using <see cref="ViewType"/> for lazy UI instantiation, or auto-generated property editor
    /// when <see cref="ViewType"/> is null).
    /// </para>
    /// <para>
    /// When applied to a <b>property</b>, the property is intended to be picked up by
    /// future property-level discovery logic (reserved for extensibility).
    /// </para>
    /// </summary>
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
        /// Must be a type that derives from <see cref="System.Windows.Controls.UserControl"/>.
        /// </summary>
        public Type ViewType { get; set; }

        public ConfigSettingAttribute(string name)
        {
            Name = name;
        }

        public ConfigSettingAttribute() { }
    }
}
