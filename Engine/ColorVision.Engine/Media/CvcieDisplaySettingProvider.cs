using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieDisplaySettingProvider : IImageComponent
    {
        public void Execute(ImageView imageView)
        {
            imageView.RegisterSettings(() =>
            {
                var config = CvcieDisplayConfig.Current;
                return [new ImageViewSettingsEntry(SettingsText.FileOpening, "CVCIE", config, () => ImageSettingsPersistence.Save(config))
                {
                    Id = "cvcie-display", OwnerId = "Engine", CategoryId = ImageSettingsCategories.FileOpening,
                    Scope = ImageSettingsScope.Defaults, Order = 121, Description = SettingsText.FileHint
                }];
            });
        }
    }
}
