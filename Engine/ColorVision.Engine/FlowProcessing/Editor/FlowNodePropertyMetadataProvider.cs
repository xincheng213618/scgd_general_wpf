using ColorVision.UI;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib.PropertyEditor;
using ST.Library.UI.NodeEditor;
using ST.Library.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.Engine.FlowProcessing.Editor
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
            nameof(FlowEngineLib.Base.CVBaseServerNode.ContinueOnFail),
        };

        public static PropertyEditorAdvancedOptions AdvancedOptions { get; } = new(IsAdvancedProperty)
        {
            ToolTip = Properties.Resources.Flow_ShowAdvancedPropertiesTooltip,
            ShowFirstCategoryHeader = false,
            ShowAdvancedToggleInCategoryHeader = false
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

            Type? nodeType = propertyInfo.ReflectedType;
            if (propertyInfo.Name == nameof(FlowEngineLib.Base.CVBaseServerNode.DeviceCode)
                && (nodeType == typeof(LocalBuildPoiNode) || nodeType == typeof(LocalBuildPoiByTemplateNode)))
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

        private static bool IsAdvancedProperty(PropertyInfo propertyInfo)
        {
            if (DefaultHiddenProperties.Contains(propertyInfo.Name))
            {
                return true;
            }

            Type? nodeType = propertyInfo.ReflectedType ?? propertyInfo.DeclaringType;
            return propertyInfo.Name == nameof(FlowEngineLib.Base.CVCommonNode.ZIndex)
                && IsLocalNodeType(nodeType);
        }

        private static bool IsLocalNodeType(Type? nodeType)
        {
            return nodeType != null
                && typeof(LocalFlowNodeBase).IsAssignableFrom(nodeType);
        }

        private static string? Localize(string? resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
                return resourceKey;

            return Lang.GetOrDefault(resourceKey);
        }
    }
}
