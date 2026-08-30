namespace ColorVision.UI.HotKey
{
    internal static class HotkeyBindingCollection
    {
        public static Hotkey Copy(Hotkey? hotkey) => hotkey == null ? new Hotkey() : new Hotkey(hotkey.Key, hotkey.Modifiers);

        public static List<Hotkey> Copy(IEnumerable<Hotkey>? bindings) => bindings?.Select(Copy).ToList() ?? new List<Hotkey>();

        public static IReadOnlyList<Hotkey> Collect(Hotkey? primary, IEnumerable<Hotkey> additional)
        {
            List<Hotkey> bindings = new();
            if (!primary.IsNullOrEmpty()) bindings.Add(Copy(primary));
            // Preserve empty additional slots so validation reports malformed data instead of silently losing it.
            bindings.AddRange(Copy(additional));
            return bindings;
        }
    }
}
