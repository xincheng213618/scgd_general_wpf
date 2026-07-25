using ColorVision.Themes;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.Flow
{
    public partial class FlowEditorCanvas : UserControl, IDisposable
    {
        private bool _forwardingEditCommand;
        private readonly List<(UIElement Host, CommandBinding Binding)> _editCommandForwarders = [];

        public static readonly DependencyProperty ToolbarContentProperty = DependencyProperty.Register(
            nameof(ToolbarContent),
            typeof(object),
            typeof(FlowEditorCanvas),
            new PropertyMetadata(null));

        public static readonly DependencyProperty PropertyPanelMarginProperty = DependencyProperty.Register(
            nameof(PropertyPanelMargin),
            typeof(Thickness),
            typeof(FlowEditorCanvas),
            new PropertyMetadata(new Thickness(0, 54, 10, 10)));

        public object? ToolbarContent
        {
            get => GetValue(ToolbarContentProperty);
            set => SetValue(ToolbarContentProperty, value);
        }

        public Thickness PropertyPanelMargin
        {
            get => (Thickness)GetValue(PropertyPanelMarginProperty);
            set => SetValue(PropertyPanelMarginProperty, value);
        }

        public STNodeEditor NodeEditor => STNodeEditorMain;
        public Grid NodePropertyPanelContainer => PropertyEditorPanel;
        public FlowNodePropertyPanel NodePropertyPanel => InlineNodePropertyPanel;

        public FlowEditorCanvas()
        {
            InitializeComponent();
            STNodeEditorMain.EnableHistory = true;
            AttachEditCommandRouting(this);
            STNodeEditorMain.EnableBlankLeftDragCanvasChanged += STNodeEditorMain_EnableBlankLeftDragCanvasChanged;
            CanvasDragLockButton.SetCurrentValue(
                System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                STNodeEditorMain.EnableBlankLeftDragCanvas);

            ThemeManager.Current.CurrentUIThemeChanged += ThemeChanged;
            ThemeChanged(ThemeManager.Current.CurrentUITheme);
        }

        public void AttachEditCommandRouting(UIElement host)
        {
            ArgumentNullException.ThrowIfNull(host);
            if (_editCommandForwarders.Exists(item => ReferenceEquals(item.Host, host)))
                return;

            RoutedCommand[] commands =
            [
                ApplicationCommands.Undo,
                ApplicationCommands.Redo,
                ApplicationCommands.Cut,
                ApplicationCommands.Copy,
                ApplicationCommands.Paste,
                ApplicationCommands.Delete,
                ApplicationCommands.SelectAll
            ];

            foreach (RoutedCommand command in commands)
            {
                CommandBinding binding = new CommandBinding(
                    command,
                    ForwardEditCommand,
                    CanForwardEditCommand);
                host.CommandBindings.Add(binding);
                _editCommandForwarders.Add((host, binding));
            }
        }

        private void CanForwardEditCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            if (_forwardingEditCommand ||
                IsWithinNodeEditor(e.OriginalSource as DependencyObject) ||
                ShouldPreserveNativeTextCommand(e.Command, e.OriginalSource as DependencyObject))
                return;

            _forwardingEditCommand = true;
            try
            {
                e.CanExecute = ((RoutedCommand)e.Command).CanExecute(e.Parameter, STNodeEditorMain);
                e.Handled = true;
            }
            finally
            {
                _forwardingEditCommand = false;
            }
        }

        private void ForwardEditCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (_forwardingEditCommand ||
                IsWithinNodeEditor(e.OriginalSource as DependencyObject) ||
                ShouldPreserveNativeTextCommand(e.Command, e.OriginalSource as DependencyObject))
                return;

            _forwardingEditCommand = true;
            try
            {
                ((RoutedCommand)e.Command).Execute(e.Parameter, STNodeEditorMain);
                e.Handled = true;
            }
            finally
            {
                _forwardingEditCommand = false;
            }
        }

        private bool ShouldPreserveNativeTextCommand(ICommand command, DependencyObject? commandSource)
        {
            bool isTextCommand =
                command == ApplicationCommands.Undo ||
                command == ApplicationCommands.Redo ||
                command == ApplicationCommands.Cut ||
                command == ApplicationCommands.Copy ||
                command == ApplicationCommands.Paste ||
                command == ApplicationCommands.Delete ||
                command == ApplicationCommands.SelectAll;
            if (!isTextCommand)
                return false;

            if (commandSource != null && IsWithinTextEditor(commandSource))
                return true;
            if (Keyboard.FocusedElement is DependencyObject keyboardFocus &&
                IsWithinTextEditor(keyboardFocus))
                return true;

            DependencyObject focusScope = Window.GetWindow(this) ?? FocusManager.GetFocusScope(this);
            return FocusManager.GetFocusedElement(focusScope) is DependencyObject logicalFocus &&
                   IsWithinTextEditor(logicalFocus);
        }

        private static bool IsWithinTextEditor(DependencyObject source)
        {
            DependencyObject? current = source;
            while (current != null)
            {
                if (current is TextBoxBase or PasswordBox)
                    return true;
                current = GetParent(current);
            }
            return false;
        }

        private bool IsWithinNodeEditor(DependencyObject? source)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, STNodeEditorMain))
                    return true;
                source = GetParent(source);
            }
            return false;
        }

        private static DependencyObject? GetParent(DependencyObject source)
        {
            return source switch
            {
                System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D =>
                    System.Windows.Media.VisualTreeHelper.GetParent(source),
                FrameworkContentElement contentElement => contentElement.Parent,
                _ => LogicalTreeHelper.GetParent(source)
            };
        }

        private void ThemeChanged(Theme theme)
        {
            if (theme == Theme.Dark)
            {
                STNodeEditorMain.BackColor = Color.FromArgb(255, 34, 34, 34);
                STNodeEditorMain.GridColor = Color.FromArgb(255, 0, 0, 0);
                STNodeEditorMain.ForeColor = Color.FromArgb(255, 255, 255, 255);
                STNodeEditorMain.LocationBackColor = Color.FromArgb(255, 50, 50, 50);
            }
            else
            {
                STNodeEditorMain.BackColor = Color.FromArgb(255, 150, 150, 150);
                STNodeEditorMain.GridColor = Color.FromArgb(255, 0, 0, 0);
                STNodeEditorMain.ForeColor = Color.FromArgb(255, 0, 0, 0);
                STNodeEditorMain.LocationBackColor = Color.FromArgb(255, 200, 200, 200);
            }
        }

        private void CanvasDragLockButton_Checked(object sender, RoutedEventArgs e)
        {
            STNodeEditorMain.EnableBlankLeftDragCanvas = true;
        }

        private void CanvasDragLockButton_Unchecked(object sender, RoutedEventArgs e)
        {
            STNodeEditorMain.EnableBlankLeftDragCanvas = false;
        }

        private void STNodeEditorMain_EnableBlankLeftDragCanvasChanged(object? sender, EventArgs e)
        {
            void UpdateToggle()
            {
                CanvasDragLockButton.SetCurrentValue(
                    System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                    STNodeEditorMain.EnableBlankLeftDragCanvas);
            }

            if (Dispatcher.CheckAccess())
            {
                UpdateToggle();
            }
            else
            {
                Dispatcher.BeginInvoke(UpdateToggle);
            }
        }

        public void Dispose()
        {
            ThemeManager.Current.CurrentUIThemeChanged -= ThemeChanged;
            STNodeEditorMain.EnableBlankLeftDragCanvasChanged -= STNodeEditorMain_EnableBlankLeftDragCanvasChanged;
            foreach ((UIElement host, CommandBinding binding) in _editCommandForwarders)
            {
                host.CommandBindings.Remove(binding);
            }
            _editCommandForwarders.Clear();
            STNodeEditorMain.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
