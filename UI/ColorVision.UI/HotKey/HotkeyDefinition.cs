namespace ColorVision.UI.HotKey
{
    public sealed class HotkeyDefinition
    {
        public HotkeyDefinition(string id, string name, Hotkey defaultHotkey, HotKeyCallBackHanlder handler, HotKeyKinds defaultKinds = HotKeyKinds.Windows)
        {
            Id = id;
            Name = name;
            DefaultHotkey = defaultHotkey;
            Handler = handler;
            DefaultKinds = defaultKinds;
        }

        public string Id { get; }
        public string Name { get; }
        public Hotkey DefaultHotkey { get; }
        public HotKeyKinds DefaultKinds { get; }
        public HotKeyCallBackHanlder Handler { get; }

        public HotKeys CreateRuntimeHotKeys()
        {
            return new HotKeys(Name, DefaultHotkey, Handler)
            {
                Id = Id,
                DefaultHotkey = DefaultHotkey,
                DefaultKinds = DefaultKinds,
                Kinds = DefaultKinds
            };
        }
    }
}