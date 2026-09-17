using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using System;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhySpectrums
{
    public sealed class PhySpectrum : ViewModelBase
    {
        public string SN { get; init; } = string.Empty;
        public int? ResourceId { get; init; }
        public LicenseModel? License { get; init; }
        public string DiscoverySource { get; init; } = string.Empty;
        public bool IsDiscovered => DiscoverySource.Length > 0;
        public bool IsRegistered => ResourceId.HasValue;
        public string DiscoveryText => IsDiscovered ? Properties.Resources.SpectrumDiscovered : Properties.Resources.SpectrumNotDiscovered;
        public string DisplayModel => string.IsNullOrWhiteSpace(License?.Model) ? Properties.Resources.Spectrometer : License.Model;
        public bool NeedsAttention => License == null || string.IsNullOrWhiteSpace(License.LicenseValue) || License.ExpiryDate == null || License.ExpiryDate <= DateTime.Now.AddDays(30);
        public string LicenseStatus => License == null || string.IsNullOrWhiteSpace(License.LicenseValue)
            ? Properties.Resources.LicenseStatusUnlicensed
            : License.ExpiryDate == null ? Properties.Resources.LicenseStatusInvalid
            : License.ExpiryDate <= DateTime.Now ? Properties.Resources.LicenseStatusExpired
            : NeedsAttention ? Properties.Resources.LicenseStatusExpiringSoon : Properties.Resources.LicenseStatusValid;
        public Brush LicenseBrush => NeedsAttention ? Brushes.DarkOrange : Brushes.SeaGreen;
        public string LicenseBadgeText => License?.ExpiryDate is DateTime expiry && !string.IsNullOrWhiteSpace(License.LicenseValue)
            ? expiry <= DateTime.Now ? string.Format(Properties.Resources.LicenseBadgeExpired, $"{expiry:yyyy-MM-dd}")
                : NeedsAttention ? string.Format(Properties.Resources.LicenseBadgeExpiringSoon, $"{expiry:yyyy-MM-dd}") : Properties.Resources.LicenseBadgeValid
            : LicenseStatus;
        public string LicenseDateRange => License?.ExpiryDate is DateTime expiry
            ? License.CreateDate is DateTime start ? $"· {start:yyyy-MM-dd} - {expiry:yyyy-MM-dd}" : $"· {expiry:yyyy-MM-dd}"
            : string.Empty;
    }
}
