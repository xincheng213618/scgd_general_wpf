using ColorVision.UI;
using System;
using System.IO;

namespace ColorVision.ImageEditor.Settings
{
    /// <summary>Saves the exact configuration object displayed by the settings page.</summary>
    public static class ImageSettingsPersistence
    {
        public static void Save<T>(T source) where T : class, IConfig
        {
            IConfigService service = ConfigService.Instance ?? throw new InvalidOperationException(SettingsText.SessionOnly);
            if (!ReferenceEquals(service.GetRequiredService<T>(), source))
                throw new InvalidOperationException(SettingsText.ConfigChanged);

            if (service is ConfigHandler handler)
            {
                if (!handler.TrySave(source, out string error)) throw new IOException(error);
            }
            else
            {
                service.Save<T>();
            }
        }
    }
}
