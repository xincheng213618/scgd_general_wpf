using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.Engine.Templates.Flow
{
    internal sealed class FlowNodePropertyMetadataProvider : IPropertyEditorMetadataProvider
    {
        public static FlowNodePropertyMetadataProvider Instance { get; } = new();

        private FlowNodePropertyMetadataProvider()
        {
        }

        public bool IsPropertyManaged(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<STNodePropertyAttribute>(inherit: true) != null;
        }

        public bool IsBrowsable(PropertyInfo propertyInfo)
        {
            return true;
        }

        public string? GetDisplayName(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<STNodePropertyAttribute>(inherit: true)?.Name;
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
