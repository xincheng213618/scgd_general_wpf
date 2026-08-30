using ColorVision.Database;
using ColorVision.SocketProtocol;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.DatabaseCleanup
{
    public sealed class SocketDatabaseCleanupWindowLauncher : ISocketDatabaseCleanupWindowLauncher
    {
        public void OpenWindow(Window owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            DatabaseCleanupWindow.OpenWindow(owner, CreateSourceProvider());
        }

        internal static SocketMessagesSqliteCleanupProvider CreateSourceProvider() => new();
    }
}
