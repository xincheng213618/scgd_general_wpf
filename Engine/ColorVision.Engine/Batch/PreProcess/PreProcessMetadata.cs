using System;
using System.Linq;

namespace ColorVision.Engine.Batch
{
    /// <summary>
    /// Provides metadata information for an IPreProcess implementation.
    /// </summary>
    public class PreProcessMetadata
    {
        /// <summary>
        /// Gets the display name of the pre-processor.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the description of the pre-processor.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the category of the pre-processor.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the display order of the pre-processor.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Gets the full type name of the pre-processor.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets the short type name of the pre-processor.
        /// </summary>
        public string ShortTypeName { get; }

        public PreProcessMetadata()
        {

        }

        /// <summary>
        /// Initializes a new instance of the PreProcessMetadata class.
        /// </summary>
        private PreProcessMetadata(string displayName, string description, string category, int order, string typeName, string shortTypeName)
        {
            DisplayName = displayName;
            Description = description;
            Category = category;
            Order = order;
            TypeName = typeName;
            ShortTypeName = shortTypeName;
        }

        /// <summary>
        /// Extracts metadata from an IPreProcess instance.
        /// </summary>
        /// <param name="process">The pre-processor instance.</param>
        /// <returns>A PreProcessMetadata instance containing the metadata.</returns>
        public static PreProcessMetadata FromProcess(IPreProcess process)
        {
            if (process == null)
                return new PreProcessMetadata(string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty);

            var type = process.GetType();
            return FromType(type);
        }

        /// <summary>
        /// Extracts metadata from a Type.
        /// </summary>
        /// <param name="type">The type implementing IPreProcess.</param>
        /// <returns>A PreProcessMetadata instance containing the metadata.</returns>
        public static PreProcessMetadata FromType(Type type)
        {
            if (type == null)
                return new PreProcessMetadata(string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty);

            var attribute = type.GetCustomAttributes(typeof(PreProcessAttribute), false)
                                .OfType<PreProcessAttribute>()
                                .FirstOrDefault();

            string displayName;
            string description;
            string category;
            int order;

            if (attribute != null)
            {
                displayName = !string.IsNullOrWhiteSpace(attribute.DisplayName) 
                    ? attribute.DisplayName 
                    : type.Name;
                description = attribute.Description ?? string.Empty;
                category = attribute.Category ?? string.Empty;
                order = attribute.Order;
            }
            else
            {
                // Fallback to type name if no attribute is present
                displayName = type.Name;
                description = string.Empty;
                category = string.Empty;
                order = 0;
            }

            return new PreProcessMetadata(
                displayName,
                description,
                category,
                order,
                type.FullName ?? type.Name,
                type.Name
            );
        }

        /// <summary>
        /// Gets the display text for the pre-processor, including description if available.
        /// </summary>
        public string GetDisplayText()
        {
            if (!string.IsNullOrWhiteSpace(Description))
                return $"{DisplayName} - {Description}";
            return DisplayName;
        }

        /// <summary>
        /// Gets a tooltip text for the pre-processor.
        /// </summary>
        public string GetTooltipText()
        {
            var text = DisplayName;
            if (!string.IsNullOrWhiteSpace(Description))
                text += $"\n\n{Description}";
            if (!string.IsNullOrWhiteSpace(Category))
                text += $"\n\n类别: {Category}";
            text += $"\n\n类型: {TypeName}";
            return text;
        }
    }
}
