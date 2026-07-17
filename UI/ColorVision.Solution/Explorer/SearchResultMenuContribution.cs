using ColorVision.UI.Menus;

namespace ColorVision.Solution.Explorer
{
    [SolutionMenuContribution(priority: 290)]
    public sealed class SearchResultMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.search-result-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.VisualNodes.Count == 1
                && context.PrimaryVisualNode is SolutionSearchResultNode;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            return
            [
                new MenuItemMetadata
                {
                    GuidId = SolutionNavigationCommands.RevealInTreeId,
                    Order = 6,
                    Header = "在解决方案资源管理器中定位(_L)",
                    Command = SolutionNavigationCommands.RevealInTree,
                },
            ];
        }
    }
}
