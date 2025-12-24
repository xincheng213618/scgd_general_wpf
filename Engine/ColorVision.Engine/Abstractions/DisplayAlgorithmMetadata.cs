using System;
using System.Linq;

namespace ColorVision.Engine
{
    /// <summary>
    /// Provides metadata information for an IDisplayAlgorithm implementation.
    /// </summary>
    public class DisplayAlgorithmMetadata
    {
        /// <summary>
        /// Gets the display name of the display algorithm.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the group/category of the display algorithm.
        /// </summary>
        public string Group { get; }

        /// <summary>
        /// Gets the display order of the display algorithm.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Gets the full type name of the display algorithm.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets the short type name of the display algorithm.
        /// </summary>
        public string ShortTypeName { get; }

        public DisplayAlgorithmMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the DisplayAlgorithmMetadata class.
        /// </summary>
        private DisplayAlgorithmMetadata(string displayName, string group, int order, string typeName, string shortTypeName)
        {
            DisplayName = displayName;
            Group = group;
            Order = order;
            TypeName = typeName;
            ShortTypeName = shortTypeName;
        }

        /// <summary>
        /// Extracts metadata from an IDisplayAlgorithm instance.
        /// </summary>
        /// <param name="algorithm">The display algorithm instance.</param>
        /// <returns>A DisplayAlgorithmMetadata instance containing the metadata.</returns>
        public static DisplayAlgorithmMetadata FromAlgorithm(IDisplayAlgorithm algorithm)
        {
            if (algorithm == null)
                return new DisplayAlgorithmMetadata(string.Empty, string.Empty, 0, string.Empty, string.Empty);

            var type = algorithm.GetType();
            return FromType(type);
        }

        /// <summary>
        /// Extracts metadata from a Type.
        /// </summary>
        /// <param name="type">The type implementing IDisplayAlgorithm.</param>
        /// <returns>A DisplayAlgorithmMetadata instance containing the metadata.</returns>
        public static DisplayAlgorithmMetadata FromType(Type type)
        {
            if (type == null)
                return new DisplayAlgorithmMetadata(string.Empty, string.Empty, 0, string.Empty, string.Empty);

            var attribute = type.GetCustomAttributes(typeof(DisplayAlgorithmAttribute), false)
                                .OfType<DisplayAlgorithmAttribute>()
                                .FirstOrDefault();

            string displayName;
            string group;
            int order;

            if (attribute != null)
            {
                displayName = !string.IsNullOrWhiteSpace(attribute.Name) 
                    ? attribute.Name 
                    : type.Name;
                group = attribute.Group ?? string.Empty;
                order = attribute.Order;
            }
            else
            {
                // Fallback to type name if no attribute is present
                displayName = type.Name;
                group = string.Empty;
                order = 0;
            }

            return new DisplayAlgorithmMetadata(
                displayName,
                group,
                order,
                type.FullName ?? type.Name,
                type.Name
            );
        }

        /// <summary>
        /// Gets the display text for the display algorithm.
        /// </summary>
        public string GetDisplayText()
        {
            if (!string.IsNullOrWhiteSpace(Group))
                return $"{DisplayName} ({Group})";
            return DisplayName;
        }

        /// <summary>
        /// Gets a tooltip text for the display algorithm.
        /// </summary>
        public string GetTooltipText()
        {
            var text = DisplayName;
            if (!string.IsNullOrWhiteSpace(Group))
                text += $"\n\n分组: {Group}";
            text += $"\n\n类型: {TypeName}";
            return text;
        }
    }
}
