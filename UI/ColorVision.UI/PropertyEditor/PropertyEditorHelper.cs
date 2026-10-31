#pragma warning disable CA1707,CA1852,CS8601
using ColorVision.Themes;
using log4net;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.UI
{
    public interface IPropertyEditorMetadataProvider
    {
        bool IsPropertyManaged(PropertyInfo propertyInfo);
        bool IsBrowsable(PropertyInfo propertyInfo);
        Type? GetEditorType(PropertyInfo propertyInfo);
        string? GetDisplayName(PropertyInfo propertyInfo);
        string? GetDescription(PropertyInfo propertyInfo);
        string? GetCategory(PropertyInfo propertyInfo);
    }

    public static partial class PropertyEditorHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PropertyEditorHelper));
        private static readonly PropertyEditorRegistry EditorRegistry = new(Log);

        // Constants
        public const double LabelMinWidth = 120;
        public const double ControlMinWidth = 150;

        // Cache for resources and reflection results
        public static ConcurrentDictionary<Type, Lazy<ResourceManager?>> ResourceManagerCache { get; set; } = new();
        private static readonly ConcurrentDictionary<(ResourceManager ResourceManager, string CultureName, string Key), string> ResourceStringCache = new();
        public static ConcurrentDictionary<Type, IPropertyEditor> CustomEditorCache => EditorRegistry.Instances;
        private static readonly AsyncLocal<IPropertyEditorMetadataProvider?> MetadataProviderContext = new();

        static PropertyEditorHelper()
        {
            PropertyEditorBuiltIns.Register(EditorRegistry);
            try
            {
                var editorTypes = AssemblyHandler.Instance.GetImplementationTypes(typeof(IPropertyEditor))
                    .Where(type => type.Assembly != typeof(PropertyEditorHelper).Assembly);
                foreach (Type type in editorTypes)
                {
                    try
                    {
                        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to initialize property editor '{type.FullName}'.", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize property editors.", ex);
            }
        }

        public static void RegisterEditor<TEditor>(Type targetType) where TEditor : IPropertyEditor, new()
        {
            EditorRegistry.Register<TEditor>(targetType);
        }

        public static void RegisterEditor<TEditor>(Func<Type, bool> typePredicate) where TEditor : IPropertyEditor, new()
        {
            EditorRegistry.Register<TEditor>(typePredicate);
        }

        public static IPropertyEditor GetOrCreateEditor(Type editorType)
        {
            return EditorRegistry.GetOrCreate(editorType);
        }
        public static Type? GetEditorTypeForPropertyType(Type propertyType)
        {
            return EditorRegistry.Find(propertyType);
        }

        public static List<Type> GetAllEditorTypesForPropertyType(Type propertyType)
        {
            return EditorRegistry.FindAll(propertyType);
        }

        internal static bool HasEditorForProperty(PropertyInfo property)
        {
            var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
            return editorAttr?.EditorType != null || GetEditorTypeForPropertyType(property.PropertyType) != null;
        }

        public static PropertyInfo[] GetEditableProperties(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.CanWrite)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Where(property => property.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true)
                .ToArray();
        }

        public static bool CanEditProperty(PropertyInfo property)
        {
            ArgumentNullException.ThrowIfNull(property);
            return HasEditorForProperty(property) || CanGenerateNestedEditor(property.PropertyType);
        }

        public static T GetOrCreateEditor<T>() where T : IPropertyEditor, new()
        {
            return (T)EditorRegistry.GetOrCreate(typeof(T));
        }

        private static readonly Lazy<ResourceCache> Resources = new(() => new ResourceCache());

        private class ResourceCache
        {
            public Brush GlobalTextBrush { get; set; }
            public Brush GlobalBorderBrush { get; set; }
            public Brush BorderBrush { get; set; }
            public Style ButtonCommandStyle { get; set; }
            public Style ComboBoxSmallStyle { get; set; }
            public Style TextBoxSmallStyle { get; set; }
            public IValueConverter Bool2VisibilityConverter { get; set; }
            public IValueConverter Bool2VisibilityReConverter { get; set; }
            public IValueConverter Enum2VisibilityConverter { get; set; }
            public IValueConverter Enum2VisibilityReConverter { get; set; }

            public void SetResources()
            {
                var app = Application.Current ?? throw new InvalidOperationException(Properties.Resources.ApplicationCurrentNotInitialized);

                GlobalTextBrush = (Brush)app.FindResource("GlobalTextBrush");
                GlobalBorderBrush = (Brush)app.FindResource("GlobalBorderBrush");
                BorderBrush = (Brush)app.FindResource("BorderBrush");
                ButtonCommandStyle = (Style)app.FindResource("ButtonCommand");
                ComboBoxSmallStyle = (Style)app.FindResource("ComboBox.Small");
                TextBoxSmallStyle = (Style)app.FindResource("TextBox.Small");
                
                // Required converter
                Bool2VisibilityConverter = app.TryFindResource("bool2VisibilityConverter") as IValueConverter
                    ?? throw new InvalidOperationException(Properties.Resources.Bool2VisibilityConverterNotFound);
                
                // Optional converters (may not be present in all themes)
                Bool2VisibilityReConverter = app.TryFindResource("bool2VisibilityConverter1") as IValueConverter;
                Enum2VisibilityConverter = app.TryFindResource("enum2VisibilityConverter") as IValueConverter;
                Enum2VisibilityReConverter = app.TryFindResource("enum2VisibilityConverter1") as IValueConverter;
            }

            public ResourceCache()
            {
                SetResources();
                ThemeManager.Current.CurrentUIThemeChanged += (e) => SetResources();
            }
        }

        public static Brush GlobalTextBrush => Resources.Value.GlobalTextBrush;
        public static Brush GlobalBorderBrush => Resources.Value.GlobalBorderBrush;
        public static Brush BorderBrush => Resources.Value.BorderBrush;
        public static Style ButtonCommandStyle => Resources.Value.ButtonCommandStyle;
        public static Style ComboBoxSmallStyle => Resources.Value.ComboBoxSmallStyle;
        public static Style TextBoxSmallStyle => Resources.Value.TextBoxSmallStyle;
        public static IValueConverter Bool2VisibilityConverter => Resources.Value.Bool2VisibilityConverter;
        public static IValueConverter Bool2VisibilityReConverter => Resources.Value.Bool2VisibilityReConverter;
        public static IValueConverter Enum2VisibilityConverter => Resources.Value.Enum2VisibilityConverter;
        public static IValueConverter Enum2VisibilityReConverter => Resources.Value.Enum2VisibilityReConverter;


        public static ResourceManager? GetResourceManager(object obj, ResourceManager? resourceManager = null)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return GetResourceManager(obj.GetType(), resourceManager);
        }

        public static ResourceManager? GetResourceManager(Type type, ResourceManager? resourceManager = null)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (resourceManager != null)
            {
                ResourceManagerCache.AddOrUpdate(type, new Lazy<ResourceManager?>(() => resourceManager), (_, __) => new Lazy<ResourceManager?>(() => resourceManager));
                return resourceManager;
            }

            var lazyResourceManager = ResourceManagerCache.GetOrAdd(type, t => new Lazy<ResourceManager?>(() =>
            {
                try
                {
                    string namespaceName = t.Assembly.GetName().Name!;
                    string resourceClassName = $"{namespaceName}.Properties.Resources";
                    Type? resourceType = t.Assembly.GetType(resourceClassName);
                    if (resourceType != null)
                    {
                        var rmProp = resourceType.GetProperty(nameof(ResourceManager), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (rmProp?.GetValue(null) is ResourceManager rm)
                            return rm;
                    }
                }
                catch
                {
                    // ignore and fallback to null
                }
                return null;
            }));
            return lazyResourceManager.Value;
        }
    }
}
