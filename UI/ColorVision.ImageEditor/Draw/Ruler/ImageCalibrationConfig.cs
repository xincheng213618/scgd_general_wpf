using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Draw.Ruler
{
    public class ImageCalibrationProfile : ViewModelBase
    {
        public double ActualLength { get => _ActualLength; set { _ActualLength = value <= 0 ? 1 : value; OnPropertyChanged(); } }
        private double _ActualLength = 1;

        public string PhysicalUnit { get => _PhysicalUnit; set { _PhysicalUnit = string.IsNullOrWhiteSpace(value) ? "Px" : value; OnPropertyChanged(); } }
        private string _PhysicalUnit = "Px";

        public bool IsUsePhysicalUnit { get => _IsUsePhysicalUnit; set { _IsUsePhysicalUnit = value; OnPropertyChanged(); } }
        private bool _IsUsePhysicalUnit;

        public DateTime UpdatedAt { get => _UpdatedAt; set { _UpdatedAt = value; OnPropertyChanged(); } }
        private DateTime _UpdatedAt = DateTime.Now;

        public ImageCalibrationProfile Clone()
        {
            return new ImageCalibrationProfile
            {
                ActualLength = ActualLength,
                PhysicalUnit = PhysicalUnit,
                IsUsePhysicalUnit = IsUsePhysicalUnit,
                UpdatedAt = UpdatedAt
            };
        }

        public void CopyFrom(DefalutTextAttribute attribute)
        {
            ActualLength = attribute.ActualLength;
            PhysicalUnit = attribute.PhysicalUnit;
            IsUsePhysicalUnit = attribute.IsUsePhysicalUnit;
            UpdatedAt = DateTime.Now;
        }

        public void ApplyTo(DefalutTextAttribute attribute)
        {
            attribute.ActualLength = ActualLength;
            attribute.PhysicalUnit = PhysicalUnit;
            attribute.IsUsePhysicalUnit = IsUsePhysicalUnit;
        }
    }

    public class ImageCalibrationConfig : ViewModelBase, IConfig
    {
        public const string DefaultKey = "Default";

        private static readonly ImageCalibrationConfig Fallback = new();

        public static ImageCalibrationConfig Instance
        {
            get
            {
                try
                {
                    return ConfigService.Instance?.GetRequiredService<ImageCalibrationConfig>() ?? Fallback;
                }
                catch
                {
                    return Fallback;
                }
            }
        }

        public Dictionary<string, ImageCalibrationProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultKey] = new ImageCalibrationProfile()
        };

        public ImageCalibrationProfile GetOrCreateProfile(string? key)
        {
            string profileKey = NormalizeKey(key);
            Profiles ??= new Dictionary<string, ImageCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
            if (Profiles.TryGetValue(profileKey, out var profile) && profile != null)
            {
                return profile;
            }

            var source = Profiles.TryGetValue(DefaultKey, out var defaultProfile) && defaultProfile != null
                ? defaultProfile.Clone()
                : new ImageCalibrationProfile();
            source.UpdatedAt = DateTime.Now;
            Profiles[profileKey] = source;
            return source;
        }

        public void SaveProfile(string? key, DefalutTextAttribute attribute)
        {
            var profile = GetOrCreateProfile(key);
            profile.CopyFrom(attribute);
            Profiles[NormalizeKey(key)] = profile;

            OnPropertyChanged(nameof(Profiles));
        }

        public static string NormalizeKey(string? key)
        {
            return string.IsNullOrWhiteSpace(key) ? DefaultKey : key.Trim();
        }
    }

    public static class ImageCalibrationService
    {
        public const string CalibrationSourceKeyProperty = "CalibrationSourceKey";

        public static string ResolveCalibrationKey(ImageViewConfig? config)
        {
            if (config == null)
            {
                return ImageCalibrationConfig.DefaultKey;
            }

            string? configuredKey = config.GetProperties<string>(CalibrationSourceKeyProperty);
            if (!string.IsNullOrWhiteSpace(configuredKey))
            {
                return ImageCalibrationConfig.NormalizeKey(configuredKey);
            }

            string? cameraManufacturer = config.GetProperties<string>("CameraManufacturer");
            string? cameraModel = config.GetProperties<string>("CameraModel");
            if (!string.IsNullOrWhiteSpace(cameraManufacturer) || !string.IsNullOrWhiteSpace(cameraModel))
            {
                return ImageCalibrationConfig.NormalizeKey($"Camera:{cameraManufacturer}|{cameraModel}");
            }

            return ImageCalibrationConfig.DefaultKey;
        }

        public static void ApplyToDefault(ImageViewConfig? config)
        {
            var key = ResolveCalibrationKey(config);
            config?.SetViewState(CalibrationSourceKeyProperty, key, nameof(ImageCalibrationConfig), "当前视窗使用的标定档案键");

            var profile = ImageCalibrationConfig.Instance.GetOrCreateProfile(key);
            profile.ApplyTo(DefalutTextAttribute.Defalut);
        }

        public static void SaveCurrent(ImageViewConfig? config)
        {
            var key = ResolveCalibrationKey(config);
            var attribute = DefalutTextAttribute.Defalut;
            ImageCalibrationConfig.Instance.SaveProfile(key, attribute);

            try
            {
                ConfigService.Instance?.Save<ImageCalibrationConfig>();
                ConfigService.Instance?.Save<DefalutTextAttribute>();
            }
            catch
            {
            }
        }
    }
}
