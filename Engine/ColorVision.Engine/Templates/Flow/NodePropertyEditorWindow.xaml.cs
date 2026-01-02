using ST.Library.UI.NodeEditor;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// NodePropertyEditorWindow.xaml 的交互逻辑
    /// </summary>
    public partial class NodePropertyEditorWindow : Window
    {
        private Window _owner;
        private bool _isManualClose = false;

        public STNodePropertyGrid PropertyGrid => STNodePropertyGrid1;
        public StackPanel SignStackPanel => SignStackPanelContainer;

        public NodePropertyEditorWindow()
        {
            InitializeComponent();
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
            if (!_isManualClose)
            {
                Hide();
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            UpdatePosition();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Clean up
        }

        public new void Close()
        {
            _isManualClose = true;
            base.Close();
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

        public void ShowPropertyEditor()
        {
            if (!IsVisible)
            {
                Show();
            }
            Activate();
        }
    }
}
