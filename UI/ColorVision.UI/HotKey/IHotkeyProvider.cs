namespace ColorVision.UI.HotKey
{
    public interface IHotkeyProvider
    {
        IEnumerable<HotkeyDefinition> GetHotkeyDefinitions();
    }
}