using System.Windows;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Host-provided entry point for maintaining only the Socket message database.
    /// </summary>
    public interface ISocketDatabaseCleanupWindowLauncher
    {
        void OpenWindow(Window owner);
    }
}
