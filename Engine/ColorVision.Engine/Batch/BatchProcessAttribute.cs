using System;

namespace ColorVision.Engine.Batch
{
    /// <summary>
    /// Specifies metadata for an IBatchProcess implementation to provide better user experience
    /// when selecting and managing batch processes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class BatchProcessAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the display name of the batch process.
        /// This is the user-friendly name shown in the UI.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the description of the batch process.
        /// This helps users understand what the process does.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the category for grouping related batch processes.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets the display order for sorting batch processes.
        /// Lower values appear first.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Initializes a new instance of the BatchProcessAttribute class.
        /// </summary>
        public BatchProcessAttribute()
        {
            DisplayName = string.Empty;
            Description = string.Empty;
            Category = string.Empty;
            Order = 0;
        }

        /// <summary>
        /// Initializes a new instance of the BatchProcessAttribute class with a display name.
        /// </summary>
        /// <param name="displayName">The display name of the batch process.</param>
        public BatchProcessAttribute(string displayName)
        {
            DisplayName = displayName ?? string.Empty;
            Description = string.Empty;
            Category = string.Empty;
            Order = 0;
        }

        /// <summary>
        /// Initializes a new instance of the BatchProcessAttribute class with a display name and description.
        /// </summary>
        /// <param name="displayName">The display name of the batch process.</param>
        /// <param name="description">The description of the batch process.</param>
        public BatchProcessAttribute(string displayName, string description)
        {
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Category = string.Empty;
            Order = 0;
        }
    }
}
