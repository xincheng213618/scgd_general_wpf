namespace ColorVision.Copilot
{
    public static class CopilotResponsiveLayout
    {
        public static bool ShouldShowCompactHistory(
            bool isCompactSidebar,
            bool isConversationSidebarExpanded,
            bool isConversationEmpty,
            bool canShowCompactHistory)
        {
            return isCompactSidebar
                && !isConversationSidebarExpanded
                && isConversationEmpty
                && canShowCompactHistory;
        }
    }
}
