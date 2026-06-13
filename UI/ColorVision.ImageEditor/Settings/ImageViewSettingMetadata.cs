using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.Properties;
using ColorVision.ImageEditor.Tif;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.Settings
{
    public enum ImageViewSettingScope
    {
        CurrentView,
        GlobalDefault,
        LoaderDefault,
        Workspace,
    }

    public enum ImageViewSettingType
    {
        Property,
        Class,
        View,
    }

    public sealed class ImageViewSettingMetadata
    {
        public int Order { get; set; } = 999;
        public string Group { get; set; } = Properties.Resources.Settings_GroupGeneral;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public ImageViewSettingScope Scope { get; set; } = ImageViewSettingScope.CurrentView;
        public ImageViewSettingType Type { get; set; } = ImageViewSettingType.Property;
        public string? BindingName { get; set; }
        public object? Source { get; set; }
        public Type? ViewType { get; set; }
        public Func<FrameworkElement>? ViewFactory { get; set; }
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
            yield return new ImageViewSettingMetadata { Group = Properties.Resources.Settings_GroupDisplay, Order = 10, Scope = ImageViewSettingScope.CurrentView, Source = imageView.Config, BindingName = nameof(ImageViewConfig.IsLayoutUpdated) };
            yield return new ImageViewSettingMetadata { Group = Properties.Resources.Settings_GroupDisplay, Order = 20, Scope = ImageViewSettingScope.CurrentView, Source = imageView.Config, BindingName = nameof(ImageViewConfig.IsShowText) };
            yield return new ImageViewSettingMetadata { Group = Properties.Resources.Settings_GroupDisplay, Order = 30, Scope = ImageViewSettingScope.CurrentView, Source = imageView.Config, BindingName = nameof(ImageViewConfig.IsShowMsg) };
            yield return new ImageViewSettingMetadata { Group = Properties.Resources.Settings_GroupDisplay, Order = 40, Scope = ImageViewSettingScope.CurrentView, Source = imageView.Config, BindingName = nameof(ImageViewConfig.DrawingTextFontSize) };

            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.Settings_GroupContext,
                Order = 10,
                Scope = ImageViewSettingScope.CurrentView,
                Type = ImageViewSettingType.View,
                ViewFactory = () => new ImageViewContextSettingsView(imageView),
            };
        }
    }

    internal sealed class ImageViewDefaultsSettingProvider : IImageViewSettingProvider, IImageViewSettingPersistence
    {
        public IEnumerable<ImageViewSettingMetadata> GetImageViewSettings(ImageView imageView)
        {
            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.Settings_GroupDefaults,
                Order = 5,
                Scope = ImageViewSettingScope.GlobalDefault,
                Type = ImageViewSettingType.Class,
                Name = Properties.Resources.Settings_DefaultImageScaling,
                Source = DefaultBitmapScalingConfig.Current,
            };

            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.Settings_GroupDefaults,
                Order = 10,
                Scope = ImageViewSettingScope.GlobalDefault,
                Type = ImageViewSettingType.Class,
                Name = Properties.Resources.Settings_DefaultDisplayParams,
                Source = DefaultImageViewDisplayConfig.Current,
            };

            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.Settings_GroupDefaults,
                Order = 20,
                Scope = ImageViewSettingScope.GlobalDefault,
                Type = ImageViewSettingType.Class,
                Name = Properties.Resources.Settings_DefaultTextStyle,
                Source = DefaultTextStyleConfig.Current,
            };

            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.Settings_GroupDefaults,
                Order = 30,
                Scope = ImageViewSettingScope.GlobalDefault,
                Type = ImageViewSettingType.Class,
                Name = Properties.Resources.Settings_PhysicalSizeDefaults,
                Source = DefalutTextAttribute.Defalut,
            };

            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.Settings_GroupDefaults,
                Order = 40,
                Scope = ImageViewSettingScope.GlobalDefault,
                Type = ImageViewSettingType.Class,
                Name = Properties.Resources.Settings_DefaultRealtimeCameraParams,
                Source = DefaultRealtimeCameraConfig.Current,
            };
        }

        public void SaveImageViewSettings(ImageView imageView)
        {
            DefaultBitmapScalingConfig.SaveCurrent();
            DefaultImageViewDisplayConfig.SaveCurrent();
            DefaultTextStyleConfig.SaveCurrent();
            DefaultRealtimeCameraConfig.SaveCurrent();
            ImageCalibrationService.SaveCurrent(imageView.Config);
        }
    }

    internal sealed class ImageViewWorkspaceSettingProvider : IImageViewSettingProvider, IImageViewSettingPersistence
    {
        public IEnumerable<ImageViewSettingMetadata> GetImageViewSettings(ImageView imageView)
        {
            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.Settings_GroupWorkspace,
                Order = 10,
                Scope = ImageViewSettingScope.Workspace,
                Type = ImageViewSettingType.View,
                ViewFactory = () => new ImageViewWorkspaceSettingsView(imageView),
            };

            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.Settings_GroupLoader,
                Order = 10,
                Scope = ImageViewSettingScope.LoaderDefault,
                Type = ImageViewSettingType.Class,
                Name = Properties.Resources.Settings_TifOpener,
                Source = TifOpenConfig.Current,
            };
        }

        public void SaveImageViewSettings(ImageView imageView)
        {
            TifOpenConfig.SaveCurrent();
        }
    }
}
