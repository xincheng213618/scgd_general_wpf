using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;

namespace FlowEngineLib.PropertyEditor;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class FlowNodePropertyEditorAttribute : Attribute
{
    public string PropertyName { get; }
    public Type EditorType { get; }

    public FlowNodePropertyEditorAttribute(string propertyName, Type editorType)
    {
        PropertyName = propertyName;
        EditorType = editorType;
    }

    public static Type? Resolve(Type nodeType, string propertyName)
    {
        for (Type? current = nodeType; current != null; current = current.BaseType)
        {
            foreach (FlowNodePropertyEditorAttribute attr in current.GetCustomAttributes<FlowNodePropertyEditorAttribute>(inherit: false))
            {
                if (string.Equals(attr.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                    return attr.EditorType;
            }
        }
        return null;
    }
}

public static class FlowPropertyEditorRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<PropertyInfo, object, DockPanel>> Editors = new();

    public static void Register<TEditor>(Func<PropertyInfo, object, DockPanel> factory) where TEditor : IPropertyEditor
    {
        Editors[typeof(TEditor)] = factory;
    }

    public static bool TryCreate(Type editorType, PropertyInfo property, object obj, out DockPanel dockPanel)
    {
        if (Editors.TryGetValue(editorType, out var factory))
        {
            dockPanel = factory(property, obj);
            return true;
        }

        dockPanel = null!;
        return false;
    }
}

public abstract class FlowPropertyEditorProxy : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        if (FlowPropertyEditorRegistry.TryCreate(GetType(), property, obj, out var dockPanel))
            return dockPanel;

        return new TextboxPropertiesEditor().GenProperties(property, obj);
    }
}

public sealed class FlowNodePropertyEditorSelector : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        Type? editorType = FlowNodePropertyEditorAttribute.Resolve(obj.GetType(), property.Name);
        if (editorType != null && typeof(IPropertyEditor).IsAssignableFrom(editorType) && Activator.CreateInstance(editorType) is IPropertyEditor editor)
            return editor.GenProperties(property, obj);

        return new TextboxPropertiesEditor().GenProperties(property, obj);
    }
}

public sealed class FlowDeviceNameEditor : FlowPropertyEditorProxy { }
public sealed class FlowCalibrationTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowAutoExposureTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowCameraRunTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowAutoFocusTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowPoiTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowPoiFilterTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowPoiReviseTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowPoiOutputTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowPoiGenCaliTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowSmuTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowSmuRangeEditor : FlowPropertyEditorProxy { }
public sealed class FlowSensorTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowDataLoadTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowBlackMuraJsonTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowImageRoiJsonTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowPoiAnalysisJsonTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowImageCroppingTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowLedCheck2JsonTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowOledAoiJsonTemplateEditor : FlowPropertyEditorProxy { }
public sealed class FlowKbJsonTemplateEditor : FlowPropertyEditorProxy { }
