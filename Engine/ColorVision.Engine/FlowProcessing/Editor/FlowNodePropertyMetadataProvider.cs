using ColorVision.UI;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib;
using ST.Library.UI.NodeEditor;
using ST.Library.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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

            Type? nodeType = GetNodeType(propertyInfo);
            if (propertyInfo.Name == nameof(FlowEngineLib.Base.CVCommonNode.NodeID))
            {
                return false;
            }

            if (IsLocalNodeType(nodeType)
                && propertyInfo.Name is nameof(FlowEngineLib.Base.CVCommonNode.NodeName)
                    or nameof(FlowEngineLib.Base.CVCommonNode.ZIndex))
            {
                return false;
            }

            if (IsLoopControlNodeType(nodeType)
                && propertyInfo.Name is nameof(FlowEngineLib.Base.CVDeviceNode.DeviceCode)
                    or nameof(FlowEngineLib.Base.CVCommonNode.ZIndex))
            {
                return false;
            }

            return true;
        }

        public string? GetDisplayName(PropertyInfo propertyInfo)
        {
            if (propertyInfo.Name == nameof(FlowEngineLib.Base.CVCommonNode.NodeName)
                && IsLoopControlNodeType(GetNodeType(propertyInfo)))
            {
                return Localize("循环名称");
            }

            return Localize(propertyInfo.GetCustomAttribute<STNodePropertyAttribute>(inherit: true)?.Name);
        }

        public Type? GetEditorType(PropertyInfo propertyInfo)
        {
            if (CameraCalibrationGainPropertiesEditor.IsSupported(propertyInfo))
                return typeof(CameraCalibrationGainPropertiesEditor);

            return null;
        }

        public string? GetDescription(PropertyInfo propertyInfo)
        {
            if (propertyInfo.Name == nameof(FlowEngineLib.Base.CVCommonNode.NodeName)
                && IsLoopControlNodeType(GetNodeType(propertyInfo)))
            {
                return Localize("循环名称说明");
            }

            return Localize(propertyInfo.GetCustomAttribute<STNodePropertyAttribute>(inherit: true)?.Description);
        }

        public string? GetCategory(PropertyInfo propertyInfo)
        {
            string? category = GetDeclaredCategory(propertyInfo);
            if (!string.IsNullOrWhiteSpace(category))
            {
                return Localize(category);
            }

            Type? nodeType = GetNodeType(propertyInfo);
            if (propertyInfo.Name == nameof(STNode.Title) && IsLocalNodeType(nodeType))
            {
                return Localize(GetPrimaryLocalNodeCategory(nodeType!));
            }

            return null;
        }

        private static string? GetPrimaryLocalNodeCategory(Type nodeType)
        {
            return nodeType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.Name != nameof(STNode.Title))
                .Where(property => property.GetCustomAttribute<STNodePropertyAttribute>(inherit: true) != null)
                .Where(property => property.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false)
                .OrderBy(property => property.GetCustomAttribute<DisplayAttribute>()?.GetOrder() ?? 0)
                .ThenByDescending(property => GetInheritanceDepth(property.DeclaringType))
                .Select(GetDeclaredCategory)
                .FirstOrDefault(category => !string.IsNullOrWhiteSpace(category));
        }

        private static string? GetDeclaredCategory(PropertyInfo propertyInfo)
        {
            string? category = propertyInfo.GetCustomAttribute<CategoryAttribute>()?.Category;
            if (!string.IsNullOrWhiteSpace(category))
            {
                return category;
            }

            try
            {
                return propertyInfo.GetCustomAttribute<DisplayAttribute>()?.GetGroupName();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static int GetInheritanceDepth(Type? type)
        {
            int depth = 0;
            while (type != null)
            {
                depth++;
                type = type.BaseType;
            }
            return depth;
        }

        private static bool IsAdvancedProperty(PropertyInfo propertyInfo)
        {
            Type? nodeType = GetNodeType(propertyInfo);
            if (propertyInfo.Name == nameof(FlowEngineLib.Base.CVCommonNode.NodeName)
                && IsLoopControlNodeType(nodeType))
            {
                return false;
            }

            if (DefaultHiddenProperties.Contains(propertyInfo.Name))
            {
                return true;
            }
            if (string.Equals(propertyInfo.GetCustomAttribute<CategoryAttribute>()?.Category, "高级", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return propertyInfo.Name == nameof(FlowEngineLib.Base.CVCommonNode.ZIndex)
                && IsLocalNodeType(nodeType);
        }

        private static Type? GetNodeType(PropertyInfo propertyInfo)
        {
            return propertyInfo.ReflectedType ?? propertyInfo.DeclaringType;
        }

        private static bool IsLoopControlNodeType(Type? nodeType)
        {
            return nodeType != null
                && (typeof(LoopNode).IsAssignableFrom(nodeType)
                    || typeof(LoopNextNode).IsAssignableFrom(nodeType));
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
