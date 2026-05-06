using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;
using System.Collections.Generic;

namespace ColorVision.Engine.Media
{
    public sealed class CvcieProbeImageViewSettingProvider : IImageComponent, IImageViewSettingProvider, IImageViewSettingPersistence
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
                Group = "CVCIE",
                Order = 10,
                Type = ImageViewSettingType.Class,
                Name = "当前视窗探针",
                Description = "控制当前 CVCIE 画布的取样范围和取样形状。这里的修改立即生效，但不会写回默认值。",
                Source = CvcieProbeSettings.GetOrCreate(imageView),
            };

            yield return new ImageViewSettingMetadata
            {
                Group = "CVCIE",
                Order = 20,
                Type = ImageViewSettingType.Class,
                Name = "探针默认值",
                Description = "控制后续新打开的 CVCIE 画布初始探针配置。关闭设置窗口时保存。",
                Source = CvcieProbeDefaultConfig.Current,
            };
        }

        public void SaveImageViewSettings(ImageView imageView)
        {
            CvcieProbeDefaultConfig.SaveCurrent();
        }
    }
}