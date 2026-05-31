namespace ColorVision.UI.HotKey
{
    public interface IHotkeyRegistration : IDisposable
    {
        Hotkey Hotkey { get; }
        bool IsRegistered { get; }
    }
}