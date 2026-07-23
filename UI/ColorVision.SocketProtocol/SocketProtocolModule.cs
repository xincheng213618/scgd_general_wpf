using ColorVision.UI;
using System;

namespace ColorVision.SocketProtocol
{
    public static class SocketProtocolModule
    {
        public const string Id = "ColorVision.SocketProtocol";

        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            catalog.AddBuiltIn(Id, typeof(SocketProtocolModule).Assembly);
        }
    }
}
