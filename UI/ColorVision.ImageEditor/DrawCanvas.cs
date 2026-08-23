#pragma warning disable CS8604
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

namespace ColorVision.ImageEditor
{   
    
    public enum VisualChangeType { Add, Remove, Top, Clear, AddRange }
    public class VisualChangedEventArgs : EventArgs
    {
        public Visual? Visual { get; }
        public IReadOnlyList<Visual> Visuals { get; }
        public VisualChangeType ChangeType { get; }
        public VisualChangedEventArgs(Visual? visual, VisualChangeType changeType)
        {
            Visual = visual;
            Visuals = visual == null ? Array.Empty<Visual>() : new[] { visual };
            ChangeType = changeType;
        }

        private VisualChangedEventArgs(IReadOnlyList<Visual> visuals, VisualChangeType changeType)
        {
            Visual = null;
            Visuals = visuals;
            ChangeType = changeType;
        }

        internal static VisualChangedEventArgs CreateRange(IReadOnlyList<Visual> visuals)
        {
            return new VisualChangedEventArgs(visuals, VisualChangeType.AddRange);
        }
    }

    public class DrawCanvas : Image,IDisposable
    {
        // 使用只读集合，防止外部直接修改
        private readonly List<Visual> visuals = new();
        private readonly HashSet<Visual> visualSet = new();
        private readonly List<Visual> overlayVisuals = new();

        public IReadOnlyList<Visual> Visuals => visuals;

