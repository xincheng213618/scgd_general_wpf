namespace ColorVision.Rbac.Activation;

internal enum ActivationMode
{
    Offline,
    Online
}

internal sealed record ActivationDevice(
    string DeviceId, string DeviceName, ActivationMode Mode,
    DateTimeOffset ActivatedAt, DateTimeOffset LastSeenAt, DateTimeOffset ExpiresAt);

internal sealed record ActivationState
{
    public string? LicenseCode { get; init; }
    public DateTimeOffset? LicenseExpiresAt { get; init; }
    public int DeviceLimit { get; init; }
    public string? CurrentDeviceId { get; init; }
    public IReadOnlyList<ActivationDevice> Devices { get; init; } = [];
    public ActivationMode? EnrollmentMode { get; init; }
    public DateTimeOffset? OfflineExpiresAt { get; init; }
    public bool IsOfflineSnapshot { get; init; }
    public bool IsServerAvailable { get; init; }
    public string? UnavailableReason { get; init; }
}

internal sealed class ActivationException(string code, Exception? inner = null) : Exception(code, inner)
{
    public string Code { get; } = code;
}
