#pragma warning disable CA1707,CA1852,CS8601
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

        // Resolve only the requested resource so a partial or newly replaced theme can recover.
        private static Application ResourceApplication => Application.Current
            ?? throw new InvalidOperationException(Properties.Resources.ApplicationCurrentNotInitialized);

        public static Brush GlobalTextBrush => (Brush)ResourceApplication.FindResource("GlobalTextBrush");
        public static Brush GlobalBorderBrush => (Brush)ResourceApplication.FindResource("GlobalBorderBrush");
        public static Brush BorderBrush => (Brush)ResourceApplication.FindResource("BorderBrush");
        public static Style ButtonCommandStyle => (Style)ResourceApplication.FindResource("ButtonCommand");
        public static Style ComboBoxSmallStyle => (Style)ResourceApplication.FindResource("ComboBox.Small");
        public static Style TextBoxSmallStyle => (Style)ResourceApplication.FindResource("TextBox.Small");
        public static IValueConverter Bool2VisibilityConverter => ResourceApplication.TryFindResource("bool2VisibilityConverter") as IValueConverter
            ?? throw new InvalidOperationException(Properties.Resources.Bool2VisibilityConverterNotFound);
        public static IValueConverter? Bool2VisibilityReConverter => ResourceApplication.TryFindResource("bool2VisibilityConverter1") as IValueConverter;
        public static IValueConverter? Enum2VisibilityConverter => ResourceApplication.TryFindResource("enum2VisibilityConverter") as IValueConverter;
        public static IValueConverter? Enum2VisibilityReConverter => ResourceApplication.TryFindResource("enum2VisibilityConverter1") as IValueConverter;


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
