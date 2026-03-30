namespace ColorVision.UI
{
    /// <summary>
    /// Provides dock panels that should be registered with the MainWindow DockingManager.
    /// Implementations are discovered via assembly scanning and called during MainWindow initialization,
    /// BEFORE the layout is loaded, ensuring panels are properly persisted and restored.
    /// </summary>
    public interface IDockPanelProvider
    {
        /// <summary>
        /// Order of execution (lower values first).
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Register panels with the layout manager.
        /// Called on the UI thread before LoadLayout.
        /// </summary>
        void RegisterPanels();
    }
}
