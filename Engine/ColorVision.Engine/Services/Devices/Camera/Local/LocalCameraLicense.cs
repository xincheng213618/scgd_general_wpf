using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal sealed class LocalCameraLicenseException : InvalidOperationException
{
    internal LocalCameraLicenseException(bool expired) : base(expired
        ? Properties.Resources.LicenseListTagExpired : Properties.Resources.DeviceStatusUnauthorized) { }
}

internal static class LocalCameraLicense
{
    internal static void EnsureAvailable(DateTime? expiryDate, bool skipValidation, DateTime now)
    {
        // Use the expiry shown by physical camera management; do not decode or validate the license payload.
        if (!skipValidation && (!expiryDate.HasValue || expiryDate.Value <= now))
            throw new LocalCameraLicenseException(expiryDate.HasValue);
    }
}
