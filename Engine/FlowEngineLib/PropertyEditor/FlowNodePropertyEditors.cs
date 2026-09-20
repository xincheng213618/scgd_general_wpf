using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;

namespace FlowEngineLib.PropertyEditor;

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

public sealed class FlowDeviceNameEditor : FlowPropertyEditorProxy { }
