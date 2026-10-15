using ColorVision.Engine.Utilities;

namespace ColorVision.Engine.Services.Devices.Camera;

public enum HikBayerQuality
{
    [LocalizedDescription("Camera_HikBayerQuality0")]
    Fast = 0,
    [LocalizedDescription("Camera_HikBayerQuality1")]
    Balanced = 1,
    [LocalizedDescription("Camera_HikBayerQuality2")]
    Optimal = 2,
    [LocalizedDescription("Camera_HikBayerQuality3")]
    OptimalPlus = 3
}
