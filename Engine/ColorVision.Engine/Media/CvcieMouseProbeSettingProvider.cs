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
                var defaults = CvcieMouseProbeOptions.CurrentDefaults;
                ImageViewSettingsEntry defaultEntry = new(SettingsText.NewItems, Properties.Resources.CvcieProbe, defaults, () => ImageSettingsPersistence.Save(defaults))
                {
                    Id = "cvcie-probe-defaults", OwnerId = "Engine", CategoryId = ImageSettingsCategories.Defaults,
                    Scope = ImageSettingsScope.Defaults, Order = 114, Description = SettingsText.DefaultHint
                };
                if (!imageView.Config.GetProperties<bool>("IsCVCIE"))
                {
                    return [defaultEntry];
                }
                var current = CvcieMouseProbeOptions.GetOrCreate(imageView);
                return
                [
                    new ImageViewSettingsEntry(Properties.Resources.CvcieProbe, Properties.Resources.CvcieProbe, current)
                    {
                        Id = "cvcie-probe", OwnerId = "Engine", CategoryId = "view.cvcie-probe",
                        Scope = ImageSettingsScope.CurrentView, Order = 50, Description = SettingsText.CurrentOnly,
                        Actions = [
                            new ImageSettingsAction(SettingsText.RestoreDefaults, () => current.CopyFrom(defaults)),
                            new ImageSettingsAction(SettingsText.SetAsDefault, () => { defaults.CopyFrom(current); ImageSettingsPersistence.Save(defaults); }) { SavedSource = defaults }
                        ]
                    },
                    defaultEntry,
                ];
            });
        }
    }
}
