using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ColorVision.ImageEditor.Draw
{   
    
    public enum VisualChangeType { Add, Remove, Top, Clear }
    public class VisualChangedEventArgs : EventArgs
    {
        public Visual? Visual { get; }
        public VisualChangeType ChangeType { get; }
        public VisualChangedEventArgs(Visual? visual, VisualChangeType changeType)
        {
            Visual = visual;
            ChangeType = changeType;
        }
    }

    public class DrawCanvas : Image
    {
        // 使用只读集合，防止外部直接修改
        private readonly List<Visual> visuals = new();

        public IReadOnlyList<Visual> Visuals => visuals;


        public DrawCanvas()
        {
            this.Focusable = true;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => Undo(), (s, e) => { e.CanExecute = UndoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (s, e) => Redo(), (s, e) => { e.CanExecute = RedoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(Commands.UndoHistory, null, (s, e) =>{ e.CanExecute = UndoStack.Count > 0;  if (e.Parameter is MenuItem m1 && m1.ItemsSource != UndoStack) m1.ItemsSource = UndoStack; }));
        }
        #region ActionCommand
        public ObservableCollection<ActionCommand> UndoStack { get; } = new();
        public ObservableCollection<ActionCommand> RedoStack { get; } = new();

        public void ClearActionCommand()
        {
            UndoStack.Clear();
            RedoStack.Clear();
        }

        public void AddActionCommand(ActionCommand actionCommand)
        {
            UndoStack.Add(actionCommand);
            RedoStack.Clear();
        }

        public void Undo()
        {
            if (UndoStack.Count > 0)
            {
                var undoAction = UndoStack[^1];
                UndoStack.RemoveAt(UndoStack.Count - 1);
                undoAction.UndoAction();
                RedoStack.Add(undoAction);
            }
        }

        public void Redo()
        {
            if (RedoStack.Count > 0)
            {
                var redoAction = RedoStack[^1];
                RedoStack.RemoveAt(RedoStack.Count - 1);
                redoAction.RedoAction();
                UndoStack.Add(redoAction);
            }
        }
        #endregion

        #region doubleClick
        private DateTime lastClickTime;
        private const int DoubleClickTime = 300; // ms

        public static readonly RoutedEvent MouseDoubleClickEvent = EventManager.RegisterRoutedEvent("MouseDoubleClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(DrawCanvas));
        public event RoutedEventHandler MouseDoubleClick
        {
            add { AddHandler(MouseDoubleClickEvent, value); }
            remove { RemoveHandler(MouseDoubleClickEvent, value); }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DateTime now = DateTime.Now;
            if ((now - lastClickTime).TotalMilliseconds <= DoubleClickTime)
            {
                RaiseEvent(new RoutedEventArgs(MouseDoubleClickEvent));
            }
            lastClickTime = now;
        }
        #endregion


        protected override Visual GetVisualChild(int index) => visuals[index];
        protected override int VisualChildrenCount => visuals.Count;

        public bool ContainsVisual(Visual visual) => visuals.Contains(visual);


        public event EventHandler? ImageInitialized;
        public void RaiseImageInitialized() => ImageInitialized?.Invoke(this, EventArgs.Empty);

        // 事件使用 WeakEvent 防止内存泄漏（可选）
        public event EventHandler<VisualChangedEventArgs>? VisualsChanged;
        public event EventHandler<VisualChangedEventArgs>? VisualsAdd;
        public event EventHandler<VisualChangedEventArgs>? VisualsRemove;

        // 批量操作支持
        public void Clear()
        {
            ClearActionCommand();
            foreach (var item in visuals)
                RemoveVisualTree(item);

            visuals.Clear();
            VisualsChanged?.Invoke(this, new VisualChangedEventArgs(null, VisualChangeType.Clear));
        }

        public void OnlyAddVisual(Visual visual)
        {
            if (visual == null || visuals.Contains(visual)) return;
            visuals.Add(visual);
            AddVisualTree(visual);
        }

        public void AddVisual(Visual visual, bool recordAction = true)
        {
            if (visual == null || visuals.Contains(visual)) return;

            visuals.Add(visual);
            AddVisualTree(visual);

            VisualsAdd?.Invoke(this, new VisualChangedEventArgs(visual, VisualChangeType.Add));
            VisualsChanged?.Invoke(this, new VisualChangedEventArgs(visual, VisualChangeType.Add));

            if (recordAction)
            {
                Action undoaction = () => RemoveVisual(visual, false);
                Action redoaction = () => AddVisual(visual, false);
                AddActionCommand(new ActionCommand(undoaction, redoaction) { Header = "添加" });
            }
        }

        public void RemoveVisual(Visual? visual, bool recordAction = true)
        {
            if (visual == null || !visuals.Contains(visual)) return;

            visuals.Remove(visual);
            RemoveVisualTree(visual);

            VisualsRemove?.Invoke(this, new VisualChangedEventArgs(visual, VisualChangeType.Remove));
            VisualsChanged?.Invoke(this, new VisualChangedEventArgs(visual, VisualChangeType.Remove));

            if (recordAction)
            {
                Action undoaction = () => AddVisual(visual, false);
                Action redoaction = () => RemoveVisual(visual, false);
                AddActionCommand(new ActionCommand(undoaction, redoaction) { Header = "移除" });
            }
        }

        public void TopVisual(Visual visual)
        {
            int count = visuals.Count;
            if (count == 0) return;
            int index = visuals.IndexOf(visual);

            // 已经在最上层，无需处理
            if (index == -1 || index == count - 1) return;

            visuals.RemoveAt(index);
            visuals.Add(visual);

            RemoveVisualTree(visual);
            AddVisualTree(visual);

            VisualsChanged?.Invoke(this, new VisualChangedEventArgs(visual, VisualChangeType.Top));
        }

        // 批量置顶
        public void BatchTopVisuals(IEnumerable<Visual> topVisuals)
        {
            // 用 HashSet 提高查找性能（避免重复）
            var toMove = topVisuals?.Where(v => v != null && visuals.Contains(v)).ToList();
            if (toMove == null || toMove.Count == 0) return;

            foreach (var visual in toMove)
            {
                visuals.Remove(visual);
                visuals.Add(visual);
                RemoveVisualTree(visual);
                AddVisualTree(visual);
            }

            VisualsChanged?.Invoke(this, new VisualChangedEventArgs(null, VisualChangeType.Top));
        }

        // 集中管理视觉树
        private void AddVisualTree(Visual visual)
        {
            AddVisualChild(visual);
            AddLogicalChild(visual);
        }

        private void RemoveVisualTree(Visual visual)
        {
            RemoveVisualChild(visual);
            RemoveLogicalChild(visual);
        }

        // 支持泛型
        public TVisual? GetVisual<TVisual>(Point point) where TVisual : Visual
        {
            var hitResult = VisualTreeHelper.HitTest(this, point);
            return hitResult?.VisualHit as TVisual;
        }

        private readonly List<DrawingVisual> hits = new();
        public List<DrawingVisual> GetVisuals(Geometry region)
        {
            hits.Clear();
            GeometryHitTestParameters parameters = new(region);
            HitTestResultCallback callback = new(HitTestCallback);
            VisualTreeHelper.HitTest(this, null, callback, parameters);
            return hits;
        }

        private HitTestResultBehavior HitTestCallback(HitTestResult result)
        {
            if (result is GeometryHitTestResult geometryResult
                && geometryResult.VisualHit is DrawingVisual visual
                && geometryResult.IntersectionDetail == IntersectionDetail.FullyInside)
            {
                hits.Add(visual);
            }
            return HitTestResultBehavior.Continue;
        }

    }

}
