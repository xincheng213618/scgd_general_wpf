using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace ColorVision.UI
{
    public partial class DisPlayManager
    {
        private const string DragDataFormat = "ColorVision.UI.DisPlayControl";
        private static readonly TimeSpan DisplayDragPressDelay = TimeSpan.FromMilliseconds(260);
        private IDisPlayControl? _dragSourceControl;
        private Point _dragStartPoint;
        private DateTime _dragStartTime;

        private static void AttachGroupDropFeedback(Border border)
        {
            border.MouseEnter += (s, e) => SetGroupHeaderHighlight(border, true);
            border.MouseLeave += (s, e) => SetGroupHeaderHighlight(border, false);
            border.DragEnter += GroupDropTarget_DragEnter;
            border.DragOver += GroupDropTarget_DragOver;
            border.DragLeave += GroupDropTarget_DragLeave;
            border.Drop += GroupDropTarget_Drop;
        }

        private static void GroupDropTarget_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Border border && e.Data.GetDataPresent(DragDataFormat))
            {
                SetGroupHeaderHighlight(border, true);
                e.Effects = DragDropEffects.Move;
            }
        }

        private static void GroupDropTarget_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DragDataFormat))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private static void GroupDropTarget_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                SetGroupHeaderHighlight(border, false);
        }

        private static void GroupDropTarget_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                SetGroupHeaderHighlight(border, false);
        }

        private static void SetGroupHeaderHighlight(Border border, bool isHighlighted)
        {
            border.SetResourceReference(Border.BackgroundProperty, isHighlighted ? "GlobalBorderBrush1" : "GlobalBackground");
        }

        private void DisplayControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSourceControl = null;
            if (sender is IDisPlayControl disPlayControl && sender is DependencyObject owner && CanStartDisplayDrag(e.OriginalSource as DependencyObject, owner))
            {
                _dragSourceControl = disPlayControl;
                _dragStartPoint = e.GetPosition(null);
                _dragStartTime = DateTime.UtcNow;
            }
        }

        private void DisplayControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ClearDisplayDragCandidate();

        private void DisplayControl_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => ClearDisplayDragCandidate();

        private void DisplayControl_ContextMenuOpening(object sender, ContextMenuEventArgs e) => ClearDisplayDragCandidate();

        private void DisplayControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragSourceControl == null)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ClearDisplayDragCandidate();
                return;
            }

            if (DateTime.UtcNow - _dragStartTime < DisplayDragPressDelay)
                return;

            Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (_dragSourceControl is UserControl userControl)
            {
                var data = new DataObject(DragDataFormat, GetControlKey(_dragSourceControl));
                try
                {
                    DragDrop.DoDragDrop(userControl, data, DragDropEffects.Move);
                }
                finally
                {
                    ClearDisplayDragCandidate();
                }
            }
        }

        private void ClearDisplayDragCandidate() => _dragSourceControl = null;

        private static bool CanStartDisplayDrag(DependencyObject? source, DependencyObject owner)
        {
            ToggleButton? headerToggle = null;
            while (source != null)
            {
                if (source is TextBoxBase || source is Slider || source is ComboBox || source is ScrollBar || source is Thumb)
                    return false;

                if (source is FrameworkElement { Name: "DisPlayBorder" })
                    return false;

                if (headerToggle == null && source is ToggleButton toggleButton && IsDisplayHeaderToggle(toggleButton))
                    headerToggle = toggleButton;

                if (ReferenceEquals(source, owner))
                    return headerToggle != null;

                source = GetDisplayDragParent(source);
            }
            return headerToggle != null;
        }

        private static DependencyObject? GetDisplayDragParent(DependencyObject source)
        {
            if (source is Visual || source is Visual3D)
                return VisualTreeHelper.GetParent(source);

            if (source is FrameworkContentElement contentElement)
                return contentElement.Parent;

            return LogicalTreeHelper.GetParent(source);
        }

        private static bool IsDisplayHeaderToggle(ToggleButton toggleButton)
        {
            return toggleButton.Style != null && ReferenceEquals(toggleButton.Style, toggleButton.TryFindResource("ButtonPageControl1"));
        }

        private void DisplayControl_Drop(object sender, DragEventArgs e)
        {
            if (sender is not IDisPlayControl targetControl || !TryGetDraggedControlKey(e, out string draggedKey))
                return;

            string targetGroupId = GetGroupKey(targetControl);
            var controls = GetControlsInGroup(targetGroupId).Where(a => GetControlKey(a) != draggedKey).ToList();
            int targetIndex = controls.FindIndex(a => ReferenceEquals(a, targetControl));
            if (targetIndex < 0)
                targetIndex = controls.Count;

            if (sender is FrameworkElement targetElement && e.GetPosition(targetElement).Y > targetElement.ActualHeight / 2)
                targetIndex++;

            MoveControlToGroup(draggedKey, targetGroupId, targetIndex);
            e.Handled = true;
        }

        private void Group_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedControlKey(e, out string draggedKey))
                return;

            string groupId = (sender as FrameworkElement)?.Tag as string ?? DisPlayManagerConfig.DefaultGroupId;
            MoveControlToGroup(draggedKey, groupId, GetControlsInGroup(groupId).Count);
            e.Handled = true;
        }

        private void StackPanel_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedControlKey(e, out string draggedKey))
                return;

            MoveControlToGroup(draggedKey, DisPlayManagerConfig.DefaultGroupId, GetControlsInGroup(DisPlayManagerConfig.DefaultGroupId).Count);
            e.Handled = true;
        }

        private static bool TryGetDraggedControlKey(DragEventArgs e, out string controlKey)
        {
            controlKey = e.Data.GetDataPresent(DragDataFormat)
                ? e.Data.GetData(DragDataFormat) as string ?? string.Empty
                : string.Empty;

            return !string.IsNullOrWhiteSpace(controlKey);
        }
    }
}
