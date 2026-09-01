using ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration;
using ColorVision.Themes;
using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    public partial class FlowEditorCanvas : UserControl, IDisposable
    {
        internal const double PropertyPanelMaxWidth = 360;
        internal const double PropertyPanelMaxHeight = 520;
        internal const double PropertyPanelNodeGap = 10;

        private bool _forwardingEditCommand;
        private bool _hidePropertyEditorUntilNextSelection;
        private bool _propertyPanelPositionUpdatePending;
        private bool _fitCanvasToNodesPending;
        private bool _fitCanvasToNodesScheduled;
        private bool _showNodeDocumentation;
        private bool _hasEnteredCanvasEditMode;
        private float _fitCanvasMaximumScale = 0.85f;
        private bool _disposed;
        private readonly StackPanel _generatedPropertyPanel = new();
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
            new PropertyMetadata(new Thickness(0, 54, 10, 10), OnPropertyPanelMarginChanged));

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
        public StackPanel NodePropertyPanel => NodePropertyPanelContent;

        public FlowEditorCanvas()
        {
            InitializeComponent();
            ConfigurationViewButton.Content = GetInspectorText("Flow_NodeInspector_Configuration");
            DocumentationViewButton.Content = GetInspectorText("Flow_NodeInspector_Documentation");
            STNodeEditorMain.EnableHistory = true;
            AttachEditCommandRouting(this);
            STNodeEditorMain.EnableBlankLeftDragCanvasChanged += STNodeEditorMain_EnableBlankLeftDragCanvasChanged;
            CanvasDragLockButton.SetCurrentValue(
                ToggleButton.IsCheckedProperty,
                STNodeEditorMain.EnableBlankLeftDragCanvas);
            SizeChanged += FlowEditorCanvas_SizeChanged;
            STNodeEditorMain.SizeChanged += STNodeEditorMain_SizeChanged;
            PropertyEditorPanel.IsVisibleChanged += PropertyEditorPanel_IsVisibleChanged;
            PropertyEditorPanel.SizeChanged += PropertyEditorPanel_SizeChanged;
            STNodeEditorMain.ActiveChanged += STNodeEditorMain_SelectionChanged;
            STNodeEditorMain.SelectedChanged += STNodeEditorMain_SelectionChanged;
            STNodeEditorMain.NodeLocationChanged += STNodeEditorMain_NodeLocationChanged;
            STNodeEditorMain.PreviewMouseLeftButtonDown += STNodeEditorMain_PreviewMouseLeftButtonDown;
            STNodeEditorMain.CanvasMoved += STNodeEditorMain_CanvasChanged;
            STNodeEditorMain.CanvasScaled += STNodeEditorMain_CanvasChanged;

            ThemeManager.Current.CurrentUIThemeChanged += ThemeChanged;
            ThemeChanged(ThemeManager.Current.CurrentUITheme);
            AdvancedPropertiesButton.ToolTip = FlowNodePropertyMetadataProvider.AdvancedOptions.ToolTip;
            UpdateInspectorViewButtons();
        }

        private static string GetInspectorText(string key)
        {
            return Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }

        private static void OnPropertyPanelMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FlowEditorCanvas)d).QueuePropertyPanelPositionUpdate();
        }

        private void FlowEditorCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            PreserveCanvasCenter(e.PreviousSize, e.NewSize);
            QueuePendingCanvasFit();
            UpdatePropertyPanelSizeLimit();
            QueuePropertyPanelPositionUpdate();
        }

        private void STNodeEditorMain_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            QueuePendingCanvasFit();
        }

        internal void FitCanvasToNodesAfterLayout(float maximumScale = 0.85f)
        {
            _fitCanvasMaximumScale = Math.Clamp(maximumScale, 0.2f, 5f);
            _fitCanvasToNodesPending = true;
            QueuePendingCanvasFit();
        }

        private void QueuePendingCanvasFit()
        {
            if (_disposed ||
                !_fitCanvasToNodesPending ||
                _fitCanvasToNodesScheduled ||
                ActualWidth <= 0 ||
                ActualHeight <= 0)
                return;

            _fitCanvasToNodesScheduled = true;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _fitCanvasToNodesScheduled = false;
                if (_disposed || !_fitCanvasToNodesPending)
                    return;

                if (STNodeEditorMain.ClientSize.Width <= 0 || STNodeEditorMain.ClientSize.Height <= 0)
                    return;

                _fitCanvasToNodesPending = false;
                STNodeEditorMain.FitCanvasToNodes(_fitCanvasMaximumScale);
            }));
        }

        private void PreserveCanvasCenter(System.Windows.Size previousSize, System.Windows.Size newSize)
        {
            if (STNodeEditorMain.Nodes.Count == 0 ||
                previousSize.Width <= 0 ||
                previousSize.Height <= 0 ||
                newSize.Width <= 0 ||
                newSize.Height <= 0)
                return;

            float offsetX = (float)((newSize.Width - previousSize.Width) / 2);
            float offsetY = (float)((newSize.Height - previousSize.Height) / 2);
            if (offsetX == 0 && offsetY == 0)
                return;

            STNodeEditorMain.MoveCanvas(
                STNodeEditorMain.CanvasOffsetX + offsetX,
                STNodeEditorMain.CanvasOffsetY + offsetY,
                bAnimation: false,
                CanvasMoveArgs.All);
        }

        private void PropertyEditorPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (PropertyEditorPanel.IsVisible)
            {
                UpdatePropertyPanelSizeLimit();
                QueuePropertyPanelPositionUpdate();
            }
        }

        private void PropertyEditorPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            QueuePropertyPanelPositionUpdate();
        }

        private void STNodeEditorMain_SelectionChanged(object? sender, EventArgs e)
        {
            if (!_hasEnteredCanvasEditMode && STNodeEditorMain.GetSelectedNode().Length > 0)
            {
                _hasEnteredCanvasEditMode = true;
                STNodeEditorMain.EnableBlankLeftDragCanvas = false;
            }

            _hidePropertyEditorUntilNextSelection = false;
            RefreshNodePropertyPanel();
            QueuePropertyPanelPositionUpdate();
        }

        internal void ResetCanvasInteractionMode()
        {
            _hasEnteredCanvasEditMode = false;
            STNodeEditorMain.EnableBlankLeftDragCanvas = true;
        }

        private void STNodeEditorMain_NodeLocationChanged(object? sender, EventArgs e)
        {
            _hidePropertyEditorUntilNextSelection = true;
            HideNodePropertyPanel();
        }

        private void STNodeEditorMain_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_hidePropertyEditorUntilNextSelection)
                return;

            System.Windows.Point mousePosition = e.GetPosition(STNodeEditorMain);
            var canvasPosition = STNodeEditorMain.ControlToCanvas(new PointF(
                (float)mousePosition.X,
                (float)mousePosition.Y));
            if (STNodeEditorMain.FindNodeFromPoint(canvasPosition).Node == null)
                return;

            _hidePropertyEditorUntilNextSelection = false;
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(RefreshNodePropertyPanel));
        }

        private void STNodeEditorMain_CanvasChanged(object? sender, EventArgs e)
        {
            QueuePropertyPanelPositionUpdate();
        }

        internal static bool ShouldShowPropertyEditor(STNodeEditor nodeEditor)
        {
            STNode? activeNode = nodeEditor.ActiveNode;
            if (activeNode == null || !activeNode.IsSelected)
                return false;

            STNode[] selectedNodes = nodeEditor.GetSelectedNode();
            return selectedNodes.Length == 1 && ReferenceEquals(selectedNodes[0], activeNode);
        }

        public void RefreshNodePropertyPanel()
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(RefreshNodePropertyPanel));
                return;
            }

            StackPanel signPanel = NodePropertyPanel;
            bool wasPropertyPanelVisible = PropertyEditorPanel.Visibility == Visibility.Visible;
            signPanel.Children.Clear();

            STNode? activeNode = STNodeEditorMain.ActiveNode;
            if (_hidePropertyEditorUntilNextSelection || !ShouldShowPropertyEditor(STNodeEditorMain))
            {
                signPanel.Visibility = Visibility.Collapsed;
                PropertyEditorPanel.Visibility = Visibility.Collapsed;
                return;
            }

            NodeInspectorTitle.Text = activeNode!.GetType().Name;

            if (_showNodeDocumentation)
            {
                signPanel.Children.Add(FlowNodeDocumentationPresenter.Create(activeNode!));
            }
            else
            {
                UniformGrid commandGrid = new()
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                PropertyEditorHelper.GenCommand(activeNode!, commandGrid, compact: true);
                if (commandGrid.Children.Count > 0)
                    signPanel.Children.Add(commandGrid);

                var configurator = NodeConfiguratorRegistry.GetConfigurator(activeNode!.GetType());
                if (configurator != null)
                {
                    var context = new NodeConfiguratorContext
                    {
                        Node = activeNode,
                        SignStackPanel = signPanel,
                        STNodeEditor = STNodeEditorMain,
                        Refresh = RefreshNodePropertyPanel
                    };
                    configurator.Configure(context);
                }

                _generatedPropertyPanel.Children.Clear();
                var resourceManager = PropertyEditorHelper.GetResourceManager(activeNode);
                _generatedPropertyPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(
                    activeNode,
                    resourceManager,
                    metadataProvider: FlowNodePropertyMetadataProvider.Instance,
                    advancedOptions: FlowNodePropertyMetadataProvider.AdvancedOptions));
                signPanel.Children.Add(_generatedPropertyPanel);
            }
            signPanel.Visibility = signPanel.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (signPanel.Visibility != Visibility.Visible)
            {
                PropertyEditorPanel.Visibility = Visibility.Collapsed;
                return;
            }

            if (wasPropertyPanelVisible)
            {
                PropertyEditorPanel.Visibility = Visibility.Visible;
                return;
            }

            PreparePropertyPanelForFirstRender();
        }

        internal bool IsShowingNodeDocumentation => _showNodeDocumentation;

        internal void ShowNodeDocumentation(bool showDocumentation)
        {
            if (_showNodeDocumentation == showDocumentation)
                return;

            _showNodeDocumentation = showDocumentation;
            UpdateInspectorViewButtons();
            RefreshNodePropertyPanel();
            QueuePropertyPanelPositionUpdate();
        }

        private void ConfigurationViewButton_Click(object sender, RoutedEventArgs e)
        {
            ShowNodeDocumentation(false);
        }

        private void DocumentationViewButton_Click(object sender, RoutedEventArgs e)
        {
            ShowNodeDocumentation(true);
        }

        private void AdvancedPropertiesButton_Checked(object sender, RoutedEventArgs e)
        {
            SetAdvancedPropertiesVisibility(true);
        }

        private void AdvancedPropertiesButton_Unchecked(object sender, RoutedEventArgs e)
        {
            SetAdvancedPropertiesVisibility(false);
        }

        private void SetAdvancedPropertiesVisibility(bool showAdvancedProperties)
        {
            if (FlowNodePropertyMetadataProvider.AdvancedOptions.ShowAdvancedProperties == showAdvancedProperties)
                return;

            FlowNodePropertyMetadataProvider.AdvancedOptions.ShowAdvancedProperties = showAdvancedProperties;
            UpdateInspectorViewButtons();
            RefreshNodePropertyPanel();
            QueuePropertyPanelPositionUpdate();
        }

        private void UpdateInspectorViewButtons()
        {
            ConfigurationViewButton.FontWeight = _showNodeDocumentation ? FontWeights.Normal : FontWeights.SemiBold;
            DocumentationViewButton.FontWeight = _showNodeDocumentation ? FontWeights.SemiBold : FontWeights.Normal;
            ConfigurationViewButton.Opacity = _showNodeDocumentation ? 0.65 : 1;
            DocumentationViewButton.Opacity = _showNodeDocumentation ? 1 : 0.65;
            ConfigurationViewButton.SetResourceReference(Control.ForegroundProperty, _showNodeDocumentation ? "GlobalTextBrush" : "PrimaryBrush");
            DocumentationViewButton.SetResourceReference(Control.ForegroundProperty, _showNodeDocumentation ? "PrimaryBrush" : "GlobalTextBrush");
            AdvancedPropertiesButton.Visibility = _showNodeDocumentation ? Visibility.Collapsed : Visibility.Visible;
            AdvancedPropertiesButton.IsChecked = FlowNodePropertyMetadataProvider.AdvancedOptions.ShowAdvancedProperties;
            AdvancedPropertiesButton.SetResourceReference(Control.ForegroundProperty,
                FlowNodePropertyMetadataProvider.AdvancedOptions.ShowAdvancedProperties ? "PrimaryBrush" : "GlobalTextBrush");
        }

        public void HideNodePropertyPanel()
        {
            PropertyEditorPanel.Visibility = Visibility.Collapsed;
        }

        private void UpdatePropertyPanelSizeLimit()
        {
            double availableWidth = Math.Max(0, ActualWidth - PropertyPanelMargin.Left - PropertyPanelMargin.Right);
            double availableHeight = Math.Max(0, ActualHeight - PropertyPanelMargin.Top - PropertyPanelMargin.Bottom);
            if (availableWidth > 0)
                PropertyEditorPanel.MaxWidth = Math.Min(PropertyPanelMaxWidth, availableWidth);
            if (availableHeight > 0)
                PropertyEditorPanel.MaxHeight = Math.Min(PropertyPanelMaxHeight, availableHeight);
        }

        private void PreparePropertyPanelForFirstRender()
        {
            UpdatePropertyPanelSizeLimit();
            PreparePropertyPanelForFirstRender(
                PropertyEditorPanel,
                new System.Windows.Size(PropertyEditorPanel.MaxWidth, PropertyEditorPanel.MaxHeight),
                panelSize => UpdatePropertyPanelPosition(panelSize, allowHidden: true));
        }

        internal static void PreparePropertyPanelForFirstRender(
            FrameworkElement propertyPanel,
            System.Windows.Size measureConstraint,
            Action<System.Windows.Size> updatePosition)
        {
            ArgumentNullException.ThrowIfNull(propertyPanel);
            ArgumentNullException.ThrowIfNull(updatePosition);

            propertyPanel.Visibility = Visibility.Hidden;
            propertyPanel.Measure(measureConstraint);
            updatePosition(propertyPanel.DesiredSize);
            propertyPanel.Visibility = Visibility.Visible;
        }

        private void QueuePropertyPanelPositionUpdate()
        {
            if (_disposed || _propertyPanelPositionUpdatePending || PropertyEditorPanel.Visibility != Visibility.Visible)
                return;

            _propertyPanelPositionUpdatePending = true;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _propertyPanelPositionUpdatePending = false;
                UpdatePropertyPanelPosition(
                    new System.Windows.Size(PropertyEditorPanel.ActualWidth, PropertyEditorPanel.ActualHeight));
            }));
        }

        private void UpdatePropertyPanelPosition(System.Windows.Size panelSize, bool allowHidden = false)
        {
            STNode? activeNode = STNodeEditorMain.ActiveNode;
            if (_disposed ||
                (PropertyEditorPanel.Visibility != Visibility.Visible &&
                 (!allowHidden || PropertyEditorPanel.Visibility != Visibility.Hidden)) ||
                activeNode == null ||
                !activeNode.IsSelected)
                return;

            System.Drawing.Rectangle nodeRectangle = STNodeEditorMain.CanvasToControl(activeNode.Rectangle);
            var nodeBounds = new Rect(nodeRectangle.X, nodeRectangle.Y, nodeRectangle.Width, nodeRectangle.Height);
            var viewportSize = new System.Windows.Size(ActualWidth, ActualHeight);
            System.Windows.Point position = CalculatePropertyPanelPosition(
                nodeBounds,
                panelSize,
                viewportSize,
                PropertyPanelMargin);

            Canvas.SetLeft(PropertyEditorPanel, position.X);
            Canvas.SetTop(PropertyEditorPanel, position.Y);
        }

        internal static System.Windows.Point CalculatePropertyPanelPosition(
            Rect nodeBounds,
            System.Windows.Size panelSize,
            System.Windows.Size viewportSize,
            Thickness safeArea)
        {
            double leftBound = Math.Max(0, safeArea.Left);
            double topBound = Math.Max(0, safeArea.Top);
            double rightBound = Math.Max(leftBound, viewportSize.Width - Math.Max(0, safeArea.Right));
            double bottomBound = Math.Max(topBound, viewportSize.Height - Math.Max(0, safeArea.Bottom));
            double panelWidth = Math.Min(Math.Max(0, panelSize.Width), rightBound - leftBound);
            double panelHeight = Math.Min(Math.Max(0, panelSize.Height), bottomBound - topBound);
            double maxLeft = Math.Max(leftBound, rightBound - panelWidth);
            double maxTop = Math.Max(topBound, bottomBound - panelHeight);
            double rightCandidate = nodeBounds.Right + PropertyPanelNodeGap;
            double leftCandidate = nodeBounds.Left - PropertyPanelNodeGap - panelWidth;

            double left;
            if (rightCandidate <= maxLeft)
            {
                left = rightCandidate;
            }
            else if (leftCandidate >= leftBound)
            {
                left = leftCandidate;
            }
            else
            {
                double rightSpace = rightBound - nodeBounds.Right;
                double leftSpace = nodeBounds.Left - leftBound;
                left = rightSpace >= leftSpace ? maxLeft : leftBound;
            }

            return new System.Windows.Point(
                Math.Clamp(left, leftBound, maxLeft),
                Math.Clamp(nodeBounds.Top, topBound, maxTop));
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
                    ToggleButton.IsCheckedProperty,
                    STNodeEditorMain.EnableBlankLeftDragCanvas);
            }

            if (Dispatcher.CheckAccess())
            {
                UpdateToggle();
            }
            else
            {
                _ = Dispatcher.BeginInvoke(UpdateToggle);
            }
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

        public void Dispose()
        {
            _disposed = true;
            ThemeManager.Current.CurrentUIThemeChanged -= ThemeChanged;
            SizeChanged -= FlowEditorCanvas_SizeChanged;
            STNodeEditorMain.SizeChanged -= STNodeEditorMain_SizeChanged;
            PropertyEditorPanel.IsVisibleChanged -= PropertyEditorPanel_IsVisibleChanged;
            PropertyEditorPanel.SizeChanged -= PropertyEditorPanel_SizeChanged;
            STNodeEditorMain.EnableBlankLeftDragCanvasChanged -= STNodeEditorMain_EnableBlankLeftDragCanvasChanged;
            STNodeEditorMain.ActiveChanged -= STNodeEditorMain_SelectionChanged;
            STNodeEditorMain.SelectedChanged -= STNodeEditorMain_SelectionChanged;
            STNodeEditorMain.NodeLocationChanged -= STNodeEditorMain_NodeLocationChanged;
            STNodeEditorMain.PreviewMouseLeftButtonDown -= STNodeEditorMain_PreviewMouseLeftButtonDown;
            STNodeEditorMain.CanvasMoved -= STNodeEditorMain_CanvasChanged;
            STNodeEditorMain.CanvasScaled -= STNodeEditorMain_CanvasChanged;
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
