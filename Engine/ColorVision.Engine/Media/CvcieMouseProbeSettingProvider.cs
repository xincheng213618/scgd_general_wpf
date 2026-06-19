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
                    new ImageViewSettingsEntry("CVCIE 探针", "当前视图", CvcieMouseProbeOptions.GetOrCreate(imageView)),
                    new ImageViewSettingsEntry("CVCIE 探针", "默认值", CvcieMouseProbeOptions.CurrentDefaults, CvcieMouseProbeOptions.SaveDefaults),
                ];
            });
        }
    }
}
