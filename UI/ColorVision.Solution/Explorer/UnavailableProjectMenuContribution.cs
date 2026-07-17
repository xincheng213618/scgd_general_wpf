using ColorVision.UI.Menus;

namespace ColorVision.Solution.Explorer
{
    [SolutionMenuContribution(priority: 235)]
    public sealed class UnavailableProjectMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.unavailable-project-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is UnavailableProjectNode;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var projectNode = (UnavailableProjectNode)context.PrimaryNode;
            return
            [
                new MenuItemMetadata
                {
                    GuidId = "ShowUnavailableProjectError",
                    Order = 2,
                    Header = "查看加载错误(_E)...",
                    Command = projectNode.ShowLoadErrorCommand,
                },
            ];
        }
    }
}
