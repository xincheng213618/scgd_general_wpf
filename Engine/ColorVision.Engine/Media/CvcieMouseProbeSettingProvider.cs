using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;
using System.Collections.Generic;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieMouseProbeSettingProvider : IImageComponent, IImageViewSettingProvider, IImageViewSettingPersistence
    {
        public void Execute(ImageView imageView)
        {
            imageView.RegisterImageViewSettingProvider(this);
        }

        public IEnumerable<ImageViewSettingMetadata> GetImageViewSettings(ImageView imageView)
        {
            if (!imageView.Config.GetProperties<bool>("IsCVCIE"))
            {
                yield break;
            }

            yield return new ImageViewSettingMetadata
            {
                Group = "CVCIE 探针",
                Order = 10,
                Scope = ImageViewSettingScope.CurrentView,
                Type = ImageViewSettingType.Class,
                Name = "当前视图",
                Source = CvcieMouseProbeOptions.GetOrCreate(imageView),
            };

            yield return new ImageViewSettingMetadata
            {
                Group = "CVCIE 探针",
                Order = 20,
                Scope = ImageViewSettingScope.GlobalDefault,
                Type = ImageViewSettingType.Class,
                Name = "新视图默认值",
                Source = CvcieMouseProbeOptions.CurrentDefaults,
            };
        }

        public void SaveImageViewSettings(ImageView imageView)
        {
            CvcieMouseProbeOptions.SaveDefaults();
        }
    }
}
