using System.ComponentModel.DataAnnotations;

namespace ColorVision.PluginMarketplace.Models
{
    public class Plugin
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Unique plugin identifier (e.g., "Spectrum", "EventVWR")
        /// Matches manifest.json "id" field
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string PluginId { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(256)]
        public string? Author { get; set; }

        [MaxLength(512)]
        public string? Url { get; set; }

        [MaxLength(64)]
        public string? Category { get; set; }

        /// <summary>
        /// Icon file relative path in storage
        /// </summary>
        [MaxLength(512)]
        public string? IconPath { get; set; }

        /// <summary>
        /// README content (markdown)
        /// </summary>
        public string? Readme { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public long TotalDownloads { get; set; }

        /// <summary>
        /// Whether the plugin is published and visible in the marketplace
        /// </summary>
        public bool IsPublished { get; set; } = true;

        public ICollection<PluginVersion> Versions { get; set; } = new List<PluginVersion>();
    }
}
