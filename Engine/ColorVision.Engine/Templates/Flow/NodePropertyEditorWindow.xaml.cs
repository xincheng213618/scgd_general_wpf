using ST.Library.UI.NodeEditor;
using System;
using System.Linq;
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
        private System.Windows.Forms.Control _targetControl;

        public STNodePropertyGrid PropertyGrid => STNodePropertyGrid1;
        public StackPanel SignStackPanel => SignStackPanelContainer;

        public NodePropertyEditorWindow()
        {
            using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            DpiX = graphics.DpiX;
            InitializeComponent();
            // Prevent actual closing, just hide instead
            Closing += NodePropertyEditorWindow_Closing;

        }

        public static float DpiX { get; set; }



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

        /// <summary>
        /// Set the target control to position the window relative to (e.g., STNodeEditorMain)
        /// </summary>
        public void SetTargetControl(System.Windows.Forms.Control targetControl)
        {
            _targetControl = targetControl;
            if (_targetControl != null)
            {
                // Find the WPF window that hosts this control
                var handle = _targetControl.FindForm()?.Handle;
                if (handle.HasValue)
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(this);
                    var window = System.Windows.Application.Current.Windows.Cast<Window>()
                        .FirstOrDefault(w => new System.Windows.Interop.WindowInteropHelper(w).Handle == handle.Value);
                    if (window != null)
                    {
                        SetOwner(window);
                    }
                }
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            if (_targetControl != null && _targetControl.IsHandleCreated)
            {
                // Get the screen position of the target control (STNodeEditorMain)
                var controlLocation = _targetControl.PointToScreen(new System.Drawing.Point(0, 0));
                
                // Position the window to the right of the target control
                Left = (controlLocation.X + _targetControl.Width + 10  ) * 96 / DpiX - this.ActualWidth; 
                Top = controlLocation.Y * 96 / DpiX;
            }
            else if (_owner != null && _owner.IsLoaded)
            {
                // Fallback: Position the window to the right of the owner
                Left = (_owner.Left + _owner.ActualWidth + 10 ) * 96 / DpiX - this.ActualWidth;
                Top = _owner.Top * 96 / DpiX;
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Hide window when it loses focus
            //Hide();
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
            if (PropertyGridHost == null) return;
            // Show SignStackPanel, hide PropertyGrid
            PropertyGridHost.Visibility = Visibility.Collapsed;
            SignStackScrollViewer.Visibility = Visibility.Visible;
        }

        private void ToggleEditMode_Unchecked(object sender, RoutedEventArgs e)
        {
            if (PropertyGridHost == null) return;
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
