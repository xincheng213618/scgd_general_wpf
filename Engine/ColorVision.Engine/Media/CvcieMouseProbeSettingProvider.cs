using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieMouseProbeSettingProvider : IImageComponent
    {
        public void Execute(ImageView imageView)
        {
            imageView.RegisterSettings(() =>
            {
                if (!imageView.Config.GetProperties<bool>("IsCVCIE"))
                {
                    return [];
                }

                return
                [
                    new ImageViewSettingsEntry(Properties.Resources.CvcieProbe, Properties.Resources.CurrentView, CvcieMouseProbeOptions.GetOrCreate(imageView)),
                    new ImageViewSettingsEntry(Properties.Resources.CvcieProbe, Properties.Resources.DefaultValue, CvcieMouseProbeOptions.CurrentDefaults, CvcieMouseProbeOptions.SaveDefaults),
                ];
            });
        }
    }
}
