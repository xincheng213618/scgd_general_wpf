using System;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.PostProcess
{
    /// <summary>
    /// Provides metadata information for an IPostProcessor implementation.
    /// </summary>
    public class PostProcessMetadata
    {
        /// <summary>
        /// Gets the display name of the batch process.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the description of the batch process.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the category of the batch process.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the display order of the batch process.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Gets the full type name of the batch process.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets the short type name of the batch process.
        /// </summary>
        public string ShortTypeName { get; }


        public PostProcessMetadata()
        {

        }
        /// <summary>
        /// Initializes a new instance of the PostProcessMetadata class.
        /// </summary>
        private PostProcessMetadata(string displayName, string description, string category, int order, string typeName, string shortTypeName)
        {
            DisplayName = displayName;
            Description = description;
            Category = category;
            Order = order;
            TypeName = typeName;
            ShortTypeName = shortTypeName;
        }

        /// <summary>
        /// Extracts metadata from an IPostProcessor instance.
        /// </summary>
        /// <param name="process">The batch process instance.</param>
        /// <returns>A PostProcessMetadata instance containing the metadata.</returns>
        public static PostProcessMetadata FromProcess(IPostProcessor process)
        {
            if (process == null)
                return new PostProcessMetadata(string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty);

            var type = process.GetType();
            return FromType(type);
        }

        /// <summary>
        /// Extracts metadata from a Type.
        /// </summary>
        /// <param name="type">The type implementing IPostProcessor.</param>
        /// <returns>A PostProcessMetadata instance containing the metadata.</returns>
        public static PostProcessMetadata FromType(Type type)
        {
            if (type == null)
                return new PostProcessMetadata(string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty);

            var attribute = type.GetCustomAttributes(typeof(PostProcessAttribute), false)
                                .OfType<PostProcessAttribute>()
                                .FirstOrDefault();

            string displayName;
            string description;
            string category;
            int order;

            if (attribute != null)
            {
                displayName = !string.IsNullOrWhiteSpace(attribute.DisplayName) 
                    ? EngineLocalization.Get(attribute.DisplayName)
                    : type.Name;
                description = EngineLocalization.Get(attribute.Description ?? string.Empty);
                category = EngineLocalization.Get(attribute.Category ?? string.Empty);
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

            return new PostProcessMetadata(
                displayName,
                description,
                category,
                order,
                type.FullName ?? type.Name,
                type.Name
            );
        }

        /// <summary>
        /// Gets the display text for the batch process, including description if available.
        /// </summary>
        public string GetDisplayText()
        {
            if (!string.IsNullOrWhiteSpace(Description))
                return $"{DisplayName} - {Description}";
            return DisplayName;
        }

        /// <summary>
        /// Gets a tooltip text for the batch process.
        /// </summary>
        public string GetTooltipText()
        {
            var text = DisplayName;
            if (!string.IsNullOrWhiteSpace(Description))
                text += $"\n\n{Description}";
            if (!string.IsNullOrWhiteSpace(Category))
                text += $"\n\n{EngineLocalization.Get("类别")}: {Category}";
            text += $"\n\n{EngineLocalization.Get("类型")}: {TypeName}";
            return text;
        }
    }
}
