using ColorVision.UI;
using System;

namespace ColorVision
{
    internal static class BuiltInModules
    {
        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);

            // The host runs with catalog-only discovery, so every in-process first-party assembly
            // that may contribute extension types must be listed here or by its own module.
            catalog.AddBuiltIn("ColorVision", typeof(BuiltInModules).Assembly);
            catalog.AddBuiltIn("ColorVision.Common", typeof(ModuleCatalog).Assembly);
            catalog.AddBuiltIn("ColorVision.UI", typeof(AssemblyHandler).Assembly);
            catalog.AddBuiltIn("ColorVision.Themes", typeof(Themes.ThemeManager).Assembly);
            catalog.AddBuiltIn("ColorVision.Core", typeof(Core.FileFusion).Assembly);
            catalog.AddBuiltIn("ColorVision.Algorithms", typeof(Algorithms.AlgorithmCatalog).Assembly);
            catalog.AddBuiltIn("ColorVision.FileIO", typeof(FileIO.CVFileUtil).Assembly);
            catalog.AddBuiltIn("cvColorVision", typeof(cvColorVision.cvCameraCSLib).Assembly);
            catalog.AddBuiltIn("FlowEngineLib", typeof(FlowEngineLib.Algorithm.AlgorithmNode).Assembly);
            catalog.AddBuiltIn("ST.Library.UI", typeof(ST.Library.UI.NodeContainer.CVNode).Assembly);

            Engine.EngineModule.Register(catalog);
            Scheduler.SchedulerModule.Register(catalog);
            ImageEditor.ImageEditorModule.Register(catalog);
            Solution.SolutionModule.Register(catalog);
            SocketProtocol.SocketProtocolModule.Register(catalog);
            Database.DatabaseModule.Register(catalog);
            UI.Desktop.DesktopModule.Register(catalog);
            ImageTools.ImageToolsModule.Register(catalog);
            Rbac.RbacModule.Register(catalog);
        }
    }
}
