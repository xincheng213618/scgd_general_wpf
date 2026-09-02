using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Solution.Explorer
{
    /// <summary>Softens the explorer's themed scrollbar without replacing its template or hit area.</summary>
    public static class ExplorerScrollBarBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(ExplorerScrollBarBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "State", typeof(ScrollBarState), typeof(ExplorerScrollBarBehavior));

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not TreeView tree)
                return;

            if (tree.GetValue(StateProperty) is ScrollBarState previous)
            {
                previous.Dispose();
                tree.ClearValue(StateProperty);
            }
            if ((bool)e.NewValue)
                tree.SetValue(StateProperty, new ScrollBarState(tree));
        }

        private sealed class ScrollBarState : IDisposable
        {
            private readonly TreeView _tree;
            private DependencyObject? _templateRoot;
            private ScrollViewer? _viewer;
            private ControlTemplate? _viewerTemplate;
            private ScrollBarMask? _verticalMask;
            private ScrollBarMask? _horizontalMask;

            public ScrollBarState(TreeView tree)
            {
                _tree = tree;
                tree.Loaded += OnLoaded;
                tree.Unloaded += OnUnloaded;
                if (tree.IsLoaded)
                    Start();
            }

            private void OnLoaded(object sender, RoutedEventArgs e) => Start();

            private void OnUnloaded(object sender, RoutedEventArgs e) => Stop();

            private void Start()
            {
                _tree.LayoutUpdated -= OnLayoutUpdated;
                _tree.LayoutUpdated += OnLayoutUpdated;
                UpdateScrollBars();
            }

            private void Stop()
            {
                _tree.LayoutUpdated -= OnLayoutUpdated;
                UpdateMask(ref _verticalMask, null);
                UpdateMask(ref _horizontalMask, null);
                _templateRoot = null;
                _viewer = null;
                _viewerTemplate = null;
            }

            private void OnLayoutUpdated(object? sender, EventArgs e) => UpdateScrollBars();

            private void UpdateScrollBars()
            {
                DependencyObject? templateRoot = VisualTreeHelper.GetChildrenCount(_tree) > 0
                    ? VisualTreeHelper.GetChild(_tree, 0)
                    : null;
                if (!ReferenceEquals(templateRoot, _templateRoot)
                    || _viewer == null
                    || !IsDescendantOf(_viewer, _tree))
                {
                    _templateRoot = templateRoot;
                    _viewer = FindScrollViewer(templateRoot);
                    _viewerTemplate = null;
                    _viewer?.ApplyTemplate();
                }

                if (_viewer != null && !ReferenceEquals(_viewerTemplate, _viewer.Template))
                {
                    _viewer.ApplyTemplate();
                    _viewerTemplate = _viewer.Template;
                }
                UpdateMask(ref _verticalMask, _viewerTemplate?.FindName("PART_VerticalScrollBar", _viewer!) as ScrollBar);
                UpdateMask(ref _horizontalMask, _viewerTemplate?.FindName("PART_HorizontalScrollBar", _viewer!) as ScrollBar);
            }

            private static ScrollViewer? FindScrollViewer(DependencyObject? element)
            {
                if (element is ScrollViewer viewer)
                    return viewer;
                if (element == null || element is ItemsPresenter or TreeViewItem)
                    return null;

                for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                {
                    if (FindScrollViewer(VisualTreeHelper.GetChild(element, index)) is { } descendant)
                        return descendant;
                }
                return null;
            }

            private static bool IsDescendantOf(DependencyObject element, DependencyObject ancestor)
            {
                for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
                {
                    if (ReferenceEquals(current, ancestor))
                        return true;
                }
                return false;
            }

            private static void UpdateMask(ref ScrollBarMask? current, ScrollBar? scrollBar)
            {
                if (ReferenceEquals(current?.ScrollBar, scrollBar))
                    return;
                current?.Dispose();
                current = scrollBar == null ? null : new ScrollBarMask(scrollBar);
            }

            public void Dispose()
            {
                _tree.Loaded -= OnLoaded;
                _tree.Unloaded -= OnUnloaded;
                Stop();
            }
        }

        private sealed class ScrollBarMask : IDisposable
        {
            private readonly MultiBinding? _binding;

            public ScrollBar ScrollBar { get; }

            public ScrollBarMask(ScrollBar scrollBar)
            {
                ScrollBar = scrollBar;
                if (scrollBar.ReadLocalValue(UIElement.OpacityMaskProperty) != DependencyProperty.UnsetValue)
                    return;

                _binding = new MultiBinding { Mode = BindingMode.OneWay, Converter = ScrollBarMaskConverter.Instance };
                _binding.Bindings.Add(new Binding(nameof(UIElement.IsMouseOver)) { Source = scrollBar });
                _binding.Bindings.Add(new Binding(nameof(UIElement.IsMouseCaptureWithin)) { Source = scrollBar });
                BindingOperations.SetBinding(scrollBar, UIElement.OpacityMaskProperty, _binding);
            }

            public void Dispose()
            {
                if (_binding != null && ReferenceEquals(BindingOperations.GetMultiBinding(ScrollBar, UIElement.OpacityMaskProperty), _binding))
                    BindingOperations.ClearBinding(ScrollBar, UIElement.OpacityMaskProperty);
            }
        }

        private sealed class ScrollBarMaskConverter : IMultiValueConverter
        {
            public static readonly ScrollBarMaskConverter Instance = new();
            private static readonly Brush IdleMask = CreateMask(0.35);
            private static readonly Brush HoverMask = CreateMask(0.65);
            private static readonly Brush CapturedMask = CreateMask(0.85);

            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                return values.Length > 1 && values[1] is true
                    ? CapturedMask
                    : values.Length > 0 && values[0] is true ? HoverMask : IdleMask;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();

            private static Brush CreateMask(double opacity)
            {
                var brush = new SolidColorBrush(Colors.Black) { Opacity = opacity };
                brush.Freeze();
                return brush;
            }
        }
    }
}
