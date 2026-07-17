using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Routed commands owned by the solution tree. They intentionally do not use
    /// ApplicationCommands.Open, whose Ctrl+O gesture opens the global picker.
    /// </summary>
    public static class SolutionResourceCommands
    {
        public const string OpenId = "Open";
        public const string OpenWithId = "OpenWith";

        public static RoutedUICommand Open { get; } = new(
            "打开",
            nameof(Open),
            typeof(SolutionResourceCommands),
            new InputGestureCollection { new KeyGesture(Key.Enter) });

        public static RoutedUICommand OpenWith { get; } = new(
            "打开方式",
            nameof(OpenWith),
            typeof(SolutionResourceCommands));
    }
}
