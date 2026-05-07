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
                Description = "控制当前 CVCIE 放大镜探针的取样形状和取样范围，修改后立即生效。",
                Source = CvcieMouseProbeOptions.GetOrCreate(imageView),
            };

            yield return new ImageViewSettingMetadata
            {
                Group = "CVCIE 探针",
                Order = 20,
                Scope = ImageViewSettingScope.GlobalDefault,
                Type = ImageViewSettingType.Class,
                Name = "新视图默认值",
                Description = "控制后续新打开 CVCIE 视图时的初始放大镜探针参数。关闭设置窗口时保存。",
                Source = CvcieMouseProbeOptions.CurrentDefaults,
            };
        }

        public void SaveImageViewSettings(ImageView imageView)
        {
            CvcieMouseProbeOptions.SaveDefaults();
        }
    }
}