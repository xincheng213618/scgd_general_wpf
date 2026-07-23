using ColorVision.UI;
using System;

namespace ColorVision.Engine
{
    public static class EngineModule
    {
        public const string Id = "ColorVision.Engine";

        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            catalog.AddBuiltIn(Id, typeof(EngineModule).Assembly);
        }
    }
}
