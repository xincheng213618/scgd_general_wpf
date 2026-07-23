using System;
using System.Reflection;

namespace ColorVision.UI
{
    /// <summary>
    /// Configures an optional advanced-property filter for a generated property editor.
    /// </summary>
    public sealed class PropertyEditorAdvancedOptions
    {
        public PropertyEditorAdvancedOptions(Predicate<PropertyInfo> isAdvancedProperty)
        {
            IsAdvancedProperty = isAdvancedProperty ?? throw new ArgumentNullException(nameof(isAdvancedProperty));
        }

        public Predicate<PropertyInfo> IsAdvancedProperty { get; }

        /// <summary>
        /// Gets or sets whether advanced properties are currently visible.
        /// Reuse the options instance to preserve this state when rebuilding an editor.
        /// </summary>
        public bool ShowAdvancedProperties { get; set; }

        public string ToolTip { get; set; } = "Advanced";

        /// <summary>
        /// Gets or sets whether categories are rendered directly in the editor instead of inside card borders.
        /// </summary>
        public bool UseIntegratedCategoryLayout { get; set; } = true;
    }
}
