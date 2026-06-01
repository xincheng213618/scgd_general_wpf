using Newtonsoft.Json;

namespace ColorVision.UI.HotKey
{
    public sealed class HotkeySetting
    {
        public string Id { get; set; } = string.Empty;

        [JsonProperty("Name")]
        public string LegacyName { get; set; } = string.Empty;

        public Hotkey Hotkey { get; set; } = Hotkey.None;
        public HotKeyKinds Kinds { get; set; } = HotKeyKinds.Windows;

        [JsonProperty("IsGlobal")]
        public bool LegacyIsGlobal
        {
            get => Kinds == HotKeyKinds.Global;
            set => Kinds = value ? HotKeyKinds.Global : HotKeyKinds.Windows;
        }

    #pragma warning disable CA1822
        public bool ShouldSerializeLegacyName() => false;
        public bool ShouldSerializeLegacyIsGlobal() => false;
    #pragma warning restore CA1822

        public static HotkeySetting FromHotKeys(HotKeys hotKeys)
        {
            return new HotkeySetting
            {
                Id = hotKeys.Id,
                Hotkey = hotKeys.Hotkey,
                Kinds = hotKeys.Kinds
            };
        }
    }
}