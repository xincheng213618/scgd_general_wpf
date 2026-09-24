using ColorVision.UI;

namespace ColorVision.Solution
{
    public static class SolutionModule
    {
        public const string Id = "ColorVision.Solution";

        public static void Register(ModuleCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            catalog.AddBuiltIn(Id, typeof(SolutionModule).Assembly);
        }
    }
}
