using ColorVision.UI;

namespace ColorVision.Scheduler
{
    public static class SchedulerModule
    {
        public const string Id = "ColorVision.Scheduler";

        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            catalog.AddBuiltIn(Id, typeof(SchedulerModule).Assembly);
        }
    }
}
