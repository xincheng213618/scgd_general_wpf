namespace ColorVision.Copilot
{
    internal static class CopilotSuggestionSelection
    {
        public static int Reset(int itemCount) => itemCount > 0 ? 0 : -1;

        public static int Normalize(int selectedIndex, int itemCount)
        {
            if (itemCount <= 0)
                return -1;

            return selectedIndex >= 0 && selectedIndex < itemCount
                ? selectedIndex
                : 0;
        }

        public static int Move(int selectedIndex, int itemCount, bool previous)
        {
            if (itemCount <= 0)
                return -1;
            if (selectedIndex < 0 || selectedIndex >= itemCount)
                return previous ? itemCount - 1 : 0;

            if (previous)
                return selectedIndex == 0 ? itemCount - 1 : selectedIndex - 1;
            return selectedIndex == itemCount - 1 ? 0 : selectedIndex + 1;
        }
    }
}
