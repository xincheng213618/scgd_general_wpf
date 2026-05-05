using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Settings
{
    public enum ImageViewSettingType
    {
        Property,
        Class,
        View,
    }

    public sealed class ImageViewSettingMetadata
    {
        public int Order { get; set; } = 999;
        public string Group { get; set; } = "通用";
        public string? Name { get; set; }
        public string? Description { get; set; }
        public ImageViewSettingType Type { get; set; } = ImageViewSettingType.Property;
        public string? BindingName { get; set; }
        public object? Source { get; set; }
        public Type? ViewType { get; set; }
    }

    public interface IImageViewSettingProvider
    {
        IEnumerable<ImageViewSettingMetadata> GetImageViewSettings(ImageView imageView);
    }

    public interface IImageViewSettingPersistence
    {
        void SaveImageViewSettings(ImageView imageView);
    }

    internal sealed class ImageViewDisplaySettingProvider : IImageViewSettingProvider
    {
        public IEnumerable<ImageViewSettingMetadata> GetImageViewSettings(ImageView imageView)
        {
            yield return new ImageViewSettingMetadata { Group = "显示", Order = 10, Source = imageView.Config, BindingName = nameof(ImageViewConfig.IsLayoutUpdated) };
            yield return new ImageViewSettingMetadata { Group = "显示", Order = 20, Source = imageView.Config, BindingName = nameof(ImageViewConfig.IsShowText) };
            yield return new ImageViewSettingMetadata { Group = "显示", Order = 30, Source = imageView.Config, BindingName = nameof(ImageViewConfig.IsShowMsg) };
            yield return new ImageViewSettingMetadata { Group = "显示", Order = 40, Source = imageView.Config, BindingName = nameof(ImageViewConfig.DrawingTextFontSize) };
            yield return new ImageViewSettingMetadata { Group = "显示", Order = 50, Source = imageView.ImageViewModel, BindingName = nameof(ImageViewModel.MaxZoom) };
            yield return new ImageViewSettingMetadata { Group = "显示", Order = 60, Source = imageView.ImageViewModel, BindingName = nameof(ImageViewModel.MinZoom) };
        }
    }

    internal sealed class ImageViewDefaultsSettingProvider : IImageViewSettingProvider, IImageViewSettingPersistence
    {
        public IEnumerable<ImageViewSettingMetadata> GetImageViewSettings(ImageView imageView)
        {
            yield return new ImageViewSettingMetadata
            {
                Group = "默认值",
                Order = 5,
                Type = ImageViewSettingType.Class,
                Name = "默认图像缩放",
                Description = "控制新打开图像时应用的 BitmapScalingMode；个别加载器仍可按格式要求覆盖当前值。",
                Source = DefaultBitmapScalingConfig.Current,
            };

            yield return new ImageViewSettingMetadata
            {
                Group = "默认值",
                Order = 10,
                Type = ImageViewSettingType.Class,
                Name = "默认文本样式",
                Description = "控制新建文本和带文字图元的默认字体、颜色和排版。",
                Source = DefaultTextStyleConfig.Current,
            };

            yield return new ImageViewSettingMetadata
            {
                Group = "默认值",
                Order = 20,
                Type = ImageViewSettingType.Class,
                Name = "物理尺寸默认值",
                Description = "控制标尺、网格等物理尺寸换算的默认长度和单位。",
                Source = DefalutTextAttribute.Defalut,
            };
        }

        public void SaveImageViewSettings(ImageView imageView)
        {
            DefaultBitmapScalingConfig.SaveCurrent();
            DefaultTextStyleConfig.SaveCurrent();
            ImageCalibrationService.SaveCurrent(imageView.EditorContext);
        }
    }
}