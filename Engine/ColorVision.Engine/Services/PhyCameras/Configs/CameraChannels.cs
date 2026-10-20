using cvColorVision;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    internal static class CameraChannels
    {
        // Measurement output channels, not raw sensor planes or preview channels.
        public static ImageChannel Resolve(CameraMode mode, ImageChannel configured) => mode switch
        {
            CameraMode.LV_MODE => ImageChannel.One,
            CameraMode.BV_MODE or CameraMode.CV_MODE or CameraMode.LVTOBV_MODE => ImageChannel.Three,
            _ => configured
        };
    }
}
