using ColorVision.UI.LogImp;
using log4net.Core;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI;

internal static class PropertyEditorBuiltIns
{
    public static void Register(PropertyEditorRegistry registry)
    {
        registry.Register<TextboxPropertiesEditor>(TextboxPropertiesEditor.IsSupportedType);
        registry.Register<BoolPropertiesEditor>(type => Unwrap(type) == typeof(bool));
        registry.Register<EnumPropertiesEditor>(type => Unwrap(type).IsEnum);
        registry.Register<TemporalPropertiesEditor>(TemporalPropertiesEditor.IsSupportedType);
        registry.Register<CollectionJsonEditor>(CollectionJsonEditor.IsSupportedType);
        registry.Register<DictionaryJsonEditor>(DictionaryJsonEditor.IsSupportedType);

        registry.Register<PointPropertiesEditor>(type => Unwrap(type) == typeof(Point));
        registry.Register<RectPropertiesEditor>(type => Unwrap(type) == typeof(Rect));
        registry.Register<Int32RectPropertiesEditor>(type => Unwrap(type) == typeof(Int32Rect));
        registry.Register<SizePropertiesEditor>(type => Unwrap(type) == typeof(Size));
        registry.Register<ThicknessPropertiesEditor>(type => Unwrap(type) == typeof(Thickness));

        registry.Register<BrushesPropertiesEditor>(type => typeof(Brush).IsAssignableFrom(type) || type == typeof(Color));
        registry.Register<CommandPropertiesEditor>(type => typeof(ICommand).IsAssignableFrom(type));
        registry.Register<LevelPropertiesEditor>(type => typeof(Level).IsAssignableFrom(type));

        registry.Register<FontFamilyPropertiesEditor>(typeof(FontFamily));
        registry.Register<FontStretchPropertiesEditor>(typeof(FontStretch));
        registry.Register<FontStylePropertiesEditor>(typeof(FontStyle));
        registry.Register<FontWeightPropertiesEditor>(typeof(FontWeight));
    }

    private static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;
}
