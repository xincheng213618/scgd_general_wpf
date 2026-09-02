using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;
using EditorResources = ColorVision.ImageEditor.Properties.Resources;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieDisplaySettingProvider : IImageComponent
    {
        public void Execute(ImageView imageView)
        {
            imageView.RegisterSettings(() =>
            [
                new ImageViewSettingsEntry(EditorResources.Settings_GroupDefaults, "CVCIE 显示", CvcieDisplayConfig.Current, CvcieDisplayConfig.SaveCurrent),
            ]);
        }
    }
}
