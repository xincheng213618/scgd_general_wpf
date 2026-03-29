using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ColorVision.PluginMarketplace.Models
{
    public class PluginVersion
    {
        [Key]
        public int Id { get; set; }

        public int PluginId { get; set; }

        [ForeignKey(nameof(PluginId))]
        public Plugin Plugin { get; set; } = null!;

        /// <summary>
        /// Version string (e.g., "1.3.15.8")
        /// </summary>
        [Required]
        [MaxLength(32)]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Minimum required ColorVision engine version
        /// </summary>
        [MaxLength(32)]
        public string? RequiresVersion { get; set; }

        /// <summary>
        /// Changelog for this version (markdown)
        /// </summary>
        public string? ChangeLog { get; set; }

        /// <summary>
        /// Relative path to the .cvxp package file in storage
        /// </summary>
        [MaxLength(512)]
        public string? PackagePath { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// SHA256 hash of the package file for integrity verification
        /// </summary>
        [MaxLength(64)]
        public string? FileHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public long DownloadCount { get; set; }
    }
}
