using ColorVision.UI;
using System;

namespace ColorVision
{
    internal static class BuiltInModules
    {
        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);

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
