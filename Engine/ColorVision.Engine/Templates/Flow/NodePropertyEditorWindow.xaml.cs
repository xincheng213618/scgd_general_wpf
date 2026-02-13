using ColorVision.Common.MVVM;
using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow
{
    public class NodePropertyEditorConfig : ViewModelBase, IConfig
    {
        public bool IsPropertyEditor { get => _IsPropertyEditor; set { _IsPropertyEditor = value; OnPropertyChanged(); } }
        private bool _IsPropertyEditor = true;

    }

    public class NodePropertyEditorVM : ViewModelBase
    {
        public NodePropertyEditorConfig Config { get; set; }
        public RelayCommand EditCommand { get; set; }
        public NodePropertyEditorVM()
        {
            Config = ConfigService.Instance.GetRequiredService<NodePropertyEditorConfig>();
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(),WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }
    }
    
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
        NodePropertyEditorVM NodePropertyEditorVM { get; set; }
        private void Window_Initialized(object sender, EventArgs e)
        {
            NodePropertyEditorVM = new NodePropertyEditorVM();
            this.DataContext = NodePropertyEditorVM;
        }

        public static float DpiX { get; set; }

        private void NodePropertyEditorWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
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
            //Hide();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            UpdatePosition();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            PropertyGridHost.Dispose();
        }


        public void CloseWindow()
        {
            _allowClose = true;
            Close();
        }
        public void ShowPropertyEditor()
        {
            try
            {
                if (!IsVisible)
                {
                    Show();
                }
                
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
        private void ToggleEditMode_Click(object sender, RoutedEventArgs e)
        {
            NodePropertyEditorVM.Config.IsPropertyEditor = !NodePropertyEditorVM.Config.IsPropertyEditor;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
