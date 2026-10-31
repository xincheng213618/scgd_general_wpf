using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ColorVision.UI
{
    /// <summary>
    /// Gives an <see cref="IConfig"/> a persisted section name that does not depend on its CLR name,
    /// so the type can be renamed or moved without losing stored settings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ConfigSectionAttribute : Attribute
    {
        public ConfigSectionAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Configuration section name must not be empty.", nameof(name));
            Name = name;
        }

        public string Name { get; }

        /// <summary>Earlier section names that are read when <see cref="Name"/> is absent, in priority order.</summary>
        public string[] Aliases { get; set; } = Array.Empty<string>();
    }

    public static class ConfigSectionNames
    {
        /// <summary>The section a configuration type is written to.</summary>
        public static string GetSectionName(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);
            return configType.GetCustomAttribute<ConfigSectionAttribute>(inherit: false)?.Name
                ?? configType.FullName
                ?? configType.Name;
        }

        /// <summary>
        /// Sections tried when reading, in priority order: the declared name, its aliases,
        /// the full type name and finally the legacy short type name.
        /// </summary>
        public static IReadOnlyList<string> GetReadCandidates(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);
            var attribute = configType.GetCustomAttribute<ConfigSectionAttribute>(inherit: false);
            IEnumerable<string?> names = attribute == null
                ? new[] { configType.FullName, configType.Name }
                : new[] { attribute.Name }.Concat(attribute.Aliases).Append(configType.FullName).Append(configType.Name);
            return names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}
