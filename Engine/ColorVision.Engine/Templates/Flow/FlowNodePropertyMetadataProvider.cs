using ColorVision.UI;
using ColorVision.Engine.PropertyEditor;
using FlowEngineLib.PropertyEditor;
using ST.Library.UI.NodeEditor;
using ST.Library.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.Engine.Templates.Flow
{
    internal sealed class FlowNodePropertyMetadataProvider : IPropertyEditorMetadataProvider
    {
        public static FlowNodePropertyMetadataProvider Instance { get; } = new();

        private static readonly HashSet<string> DefaultHiddenProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "NodeName",
            "NodeID",
            "NodeType",
            "Token",
        };

        public static PropertyEditorAdvancedOptions AdvancedOptions { get; } = new(propertyInfo => DefaultHiddenProperties.Contains(propertyInfo.Name))
        {
            ToolTip = Properties.Resources.Flow_ShowAdvancedPropertiesTooltip
        };

        private FlowNodePropertyMetadataProvider()
        {
            FlowNodePropertyEditorRegistration.EnsureRegistered();
        }

        public bool IsPropertyManaged(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<STNodePropertyAttribute>(inherit: true) != null;
        }

        public bool IsBrowsable(PropertyInfo propertyInfo)
        {
            if (propertyInfo.GetCustomAttribute<BrowsableAttribute>()?.Browsable == false)
            {
                return false;
            }

            return true;
        }

        public string? GetDisplayName(PropertyInfo propertyInfo)
        {
            return Localize(propertyInfo.GetCustomAttribute<STNodePropertyAttribute>(inherit: true)?.Name);
        }

        public Type? GetEditorType(PropertyInfo propertyInfo)
        {
            var nodeType = propertyInfo.ReflectedType ?? propertyInfo.DeclaringType;
            if (nodeType != null && FlowNodePropertyEditorAttribute.Resolve(nodeType, propertyInfo.Name) != null)
                return typeof(FlowNodePropertyEditorSelector);

            return null;
        }

        public string? GetDescription(PropertyInfo propertyInfo)
        {
            return Localize(propertyInfo.GetCustomAttribute<STNodePropertyAttribute>(inherit: true)?.Description);
        }

        public string? GetCategory(PropertyInfo propertyInfo)
        {
            return Localize(propertyInfo.GetCustomAttribute<CategoryAttribute>()?.Category);
        }

        private static string? Localize(string? resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
                return resourceKey;

            return Lang.GetOrDefault(resourceKey);
        }
    }
}
