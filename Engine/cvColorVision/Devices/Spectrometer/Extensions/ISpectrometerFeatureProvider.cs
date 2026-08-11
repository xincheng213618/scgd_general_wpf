using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace cvColorVision
{
    public sealed record SpectrometerFeatureMetadata(
        string Id,
        string DisplayName,
        string Description,
        int Order,
        bool RequiresExclusiveDeviceAccess = true,
        bool ShowCompletionMessage = true);

    public sealed record SpectrometerCalibrationGroupSnapshot(
        string GroupName,
        string WavelengthFile,
        string MagnitudeFile,
        int FilterWheelPosition = -1);

    public sealed record SpectrometerConfigurationSnapshot
    {
        public int ContractVersion { get; init; } = 1;
        public string DeviceCode { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public SpectrometerType SpectrometerType { get; init; }
        public bool IsComPort { get; init; }
        public string ComPortName { get; init; } = string.Empty;
        public int BaudRate { get; init; }
        public float IntegrationTime { get; init; }
        public int Average { get; init; }
        public string ActiveCalibrationGroupName { get; init; } = string.Empty;
        public string SourceBaseDirectory { get; init; } = string.Empty;
        public IReadOnlyList<SpectrometerCalibrationGroupSnapshot> CalibrationGroups { get; init; }
            = Array.Empty<SpectrometerCalibrationGroupSnapshot>();
    }

    public enum SpectrometerFeatureStatus
    {
        Succeeded,
        Cancelled,
        Failed,
    }

    public sealed record SpectrometerFeatureResult(
        SpectrometerFeatureStatus Status,
        string Message,
        string GeneratedMagnitudeFile,
        string ActiveCalibrationGroupName)
    {
        public static SpectrometerFeatureResult Success(
            string generatedMagnitudeFile,
            string activeCalibrationGroupName,
            string message = "") =>
            new(SpectrometerFeatureStatus.Succeeded, message, generatedMagnitudeFile, activeCalibrationGroupName);

        public static SpectrometerFeatureResult Cancel(
            string message = "",
            string activeCalibrationGroupName = "") =>
            new(SpectrometerFeatureStatus.Cancelled, message, string.Empty, activeCalibrationGroupName);

        public static SpectrometerFeatureResult Failure(
            string message,
            string activeCalibrationGroupName = "") =>
            new(SpectrometerFeatureStatus.Failed, message, string.Empty, activeCalibrationGroupName);
    }

    public interface ISpectrometerFeatureProvider
    {
        SpectrometerFeatureMetadata Metadata { get; }

        Task<SpectrometerFeatureResult> ExecuteAsync(
            SpectrometerConfigurationSnapshot snapshot,
            CancellationToken cancellationToken);
    }
}
