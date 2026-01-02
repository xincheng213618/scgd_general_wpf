using ST.Library.UI.NodeEditor;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// NodePropertyEditorWindow.xaml 的交互逻辑
    /// Popup window for editing node properties with toggle between PropertyGrid and SignStackPanel
    /// </summary>
    public partial class NodePropertyEditorWindow : Window
    {
        private Window _owner;
        private bool _allowClose = false;

        public STNodePropertyGrid PropertyGrid => STNodePropertyGrid1;
        public StackPanel SignStackPanel => SignStackPanelContainer;

        public NodePropertyEditorWindow()
        {
            InitializeComponent();
            // Prevent actual closing, just hide instead
            Closing += NodePropertyEditorWindow_Closing;
        }

        private void NodePropertyEditorWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Prevent window from closing unless explicitly allowed
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
            }
        }

        public void SetOwner(Window owner)
        {
            _owner = owner;
            if (owner != null)
            {
                Owner = owner;
                // Position window relative to owner
                WindowStartupLocation = WindowStartupLocation.Manual;
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            if (_owner != null && _owner.IsLoaded)
            {
                // Position the window to the right of the owner
                Left = _owner.Left + _owner.ActualWidth + 10;
                Top = _owner.Top;
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Hide window when it loses focus
            Hide();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            UpdatePosition();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Clean up
        }

        /// <summary>
        /// Closes the window permanently. Only call this when disposing.
        /// </summary>
        public void CloseWindow()
        {
            _allowClose = true;
            Close();
        }

        private void ToggleEditMode_Checked(object sender, RoutedEventArgs e)
        {
            // Show SignStackPanel, hide PropertyGrid
            PropertyGridHost.Visibility = Visibility.Collapsed;
            SignStackScrollViewer.Visibility = Visibility.Visible;
        }

        private void ToggleEditMode_Unchecked(object sender, RoutedEventArgs e)
        {
            // Show PropertyGrid, hide SignStackPanel
            PropertyGridHost.Visibility = Visibility.Visible;
            SignStackScrollViewer.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Shows the property editor window and activates it
        /// </summary>
        public void ShowPropertyEditor()
        {
            try
            {
                if (!IsVisible)
                {
                    Show();
                }
                
                // Try to activate the window
                if (IsVisible)
                {
                    Activate();
                }
            }
            catch (InvalidOperationException)
            {
                // Window was closed, cannot reopen
                // This should not happen with our Closing event handler, but just in case
            }
        }
    }
}
