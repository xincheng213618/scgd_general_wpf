using ColorVision.Themes;
using ST.Library.UI.NodeEditor;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow
{
    public partial class FlowEditorCanvas : UserControl, IDisposable
    {
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
            STNodeEditorMain.EnableBlankLeftDragCanvasChanged += STNodeEditorMain_EnableBlankLeftDragCanvasChanged;
            CanvasDragLockButton.SetCurrentValue(
                System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                STNodeEditorMain.EnableBlankLeftDragCanvas);

            ThemeManager.Current.CurrentUIThemeChanged += ThemeChanged;
            ThemeChanged(ThemeManager.Current.CurrentUITheme);
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
            STNodeEditorMain.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
