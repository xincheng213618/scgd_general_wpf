using ColorVision.UI;
using ColorVision.Engine.PropertyEditor;
using ST.Library.UI.NodeEditor;
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

        public static bool ShowDebugProperties { get; set; }

        private FlowNodePropertyMetadataProvider()
        {
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

            return ShowDebugProperties || !DefaultHiddenProperties.Contains(propertyInfo.Name);
        }

        public string? GetDisplayName(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<STNodePropertyAttribute>(inherit: true)?.Name;
        }

        public Type? GetEditorType(PropertyInfo propertyInfo)
        {
            return propertyInfo.Name.Equals("DeviceCode", StringComparison.OrdinalIgnoreCase)
                ? typeof(DeviceNameEditor)
                : null;
        }

        public string? GetDescription(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<STNodePropertyAttribute>(inherit: true)?.Description;
        }

        public string? GetCategory(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<CategoryAttribute>()?.Category;
        }
    }
}
