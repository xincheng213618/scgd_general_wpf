using Newtonsoft.Json;

namespace ColorVision.UI.HotKey
{
    public sealed class HotkeySetting
    {
        public string Id { get; set; } = string.Empty;

        [JsonProperty("Name")]
        public string LegacyName { get; set; } = string.Empty;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Hotkey Hotkey { get; set; } = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<Hotkey> AdditionalHotkeys
        {
            get => _additionalHotkeys;
            set => _additionalHotkeys = HotkeyBindingCollection.Copy(value);
        }
        private List<Hotkey> _additionalHotkeys = new();

        public IReadOnlyList<Hotkey> GetBindings() => HotkeyBindingCollection.Collect(Hotkey, AdditionalHotkeys);

        public void SetBindings(IEnumerable<Hotkey> bindings)
        {
            List<Hotkey> copy = HotkeyBindingCollection.Copy(bindings);
            Hotkey = copy.Count > 0 ? copy[0] : new Hotkey();
            AdditionalHotkeys = copy.Skip(1).ToList();
        }

        public HotKeyKinds Kinds { get; set; } = HotKeyKinds.Windows;

        [JsonProperty("IsGlobal")]
        public bool LegacyIsGlobal
        {
            get => Kinds == HotKeyKinds.Global;
            set => Kinds = value ? HotKeyKinds.Global : HotKeyKinds.Windows;
        }

    #pragma warning disable CA1822
        public bool ShouldSerializeLegacyName() => string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(LegacyName);
        public bool ShouldSerializeLegacyIsGlobal() => false;
    #pragma warning restore CA1822

        public static HotkeySetting FromHotKeys(HotKeys hotKeys)
        {
            return new HotkeySetting
            {
                Id = hotKeys.Id,
                Hotkey = new Hotkey(hotKeys.Hotkey.Key, hotKeys.Hotkey.Modifiers),
                AdditionalHotkeys = hotKeys.AdditionalHotkeys,
                Kinds = hotKeys.Kinds
            };
        }
    }
}
