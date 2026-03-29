using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ColorVision.PluginMarketplace.Models
{
    public class DownloadRecord
    {
        [Key]
        public long Id { get; set; }

        public int PluginVersionId { get; set; }

        [ForeignKey(nameof(PluginVersionId))]
        public PluginVersion PluginVersion { get; set; } = null!;

        /// <summary>
        /// Client IP address (hashed for privacy)
        /// </summary>
        [MaxLength(64)]
        public string? ClientIpHash { get; set; }

        /// <summary>
        /// Client application version
        /// </summary>
        [MaxLength(32)]
        public string? ClientVersion { get; set; }

        public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    }
}
