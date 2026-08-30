using Newtonsoft.Json;

namespace ColorVision.UI.HotKey
{
    public sealed class HotkeyDefinition
    {
        public HotkeyDefinition(string id, string name, Hotkey defaultHotkey, HotKeyCallBackHanlder handler, HotKeyKinds defaultKinds = HotKeyKinds.Windows)
        {
            Id = id;
            Name = name;
            DefaultHotkey = new Hotkey(defaultHotkey.Key, defaultHotkey.Modifiers);
            Handler = handler;
            DefaultKinds = defaultKinds;
        }

        public string Id { get; }
        public string Name { get; }
        [JsonIgnore]
        public string DisplayName { get; set; } = string.Empty;
        [JsonIgnore]
        public string Description { get; set; } = string.Empty;
        [JsonIgnore]
        public string Category { get; set; } = string.Empty;
        [JsonIgnore]
        public string Source { get; set; } = string.Empty;
        public Hotkey DefaultHotkey { get; }
        public HotKeyKinds DefaultKinds { get; }
        public HotKeyCallBackHanlder Handler { get; }

        public HotKeys CreateRuntimeHotKeys()
        {
            return new HotKeys(Name, DefaultHotkey, Handler)
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                Category = Category,
                Source = Source,
                DefaultKinds = DefaultKinds,
                Kinds = DefaultKinds
            };
        }
    }
}
