using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal sealed class LocalCameraLicenseException : InvalidOperationException
{
    public LocalCameraLicenseException() : base(Properties.Resources.UnauthorizedOrCameraLicenseExpiredz) { }
}

internal static class LocalCameraLicense
{
    internal static void EnsureAvailable(DateTime? expiryDate, bool experimentalFeatures, DateTime now)
    {
        // Use the expiry shown by physical camera management; do not decode or validate the license payload.
        if (!experimentalFeatures && (!expiryDate.HasValue || expiryDate.Value <= now))
            throw new LocalCameraLicenseException();
    }
}
