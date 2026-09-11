using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.ImageEditor.EditorTools.PseudoColor;
using ColorVision.ImageEditor.Tif;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using EditorResources = ColorVision.ImageEditor.Properties.Resources;

namespace ColorVision.ImageEditor.Settings
{
    public static class ImageSettingsCategories
    {
        public const string Information = "image.information";
        public const string Display = "view.display";
        public const string PseudoColor = "view.pseudo-color";
        public const string Filters = "view.filters";
        public const string Calibration = "view.calibration";
        public const string Application = "application.display";
        public const string Defaults = "application.defaults";
        public const string FileOpening = "application.file-opening";
        public const string Realtime = "application.realtime";
        public const string Diagnostics = "image.diagnostics";
    }

    internal static class ImageSettingsCatalog
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ImageSettingsCatalog));

        internal static IReadOnlyList<ImageViewSettingsEntry> Create(ImageView view)
        {
            List<ImageViewSettingsEntry> entries = new();
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (ImageViewSettingsEntry entry in BuiltIn(view).Concat(view.GetRegisteredSettings()))
            {
                if (ids.Add($"{entry.OwnerId}:{entry.StableId}")) entries.Add(entry);
                else Log.Warn($"Duplicate image settings entry: {entry.OwnerId}:{entry.StableId}");
            }
            return entries.OrderBy(entry => entry.Order).ToArray();
        }

        private static IEnumerable<ImageViewSettingsEntry> BuiltIn(ImageView view)
        {
            yield return new(SettingsText.Information, SettingsText.Information, view.Config)
            {
                Id = "information", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Information,
                Scope = ImageSettingsScope.CurrentImage, IsReadOnly = true, Order = 0, Description = SettingsText.InfoHint,
                CreateView = () => new ImageSettingsInformationView(view, false)
            };
            yield return new(SettingsText.Display, EditorResources.Settings_GroupDisplay, view.Config)
            {
                Id = "display", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Display,
                Scope = ImageSettingsScope.CurrentView, Order = 10, Description = SettingsText.CurrentOnly,
                PropertyNames = new[] { nameof(ImageViewConfig.IsLayoutUpdated), nameof(ImageViewConfig.IsShowText), nameof(ImageViewConfig.IsShowMsg), nameof(ImageViewConfig.DrawingTextFontSize) }
            };
            yield return new(SettingsText.Display, SettingsText.Toolbar, view.Config)
            {
                Id = "toolbars", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Display,
                Scope = ImageSettingsScope.CurrentView, Order = 11,
                PropertyNames = new[] { nameof(ImageViewConfig.IsToolBarTopVisible), nameof(ImageViewConfig.IsToolBarLeftVisible), nameof(ImageViewConfig.IsToolBarRightVisible), nameof(ImageViewConfig.IsToolBarAlVisible), nameof(ImageViewConfig.IsToolBarDrawVisible) }
            };

            if (view.IEditorToolFactory.GetIEditorTool<PseudoColorEditorTool>() is { } pseudo)
            {
                yield return new(SettingsText.PseudoColor, SettingsText.PseudoColor, pseudo.State)
                {
                    Id = "pseudo-color", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.PseudoColor,
                    Scope = ImageSettingsScope.CurrentView, Order = 20, Description = SettingsText.PseudoHint,
                    Actions = new[] {
                        new ImageSettingsAction(SettingsText.RestoreDefaults, () => pseudo.State.ApplyDefaults(PseudoColorDefaultConfig.Current)),
                        new ImageSettingsAction(SettingsText.SetAsDefault, () => {
                            var defaults = PseudoColorDefaultConfig.Current;
                            defaults.DefaultColormapTypes = pseudo.State.ColormapTypes;
                            defaults.IsAutoSetRangeByDefault = pseudo.State.IsAutoSetRange;
                            ImageSettingsPersistence.Save(defaults);
                        }) { SavedSource = PseudoColorDefaultConfig.Current }
                    }
                };
            }
            if (view.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>() is { } shader)
            {
                yield return new(SettingsText.Filters, SettingsText.Filters, shader.State)
                {
                    Id = "shader", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Filters,
                    Scope = ImageSettingsScope.CurrentView, Order = 30,
                    Description = shader.HasExternalPersistence ? SettingsText.ExternalHint : SettingsText.CurrentOnly,
                    Actions = new[] { new ImageSettingsAction(SettingsText.RestoreDefaults, shader.RestoreDefaults), new ImageSettingsAction(SettingsText.SetAsDefault, shader.SaveAsDefault) { SavedSource = DisplayShaderFilterDefaultConfig.Current.State } }
                };
            }
            string profileKey = view.Config.CalibrationProfileKey ?? ImageCalibrationConfig.DefaultKey;
            yield return new(SettingsText.Calibration, string.Format(SettingsText.ProfileName, profileKey), view.Config.Calibration)
            {
                Id = "calibration", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Calibration,
                Scope = ImageSettingsScope.SourceProfile, Order = 40, Description = SettingsText.ProfileHint,
                Actions = new[] {
                    new ImageSettingsAction(SettingsText.ReloadProfile, () => {
                        VerifyProfile(view, profileKey);
                        ImageCalibrationService.ApplyToView(view.Config, reload: true);
                    }),
                    new ImageSettingsAction(SettingsText.SaveProfile, () => {
                        VerifyProfile(view, profileKey);
                        ImageCalibrationService.SaveCurrent(view.Config);
                    })
                }
            };
            var display = DefaultImageViewDisplayConfig.Current;
            yield return new(SettingsText.GlobalDisplay, SettingsText.GlobalDisplay, display, () => ImageSettingsPersistence.Save(display))
            {
                Id = "display-preferences", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Application,
                Scope = ImageSettingsScope.Application, Order = 100, Description = SettingsText.GlobalHint
            };
            yield return Default("scaling", SettingsText.ImageScaling, DefaultBitmapScalingConfig.Current, 110);
            yield return Default("text", SettingsText.TextStyle, DefaultTextStyleConfig.Current, 111);
            yield return Default("pseudo-defaults", SettingsText.DefaultPseudo, PseudoColorDefaultConfig.Current, 112);
            var filters = DisplayShaderFilterDefaultConfig.Current;
            yield return new(SettingsText.NewItems, SettingsText.DefaultShader, filters.State, () => ImageSettingsPersistence.Save(filters))
            {
                Id = "shader-defaults", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Defaults,
                Scope = ImageSettingsScope.Defaults, Order = 113, Description = SettingsText.DefaultHint
            };
            var tiff = TifOpenConfig.Current;
            yield return new(SettingsText.FileOpening, SettingsText.Tiff, tiff, () => ImageSettingsPersistence.Save(tiff))
            {
                Id = "tiff", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.FileOpening,
                Scope = ImageSettingsScope.Defaults, Order = 120, Description = SettingsText.FileHint
            };
            var realtime = DefaultRealtimeCameraConfig.Current;
            yield return new(SettingsText.Realtime, SettingsText.Realtime, realtime, () => ImageSettingsPersistence.Save(realtime))
            {
                Id = "realtime", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Realtime,
                Scope = ImageSettingsScope.Application, Order = 130, Description = SettingsText.RealtimeHint
            };
            yield return new(SettingsText.Diagnostics, SettingsText.Technical, view.Config)
            {
                Id = "diagnostics", OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Diagnostics,
                Scope = ImageSettingsScope.CurrentImage, Order = 200, IsReadOnly = true,
                CreateView = () => new ImageSettingsInformationView(view, true)
            };
        }

        private static ImageViewSettingsEntry Default<T>(string id, string title, T source, int order) where T : class, IConfig
            => new(SettingsText.NewItems, title, source, () => ImageSettingsPersistence.Save(source))
            {
                Id = id, OwnerId = "ImageEditor", CategoryId = ImageSettingsCategories.Defaults,
                Scope = ImageSettingsScope.Defaults, Order = order, Description = SettingsText.DefaultHint
            };

        private static void VerifyProfile(ImageView view, string key)
        {
            if (view.Config.CalibrationProfileKey != key || ImageCalibrationService.ResolveCalibrationKey(view.Config) != key)
                throw new InvalidOperationException(SettingsText.ConfigChanged);
        }
    }
}
