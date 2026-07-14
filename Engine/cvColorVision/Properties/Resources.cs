using System.Globalization;
using System.Reflection;
using System.Resources;

namespace cvColorVision.Properties
{
    internal static class Resources
    {
        internal const string ExpTimeConfigCategory = nameof(ExpTimeConfigCategory);
        internal const string CameraConfigCategory = nameof(CameraConfigCategory);
        internal const string CalibrationLibraryConfigCategory = nameof(CalibrationLibraryConfigCategory);
        internal const string ChannelConfigCategory = nameof(ChannelConfigCategory);
        internal const string ExposureParameters = nameof(ExposureParameters);
        internal const string CameraParameters = nameof(CameraParameters);
        internal const string CalibrationParameters = nameof(CalibrationParameters);
        internal const string ChannelParameters = nameof(ChannelParameters);

        internal static ResourceManager ResourceManager { get; } = new("cvColorVision.Properties.Resources", Assembly.GetExecutingAssembly());

        internal static string GetString(string key)
        {
            return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
    }
}