        public DrawCanvas()
        {
            this.Focusable = true;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            PreviewMouseDown += (s, e) => Focus();
            PreviewKeyDown += (s, e) => Focus();
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => Undo(), (s, e) => { e.CanExecute = UndoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (s, e) => Redo(), (s, e) => { e.CanExecute = RedoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(Commands.UndoHistory, null, (s, e) =>{ e.CanExecute = UndoStack.Count > 0;  if (e.Parameter is MenuItem m1 && m1.ItemsSource != UndoStack) m1.ItemsSource = UndoStack; }));
        }
        #region ActionCommand
        public ObservableCollection<ActionCommand> UndoStack { get; } = new();
        public ObservableCollection<ActionCommand> RedoStack { get; } = new();
        private ActionCommand? _executingActionCommand;
        private bool _discardExecutingActionCommand;
        private bool _isExecutingHistoryAction;
        private readonly HashSet<Visual> _visualRemovalCommandTargets = new();
        private readonly HashSet<Visual> _visualCommandAdditionsInProgress = new();

        public void ClearActionCommand()
        {
            if (_executingActionCommand != null)
                _discardExecutingActionCommand = true;
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
            if (_isExecutingHistoryAction || UndoStack.Count == 0)
                return;

            ActionCommand undoAction = UndoStack[^1];
            _isExecutingHistoryAction = true;
            _executingActionCommand = undoAction;
            _discardExecutingActionCommand = false;
            try
            {
                UndoStack.RemoveAt(UndoStack.Count - 1);
                undoAction.UndoAction();

                if (!_discardExecutingActionCommand)
                    RedoStack.Add(undoAction);

                if (_discardExecutingActionCommand)
                {
                    UndoStack.Remove(undoAction);
                    RedoStack.Remove(undoAction);
                }
            }
            finally
            {
                _executingActionCommand = null;
                _discardExecutingActionCommand = false;
                _isExecutingHistoryAction = false;
            }
        }

        public void Redo()
        {
            if (_isExecutingHistoryAction || RedoStack.Count == 0)
                return;

            ActionCommand redoAction = RedoStack[^1];
            _isExecutingHistoryAction = true;
            _executingActionCommand = redoAction;
            _discardExecutingActionCommand = false;
            try
            {
                RedoStack.RemoveAt(RedoStack.Count - 1);
                redoAction.RedoAction();

                if (!_discardExecutingActionCommand)
                    UndoStack.Add(redoAction);

                if (_discardExecutingActionCommand)
                {
                    UndoStack.Remove(redoAction);
                    RedoStack.Remove(redoAction);
                }
            }
            finally
            {
                _executingActionCommand = null;
                _discardExecutingActionCommand = false;
                _isExecutingHistoryAction = false;
            }
        }

        internal void DiscardActionCommand(ActionCommand actionCommand)
        {
            ArgumentNullException.ThrowIfNull(actionCommand);
            if (ReferenceEquals(_executingActionCommand, actionCommand))
            {
                _discardExecutingActionCommand = true;
                return;
            }

            UndoStack.Remove(actionCommand);
            RedoStack.Remove(actionCommand);
        }

        internal bool IsVisualRemovalCommandInProgress(Visual visual)
        {
            return _visualRemovalCommandTargets.Contains(visual);
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

        public bool ContainsVisual(Visual visual) => visualSet.Contains(visual);


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
            foreach (Visual item in visuals.ToList())
                TryRemoveVisual(item, raiseEvents: !overlayVisuals.Contains(item));

            overlayVisuals.Clear();
            VisualsChanged?.Invoke(this, new VisualChangedEventArgs((Visual?)null, VisualChangeType.Clear));
        }

        public bool IsLayoutUpdated { get; set; } = true;

        public double Scale { get; set; } = 1;

        public double Sacle { get => Scale; set => Scale = value; }

        public double TextFontSizeOverride { get; set; }

        public void ApplyLayoutScaleToVisuals()
        {
            DrawingVisualScaleContext context = CreateScaleContext();
            foreach (var visual in visuals)
                ApplyLayoutScale(visual, context);
        }

        private DrawingVisualScaleContext CreateScaleContext()
        {
            return new DrawingVisualScaleContext(IsLayoutUpdated, Scale, TextFontSizeOverride);
        }

        private static void ApplyLayoutScale(Visual visual, DrawingVisualScaleContext context)
        {
            if (visual is ILayoutScaleDrawingVisual scalableVisual)
                scalableVisual.ApplyLayoutScale(context);
        }

        private bool TryAddVisual(Visual? visual, int? index = null, bool raiseEvents = true)
        {
            if (visual == null || !visualSet.Add(visual)) return false;

            ApplyLayoutScale(visual, CreateScaleContext());

            if (index.HasValue)
            {
                int targetIndex = index.Value;
                if (targetIndex < 0) targetIndex = 0;
                if (targetIndex > visuals.Count) targetIndex = visuals.Count;
                visuals.Insert(targetIndex, visual);
            }
            else
            {
                visuals.Add(visual);
            }

            AddVisualTree(visual);

            if (raiseEvents)
                RaiseVisualAdded(visual);

            return true;
        }

        private bool TryRemoveVisual(Visual? visual, bool raiseEvents = true)
        {
            if (visual == null || !visualSet.Remove(visual)) return false;

            visuals.Remove(visual);
            RemoveVisualTree(visual);

            if (raiseEvents)
                RaiseVisualRemoved(visual);

            return true;
        }

        private void RaiseVisualAdded(Visual visual)
        {
            VisualChangedEventArgs args = new(visual, VisualChangeType.Add);
            VisualsAdd?.Invoke(this, args);
            VisualsChanged?.Invoke(this, args);
        }

        private void RaiseVisualRemoved(Visual visual)
        {
            VisualChangedEventArgs args = new(visual, VisualChangeType.Remove);
            VisualsRemove?.Invoke(this, args);
            VisualsChanged?.Invoke(this, args);
        }

        public void AddVisual(Visual visual)
        {
            TryAddVisual(visual);
        }

        public int AddVisuals(IEnumerable<Visual> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            List<Visual> addedVisuals = new();
            DrawingVisualScaleContext context = CreateScaleContext();
            foreach (Visual visual in items)
            {
                if (visual == null || !visualSet.Add(visual))
                {
                    continue;
                }

                ApplyLayoutScale(visual, context);
                visuals.Add(visual);
                AddVisualTree(visual);
                addedVisuals.Add(visual);
            }

            if (addedVisuals.Count > 0)
            {
                VisualChangedEventArgs args = VisualChangedEventArgs.CreateRange(addedVisuals);
                VisualsAdd?.Invoke(this, args);
                VisualsChanged?.Invoke(this, args);
            }

            return addedVisuals.Count;
        }
        public void RemoveVisual(Visual visual)
        {
            TryRemoveVisual(visual);
        }

        /// <summary>
        /// 在指定位置插入 Visual（用于 Undo/Redo 恢复时保持顺序）
        /// </summary>
        public void InsertVisual(int index, Visual visual)
        {
            TryAddVisual(visual, index);
        }


        public void AddVisualCommand(Visual visual)
        {
            if (!TryAddVisual(visual)) return;
            AddVisualActionCommand(visual);
        }

        internal ActionCommand? AddVisualCommandCore(Visual visual)
        {
            bool ownsAdditionMarker = _visualCommandAdditionsInProgress.Add(visual);
            bool added;
            try
            {
                added = TryAddVisual(visual);
            }
            finally
            {
                if (ownsAdditionMarker)
                    _visualCommandAdditionsInProgress.Remove(visual);
            }

            if (!added || !ContainsVisual(visual)) return null;
            return AddVisualActionCommand(visual);
        }

        private ActionCommand AddVisualActionCommand(Visual visual)
        {
            Action undoaction = () => RemoveVisual(visual);
            Action redoaction = () => AddVisual(visual);
            ActionCommand command = new(undoaction, redoaction) { Header = "添加" };
            AddActionCommand(command);
            return command;
        }

        public void RemoveVisualCommand(Visual? visual)
        {
            int index = visuals.IndexOf(visual);
            if (visual == null) return;

            bool ownsRemovalMarker = _visualRemovalCommandTargets.Add(visual);
            bool removed;
            try
            {
                removed = TryRemoveVisual(visual);
            }
            finally
            {
                if (ownsRemovalMarker)
                    _visualRemovalCommandTargets.Remove(visual);
            }
            if (!removed) return;
            if (_visualCommandAdditionsInProgress.Contains(visual)) return;

            Action undoaction = () => InsertVisual(index, visual);
            Action redoaction = () => RemoveVisual(visual);
            AddActionCommand(new ActionCommand(undoaction, redoaction) { Header = "移除" });
        }

        public void AddOverlayVisual(Visual visual)
        {
            if (visual == null) return;
            if (!TryAddVisual(visual, raiseEvents: false)) return;
            if (!overlayVisuals.Contains(visual))
            {
                overlayVisuals.Add(visual);
            }
        }

        public void RemoveOverlayVisual(Visual? visual)
        {
            if (visual == null) return;
            overlayVisuals.Remove(visual);
            TryRemoveVisual(visual, raiseEvents: false);
        }

        public void ClearOverlayVisuals()
        {
            foreach (Visual visual in overlayVisuals.ToList())
            {
                TryRemoveVisual(visual, raiseEvents: false);
            }
            overlayVisuals.Clear();
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
            // Materialize once because callers may provide a lazy view over Visuals.
            // Distinct also avoids rebuilding the visual tree repeatedly for duplicate input.
            var toMove = topVisuals?.Where(visualSet.Contains).Distinct().ToList();
            if (toMove == null || toMove.Count == 0) return;

            foreach (var visual in toMove)
            {
                visuals.Remove(visual);
                visuals.Add(visual);
                RemoveVisualTree(visual);
                AddVisualTree(visual);
            }

            VisualsChanged?.Invoke(this, new VisualChangedEventArgs((Visual?)null, VisualChangeType.Top));
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

        public void Dispose()
        {
            Clear();
            MouseLeftButtonDown -= OnMouseLeftButtonDown;
            this.CommandBindings.Clear();
            GC.SuppressFinalize(this);
        }
    }

}
