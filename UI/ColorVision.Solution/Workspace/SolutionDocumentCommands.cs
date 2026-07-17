using System.Windows.Input;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// Commands shared by document editors and the application shell.
    /// </summary>
    public static class SolutionDocumentCommands
    {
        public static RoutedUICommand Reload { get; } = new(
            "重新加载",
            nameof(Reload),
            typeof(SolutionDocumentCommands),
            new InputGestureCollection { new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Shift) });
    }
}
