using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class DrawCanvas : Image
    {
        private List<Visual> visuals = new List<Visual>();

        public DrawCanvas()
        {
            this.Focusable = true;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => Undo(), (s, e) => { e.CanExecute = UndoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (s, e) => Redo(), (s, e) => { e.CanExecute = RedoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(Commands.UndoHistory, null, (s, e) =>{ e.CanExecute = UndoStack.Count > 0;  if (e.Parameter is MenuItem m1 && m1.ItemsSource != UndoStack) m1.ItemsSource = UndoStack; }));
        }
        #region ActionCommand
        public ObservableCollection<ActionCommand> UndoStack { get; set; } = new ObservableCollection<ActionCommand>();
        public ObservableCollection<ActionCommand> RedoStack { get; set; } = new ObservableCollection<ActionCommand>();

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
                var undoAction = UndoStack[^1]; // Access the last element
                UndoStack.RemoveAt(UndoStack.Count - 1); // Remove the last element
                undoAction.UndoAction();
                RedoStack.Add(undoAction);
            }
        }

        public void Redo()
        {
            if (RedoStack.Count > 0)
            {
                var redoAction = RedoStack[^1]; // Access the last element
                RedoStack.RemoveAt(RedoStack.Count - 1); // Remove the last element
                redoAction.RedoAction();
                UndoStack.Add(redoAction);
            }
        }
        #endregion

        #region doubleClick
        private DateTime lastClickTime;
        private const int DoubleClickTime = 300; // Time in milliseconds

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

        protected override int VisualChildrenCount { get => visuals.Count; }

        public bool ContainsVisual(Visual visual) => visuals.Contains(visual);

        public event EventHandler? ImageInitialized;

        public void RaiseImageInitialized()
        {
            ImageInitialized?.Invoke(this, new EventArgs());
        }


        public event EventHandler? VisualsChanged;

        public event EventHandler? VisualsAdd;
        public event EventHandler? VisualsRemove;
        public event EventHandler VisualsClear;

        public void Clear()
        {
            ClearActionCommand();

            foreach (var item in visuals)
            {
                RemoveVisualChild(item);
                RemoveLogicalChild(item);
            }
            visuals.Clear();

        }
        public void OnlyAddVisual(Visual visual)
        {
            visuals.Add(visual);

            AddVisualChild(visual);
            AddLogicalChild(visual);
        }

        public void AddVisual(Visual visual, bool recordAction = true)
        {
            try
            {
                visuals.Add(visual);

                AddVisualChild(visual);
                AddLogicalChild(visual);
                VisualsAdd?.Invoke(visual, EventArgs.Empty);
                VisualsChanged?.Invoke(visual, EventArgs.Empty);

                if (recordAction)
                {
                    Action undoaction = new Action(() =>
                    {
                        RemoveVisual(visual, false);
                    });
                    Action redoaction = new Action(() =>
                    {
                        AddVisual(visual, false);
                    });
                    AddActionCommand(new ActionCommand(undoaction, redoaction) { Header = "添加" });
                }

            }
            catch
            {

            }

        }

        public void RemoveVisual(Visual? visual, bool recordAction = true)
        {
            if (visual == null) return;
            visuals.Remove(visual);

            RemoveVisualChild(visual);
            RemoveLogicalChild(visual);
            VisualsRemove?.Invoke(visual, EventArgs.Empty);
            VisualsChanged?.Invoke(visual, EventArgs.Empty);

            if (recordAction)
            {
                Action undoaction = new Action(() =>
                {
                    AddVisual(visual, false);
                });
                Action redoaction = new Action(() =>
                {
                    RemoveVisual(visual, false);
                });
                AddActionCommand(new ActionCommand(undoaction, redoaction) { Header = "移除" });
            }
        }

        public void TopVisual(Visual visual)
        {
            RemoveVisualChild(visual);
            RemoveLogicalChild(visual);

            AddVisualChild(visual);
            AddLogicalChild(visual);
            VisualsChanged?.Invoke(visual, EventArgs.Empty);

        }


        public DrawingVisual? GetVisual(Point point)
        {
            HitTestResult hitResult = VisualTreeHelper.HitTest(this, point);

            if (hitResult == null)
                return null;
            return hitResult.VisualHit as DrawingVisual;
        }



        private List<DrawingVisual> hits = new();
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
            GeometryHitTestResult geometryResult = (GeometryHitTestResult)result;
            DrawingVisual visual = result.VisualHit as DrawingVisual;
            if (visual != null &&
                geometryResult.IntersectionDetail == IntersectionDetail.FullyInside)
            {
                hits.Add(visual);
            }
            return HitTestResultBehavior.Continue;
        }



    }

}
