using System.Resources;
using System.Runtime.CompilerServices;
using ST.Library.UI;

namespace FlowEngineLib;

internal static class FlowEngineLocalization
{
    private static bool _registered;

    [ModuleInitializer]
    internal static void EnsureRegistered()
    {
        if (_registered) return;
        _registered = true;
        Lang.RegisterResourceManager(FlowEngineLib.Properties.Resources.ResourceManager);
    }
}
