using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace ColorVision.Engine.Utilities
{
    internal static class LocalizedResourceResolver
    {
        private static readonly Type DefaultResourceType = typeof(ColorVision.Engine.Properties.Resources);
        private static readonly ConcurrentDictionary<Type, Lazy<ResourceManager?>> ResourceManagers = new();
        private static readonly ConcurrentDictionary<(ResourceManager ResourceManager, string CultureName, string ResourceKey), string> LocalizedStrings = new();

        public static ResourceManager? GetResourceManager(Type? resourceType)
        {
            resourceType ??= DefaultResourceType;

            return ResourceManagers.GetOrAdd(resourceType, static type => new Lazy<ResourceManager?>(() => CreateResourceManager(type))).Value;
        }

        public static string GetString(ResourceManager? resourceManager, string resourceKey)
        {
            if (resourceManager == null || string.IsNullOrWhiteSpace(resourceKey))
            {
                return resourceKey;
            }

            CultureInfo culture = CultureInfo.CurrentUICulture;
            return LocalizedStrings.GetOrAdd((resourceManager, culture.Name, resourceKey), _ => GetStringCore(resourceManager, resourceKey, culture));
        }

        private static ResourceManager? CreateResourceManager(Type resourceType)
        {
            try
            {
                PropertyInfo? resourceManagerProperty = resourceType.GetProperty(nameof(ResourceManager), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (resourceManagerProperty?.GetValue(null) is ResourceManager resourceManager)
                {
                    return resourceManager;
                }
            }
            catch
            {
            }

            try
            {
                return new ResourceManager(resourceType);
            }
            catch
            {
                return null;
            }
        }

        private static string GetStringCore(ResourceManager resourceManager, string resourceKey, CultureInfo culture)
        {
            try
            {
                string? localizedString = resourceManager.GetString(resourceKey, culture);
                return string.IsNullOrEmpty(localizedString) ? resourceKey : localizedString;
            }
            catch
            {
                return resourceKey;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class LocalizedDisplayNameAttribute : DisplayNameAttribute
    {
        private readonly string _resourceKey;
        private readonly ResourceManager? _resourceManager;

        public string ResourceKey => _resourceKey;

        public LocalizedDisplayNameAttribute(string resourceKey)
            : this(resourceKey, null)
        {
        }

        public LocalizedDisplayNameAttribute(Type resourceType, string resourceKey)
            : this(resourceKey, resourceType)
        {
        }

        private LocalizedDisplayNameAttribute(string resourceKey, Type? resourceType)
            : base(resourceKey)
        {
            _resourceKey = resourceKey;
            _resourceManager = LocalizedResourceResolver.GetResourceManager(resourceType);
            DisplayNameValue = LocalizedResourceResolver.GetString(_resourceManager, resourceKey);
        }

        public override string DisplayName => LocalizedResourceResolver.GetString(_resourceManager, _resourceKey);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private readonly string _resourceKey;
        private readonly ResourceManager? _resourceManager;

        public string ResourceKey => _resourceKey;

        public LocalizedDescriptionAttribute(string resourceKey)
            : this(resourceKey, null)
        {
        }

        public LocalizedDescriptionAttribute(Type resourceType, string resourceKey)
            : this(resourceKey, resourceType)
        {
        }

        private LocalizedDescriptionAttribute(string resourceKey, Type? resourceType)
            : base(resourceKey)
        {
            _resourceKey = resourceKey;
            _resourceManager = LocalizedResourceResolver.GetResourceManager(resourceType);
            DescriptionValue = LocalizedResourceResolver.GetString(_resourceManager, resourceKey);
        }

        public override string Description => LocalizedResourceResolver.GetString(_resourceManager, _resourceKey);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class LocalizedCategoryAttribute : CategoryAttribute
    {
        private readonly string _resourceKey;
        private readonly ResourceManager? _resourceManager;

        public string ResourceKey => _resourceKey;

        public LocalizedCategoryAttribute(string resourceKey)
            : this(resourceKey, null)
        {
        }

        public LocalizedCategoryAttribute(Type resourceType, string resourceKey)
            : this(resourceKey, resourceType)
        {
        }

        private LocalizedCategoryAttribute(string resourceKey, Type? resourceType)
            : base(resourceKey)
        {
            _resourceKey = resourceKey;
            _resourceManager = LocalizedResourceResolver.GetResourceManager(resourceType);
        }

        protected override string GetLocalizedString(string value)
        {
            return LocalizedResourceResolver.GetString(_resourceManager, _resourceKey);
        }
    }
}
