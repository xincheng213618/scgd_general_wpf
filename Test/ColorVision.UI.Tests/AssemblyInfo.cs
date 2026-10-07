using ColorVision.UI;
using System.Runtime.CompilerServices;

// Several production registries reflect over newly loaded assemblies while holding process-wide locks.
// Keep test collections serial until those AssemblyLoad callbacks are made lock-free.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void InitializeConfiguration()
    {
        // Application startup supplies configuration before Engine services start.
        // Tests that replace it must restore a valid service for background callbacks.
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
    }
}
