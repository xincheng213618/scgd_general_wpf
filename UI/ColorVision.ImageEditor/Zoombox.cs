#pragma warning disable CA1510
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    
    /// <summary>
    /// A decorator that adds zoom and pan.
    /// </summary>
    public class Zoombox : Decorator
    {

        public ModifierKeys ActivateOn
        {
            get { return (ModifierKeys)GetValue(ActivateOnProperty); }
            set { SetValue(ActivateOnProperty, value); }
        }

        public static readonly DependencyProperty ActivateOnProperty = DependencyProperty.Register(nameof(ActivateOn), typeof(ModifierKeys), typeof(Zoombox), new PropertyMetadata(ModifierKeys.None));

        /// <summary>Identifies the <see cref="WheelZoomFactor"/> dependency property.</summary>
        public static readonly DependencyProperty WheelZoomFactorProperty = DependencyProperty.Register(
            nameof(WheelZoomFactor),
            typeof(double),
            typeof(Zoombox),
            new PropertyMetadata(1.05));

        /// <summary>Identifies the <see cref="MinZoom"/> dependency property.</summary>
        public static readonly DependencyProperty MinZoomProperty = DependencyProperty.Register(
            nameof(MinZoom),
            typeof(double),
            typeof(Zoombox),
            new PropertyMetadata(double.NegativeInfinity));

        /// <summary>Identifies the <see cref="MaxZoom"/> dependency property.</summary>
        public static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register(
            nameof(MaxZoom),
            typeof(double),
            typeof(Zoombox),
            new PropertyMetadata(double.PositiveInfinity));

        /// <summary>Identifies the <see cref="ContentMatrix"/> dependency property.</summary>
        public static readonly DependencyProperty ContentMatrixProperty = DependencyProperty.Register(
            nameof(ContentMatrix),
            typeof(Matrix),
            typeof(Zoombox),
            new PropertyMetadata(
                default(Matrix),
                (d, e) =>
                {
                    var zb = (Zoombox)d;
                    zb.CancelPendingZoom();
                    ((MatrixTransform)zb.InternalVisual.Transform).SetCurrentValue(MatrixTransform.MatrixProperty, (Matrix)e.NewValue);
                    var previous = (Matrix)e.OldValue;
                    var current = (Matrix)e.NewValue;
                    if (previous.M11 != current.M11 || previous.M22 != current.M22)
                        CommandManager.InvalidateRequerySuggested();
                    zb.ContentMatrixChanged?.Invoke(zb, EventArgs.Empty);
                }));

        private const double MinScaleDelta = 1E-6;

        private ContainerVisual? internalVisual;
        private Point position;
        private Action? pendingZoom;

        public Zoombox()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (pendingZoom == null) return;
            LayoutUpdated += OnPendingZoomLayoutUpdated;
            OnPendingZoomLayoutUpdated(this, EventArgs.Empty);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => CancelPendingZoom();

        internal void CancelPendingZoom()
        {
            pendingZoom = null;
            LayoutUpdated -= OnPendingZoomLayoutUpdated;
        }

        private bool IsZoomLayoutReady => IsArrangeValid && InternalChild?.IsArrangeValid == true
            && ActualWidth > MinScaleDelta && ActualHeight > MinScaleDelta;

        private bool WaitForZoomLayout(Action zoom)
        {
            CancelPendingZoom();
            UIElement? child = InternalChild;
            if (child == null) return true;
            if (IsZoomLayoutReady) return false;

            // A detached child may never be arranged again. Wait for real layout
            // instead of keeping its entire window alive through Dispatcher retries.
            pendingZoom = zoom;
            if (IsLoaded) LayoutUpdated += OnPendingZoomLayoutUpdated;
            return true;
        }

        private void OnPendingZoomLayoutUpdated(object? sender, EventArgs e)
        {
            if (!IsLoaded || !IsZoomLayoutReady) return;
            Action? zoom = pendingZoom;
            CancelPendingZoom();
            zoom?.Invoke();
        }

        static Zoombox()
        {
            ClipToBoundsProperty.OverrideMetadata(
                typeof(Zoombox),
                new PropertyMetadata(
                    true,
                    ClipToBoundsProperty.GetMetadata(typeof(Decorator)).PropertyChangedCallback));
            CommandManager.RegisterClassCommandBinding(
                typeof(Zoombox),
                new CommandBinding(
                    NavigationCommands.IncreaseZoom,
                    OnIncreaseZoom,
                    OnCanIncreaseZoom));

            CommandManager.RegisterClassCommandBinding(
                typeof(Zoombox),
                new CommandBinding(
                    NavigationCommands.DecreaseZoom,
                    OnDecreaseZoom,
                    OnCanDecreaseZoom));

        }

        /// <summary>
        /// Gets or sets the increment zoom is changed on each mouse wheel.
        /// </summary>
        public double WheelZoomFactor
        {
            get => (double)this.GetValue(WheelZoomFactorProperty);
            set => this.SetValue(WheelZoomFactorProperty, value);
        }

        /// <summary>
        /// Gets or sets the minimum zoom allowed.
        /// </summary>
        public double MinZoom
        {
            get => (double)this.GetValue(MinZoomProperty);
            set => this.SetValue(MinZoomProperty, value);
        }

        /// <summary>
        /// Gets or sets the maximum zoom allowed.
        /// </summary>
        public double MaxZoom
        {
            get => (double)this.GetValue(MaxZoomProperty);
            set => this.SetValue(MaxZoomProperty, value);
        }

        public event EventHandler ContentMatrixChanged;

        /// <summary>
        /// Gets or sets the transform applied to the contents.
        /// </summary>
        public Matrix ContentMatrix
        {
            get => (Matrix)this.GetValue(ContentMatrixProperty);
            set
            {
                 CancelPendingZoom();
                 this.SetValue(ContentMatrixProperty, value);
            }
        }

        /// <inheritdoc />
        public override UIElement? Child
        {
            // everything is the same as on Decorator, the only difference is to insert intermediate Visual to
            // specify scaling transform
            get => this.InternalChild;

            set
            {
                var old = this.InternalChild;

                if (!ReferenceEquals(old, value))
                {
                    CancelPendingZoom();
                    // need to remove old element from logical tree
                    this.RemoveLogicalChild(old);

                    if (value != null)
                    {
                        this.AddLogicalChild(value);
                    }

                    this.InternalChild = value;

                    this.InvalidateMeasure();
                }
            }
        }

        /// <inheritdoc />
        protected override int VisualChildrenCount => 1;


        private ContainerVisual InternalVisual
        {
            get
            {
                if (this.internalVisual is null)
                {
                    this.internalVisual = new ContainerVisual
                    {
                        Transform = new MatrixTransform(Matrix.Identity),
                    };
                    this.AddVisualChild(this.internalVisual);
                }

                return this.internalVisual;
            }
        }

        private Vector CurrentZoom => new(this.ContentMatrix.M11, this.ContentMatrix.M22);

        private UIElement? InternalChild
        {
            get
            {
                var vc = this.InternalVisual.Children;
                if (vc.Count != 0)
                {
                    return vc[0] as UIElement;
                }
                else
                {
                    return null;
                }
            }

            set
            {
                var vc = this.InternalVisual.Children;
                if (vc.Count != 0)
                {
                    vc.Clear();
                }

                vc.Add(value);
            }
        }

        /// <summary>
        /// Zoom around a the center of the currently visible part.
        /// </summary>
        /// <param name="scale">The amount to resize as a multiplier.</param>
        public void Zoom(double scale)
        {
            this.Zoom(new Vector(scale, scale));
        }

        /// <summary>
        /// Zoom around a the center of the currently visible part.
        /// </summary>
        /// <param name="scale">The amount to resize as a multipliers.</param>
        public void Zoom(Vector scale)
        {
            Rect? bounds = LayoutInformation.GetLayoutClip(this)?.Bounds;
            this.Zoom(bounds is Rect rect ? new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2) : new Point(0, 0), scale);
        }

        /// <summary>
        /// Zoom around a point.
        /// </summary>
        /// <param name="center">The point to zoom about.</param>
        /// <param name="scale">The amount to resize as a multipliers.</param>
        public void Zoom(Point center, Vector scale)
        {
            scale = this.CoerceScale(scale);
            Matrix matrix = ContentMatrix;
            matrix.ScaleAt(scale.X, scale.Y, center.X, center.Y);
            ApplyMatrix(matrix);
        }

        /// <summary>Move the content by a distance in viewport coordinates.</summary>
        public void Pan(Vector translation)
        {
            Matrix matrix = ContentMatrix;
            matrix.Translate(translation.X, translation.Y);
            ApplyMatrix(matrix);
        }

        private void ApplyMatrix(Matrix matrix)
        {
            // Explicit navigation supersedes a deferred fit even when the matrix is unchanged.
            CancelPendingZoom();
            if (ContentMatrix == matrix) return;
            SetCurrentValue(ContentMatrixProperty, matrix);
        }

        /// <summary>
        /// The content is re-sized to fit in the destination dimensions while it preserves its native aspect ratio.
        /// </summary>
        public void ZoomUniform()
        {
            if (WaitForZoomLayout(ZoomUniform)) return;

            var size = this.InternalChild!.DesiredSize;
            if (Math.Abs(size.Width) < MinScaleDelta ||
                Math.Abs(size.Height) < MinScaleDelta)
            {
                return;
            }

            var scaleX = this.ActualWidth / size.Width;
            var scaleY = this.ActualHeight / size.Height;
            var scale = Math.Min(scaleX, scaleY);
            ApplyMatrix(new Matrix(scale, 0, 0, scale,
                (ActualWidth - scale * size.Width) / 2, (ActualHeight - scale * size.Height) / 2));
        }

        /// <summary>
        /// The content is re-sized to fill the destination dimensions while it preserves its native aspect ratio.
        /// If the aspect ratio of the destination rectangle differs from the source, the source content is clipped to fit in the destination dimensions.
        /// </summary>
        public void ZoomUniformToFill()
        {
            if (WaitForZoomLayout(ZoomUniformToFill)) return;

            var size = this.InternalChild!.DesiredSize;
            if (Math.Abs(size.Width) < MinScaleDelta ||
                Math.Abs(size.Height) < MinScaleDelta)
            {
                return;
            }

            var scaleX = this.ActualWidth / size.Width;
            var scaleY = this.ActualHeight / size.Height;
            var scale = Math.Max(scaleX, scaleY);
            ApplyMatrix(new Matrix(scale, 0, 0, scale,
                (ActualWidth - scale * size.Width) / 2, (ActualHeight - scale * size.Height) / 2));
        }

        public void ZoomToContentRect(Rect contentRect)
        {
            if (contentRect.IsEmpty) return;
            if (WaitForZoomLayout(() => ZoomToContentRect(contentRect))) return;

            var size = this.InternalChild!.DesiredSize;
            if (Math.Abs(size.Width) < MinScaleDelta ||
                Math.Abs(size.Height) < MinScaleDelta ||
                Math.Abs(this.ActualWidth) < MinScaleDelta ||
                Math.Abs(this.ActualHeight) < MinScaleDelta)
            {
                return;
            }

            double x = Math.Max(0, contentRect.X);
            double y = Math.Max(0, contentRect.Y);
            double right = Math.Min(size.Width, contentRect.Right);
            double bottom = Math.Min(size.Height, contentRect.Bottom);
            double width = right - x;
            double height = bottom - y;
            if (width < MinScaleDelta || height < MinScaleDelta)
            {
                return;
            }

            var scaleX = this.ActualWidth / width;
            var scaleY = this.ActualHeight / height;
            var scale = Math.Min(scaleX, scaleY);
            ApplyMatrix(new Matrix(scale, 0, 0, scale,
                (ActualWidth - scale * width) / 2 - scale * x,
                (ActualHeight - scale * height) / 2 - scale * y));
        }

        /// <summary>
        /// The content preserves its original size.
        /// </summary>
        public void ZoomNone()
        {
            ApplyMatrix(Matrix.Identity);
        }

        /// <summary>Restore a saved viewport and refresh tools and drawing-scale dependents.</summary>
        public void RestoreView(Matrix matrix)
        {
            ApplyMatrix(matrix);
        }

        /// <inheritdoc />
        protected override Visual GetVisualChild(int index)
        {
            if (index != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Always exactly one child");
            }

            return this.InternalVisual;
        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size constraint)
        {
            this.InternalChild?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return double.IsPositiveInfinity(constraint.Width) || double.IsPositiveInfinity(constraint.Height)
                ? this.InternalChild?.DesiredSize ?? default
                : constraint;
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var child = this.InternalChild;
            child?.Arrange(new Rect(child.DesiredSize));
            return arrangeSize;
        }

        /// <inheritdoc />
        protected override void OnRender(DrawingContext drawingContext)
        {
            if (drawingContext is null)
            {
                throw new ArgumentNullException(nameof(drawingContext));
            }

            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(this.RenderSize));
        }

        /// <inheritdoc />
        protected override void OnManipulationDelta(ManipulationDeltaEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            var delta = e.DeltaManipulation;
            Matrix matrix = ContentMatrix;
            if (Math.Abs(delta.Scale.LengthSquared - 2) > MinScaleDelta)
            {
                var p = ((FrameworkElement)e.ManipulationContainer).TranslatePoint(e.ManipulationOrigin, this);
                Vector scale = CoerceScale(delta.Scale);
                matrix.ScaleAt(scale.X, scale.Y, p.X, p.Y);
            }

            if (delta.Translation.LengthSquared > 0)
            {
                matrix.Translate(delta.Translation.X, delta.Translation.Y);
            }

            ApplyMatrix(matrix);
            base.OnManipulationDelta(e);
        }

        /// <inheritdoc />
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            if (ActivateOn == ModifierKeys.None || Keyboard.Modifiers.HasFlag(ActivateOn))
            {

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (e.Delta == 0 || this.WheelZoomFactor == 1)
                {
                    return;
                }

                var scale = e.Delta > 0
                    ? this.WheelZoomFactor
                    : 1.0 / this.WheelZoomFactor;
                var p = e.GetPosition(this);
                this.Zoom(p, new Vector(scale, scale));
            }

            base.OnMouseWheel(e);
        }

        /// <inheritdoc />
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            if (ActivateOn == ModifierKeys.None || Keyboard.Modifiers.HasFlag(ActivateOn))
            {

                this.position = e.GetPosition(this);
                if (CaptureMouse()) CancelPendingZoom();
            }


            base.OnMouseLeftButtonDown(e);
        }

        /// <inheritdoc />
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            this.ReleaseMouseCapture();
            base.OnMouseLeftButtonUp(e);
        }

        /// <inheritdoc />
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (this.IsMouseCaptured)
            {
                var newPos = e.GetPosition(this);
                var delta = newPos - this.position;
                if (delta != default) Pan(delta);
                this.position = newPos;
            }

            base.OnMouseMove(e);
        }

        private static void OnCanDecreaseZoom(object sender, CanExecuteRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            var scale = GetScale(e.Parameter);
            scale = scale.LengthSquared > 2
                ? new Vector(1 / scale.X, 1 / scale.Y)
                : scale;
            e.CanExecute = Math.Abs(box.CoerceScale(scale).LengthSquared - 2) > MinScaleDelta;
            e.Handled = true;
        }

        private static void OnDecreaseZoom(object sender, ExecutedRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            var scale = GetScale(e.Parameter);
            scale = scale.LengthSquared > 2
                ? new Vector(1 / scale.X, 1 / scale.Y)
                : scale;
            box.Zoom(scale);
            e.Handled = true;
        }

        private static void OnCanIncreaseZoom(object sender, CanExecuteRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            var scale = GetScale(e.Parameter);
            e.CanExecute = Math.Abs(box.CoerceScale(scale).LengthSquared - 2) > MinScaleDelta;
            e.Handled = true;
        }

        private static void OnIncreaseZoom(object sender, ExecutedRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            var scale = GetScale(e.Parameter);
            box.Zoom(scale);
            e.Handled = true;
        }

        private static double Clamp(double min, double value, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static Vector GetScale(object parameter)
        {
            return parameter switch
            {
                int i => new Vector(i, i),
                double d => new Vector(d, d),
                Vector v => v,
                _ => new Vector(2.0, 2.0),
            };
        }

        private Vector CoerceScale(Vector scale)
        {
            var zoom = this.CurrentZoom;
            return new Vector(
                Clamp(this.MinZoom / zoom.X, scale.X, this.MaxZoom / zoom.X),
                Clamp(this.MinZoom / zoom.Y, scale.Y, this.MaxZoom / zoom.Y));
        }
    }

}
