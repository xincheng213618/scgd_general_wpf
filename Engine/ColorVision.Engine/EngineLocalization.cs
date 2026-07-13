#pragma warning disable CA2255
using ST.Library.UI;
using System.Runtime.CompilerServices;

namespace ColorVision.Engine
{
    internal static class EngineLocalization
    {
        [ModuleInitializer]
        internal static void RegisterResources()
        {
            Lang.RegisterResourceManager(Properties.Resources.ResourceManager);
        }
    }
}
