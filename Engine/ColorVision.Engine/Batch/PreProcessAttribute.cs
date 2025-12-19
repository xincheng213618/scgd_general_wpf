using System;

namespace ColorVision.Engine.Batch
{
    /// <summary>
    /// Specifies metadata for an IPreProcess implementation to provide better user experience
    /// when selecting and managing pre-processors.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class PreProcessAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the display name of the pre-processor.
        /// This is the user-friendly name shown in the UI.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the description of the pre-processor.
        /// This helps users understand what the process does.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the category for grouping related pre-processors.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets the display order for sorting pre-processors.
        /// Lower values appear first.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Initializes a new instance of the PreProcessAttribute class.
        /// </summary>
        public PreProcessAttribute()
        {
            DisplayName = string.Empty;
            Description = string.Empty;
            Category = string.Empty;
            Order = 0;
        }

        /// <summary>
        /// Initializes a new instance of the PreProcessAttribute class with a display name.
        /// </summary>
        /// <param name="displayName">The display name of the pre-processor.</param>
        public PreProcessAttribute(string displayName)
        {
            DisplayName = displayName ?? string.Empty;
            Description = string.Empty;
            Category = string.Empty;
            Order = 0;
        }

        /// <summary>
        /// Initializes a new instance of the PreProcessAttribute class with a display name and description.
        /// </summary>
        /// <param name="displayName">The display name of the pre-processor.</param>
        /// <param name="description">The description of the pre-processor.</param>
        public PreProcessAttribute(string displayName, string description)
        {
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Category = string.Empty;
            Order = 0;
        }
    }
}
