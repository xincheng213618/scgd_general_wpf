using ColorVision.MVVM;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using ColorVision.Draw;
using Gu.Wpf.Geometry;

namespace ColorVision
{


    public class ToolBarMeasure
    {
        private ZoomboxSub Zoombox1 { get; set; }
        private DrawCanvas drawCanvas { get; set; }

        private FrameworkElement Parent { get; set; }

        public ToolBarMeasure(FrameworkElement Parent, ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            this.Parent = Parent;
            Zoombox1 = zombox;
            this.drawCanvas = drawCanvas;
        }
        private DrawingVisualRuler? DrawingVisualRulerCache;


        public bool Measure
        {
            get => _Measure;
            set
            {
                if (_Measure == value) return;
                _Measure = value;
                if (value)
                {
                    Parent.PreviewKeyDown += PreviewKeyDown;
                    drawCanvas.PreviewMouseLeftButtonDown += MouseDown;
                    drawCanvas.MouseMove += MouseMove;
                    drawCanvas.PreviewMouseLeftButtonUp += MouseUp;
                    drawCanvas.PreviewMouseRightButtonDown += PreviewMouseRightButtonDown;
                }
                else
                {
                    Parent.PreviewKeyDown -= PreviewKeyDown;
                    drawCanvas.PreviewMouseLeftButtonDown -= MouseDown;
                    drawCanvas.MouseMove -= MouseMove;
                    drawCanvas.PreviewMouseLeftButtonUp -= MouseUp;
                    drawCanvas.PreviewMouseRightButtonDown -= PreviewMouseRightButtonDown;

                }

            }
        }


        private bool _Measure;


        private bool IsMouseDown;
        private Point MouseDownP;


        private void MouseDown(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = true;
            if (DrawingVisualRulerCache == null)
            {
                DrawingVisualRulerCache = new DrawingVisualRuler();
                DrawingVisualRulerCache.Pen.Thickness = 1 / Zoombox1.ContentMatrix.M11;
                drawCanvas.AddVisual(DrawingVisualRulerCache);
            }
        }
        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && (Zoombox1.ActivateOn == ModifierKeys.None || !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);

                if (DrawingVisualRulerCache != null)
                {
                    DrawingVisualRulerCache.MovePoints = point;
                    DrawingVisualRulerCache.Render();
                }
            }
        }
        private void MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                IsMouseDown = false;
                var MouseUpP = e.GetPosition(drawCanvas);
                if (DrawingVisualRulerCache != null)
                {
                    DrawingVisualRulerCache.Points.Add(MouseUpP);
                    DrawingVisualRulerCache.MovePoints = null;
                    DrawingVisualRulerCache.Render();
                }
            }
        }
        private void PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DrawingVisualRulerCache != null)
            {
                DrawingVisualRulerCache.MovePoints = null;
                DrawingVisualRulerCache.Render();
                DrawingVisualRulerCache = null;
            }
        }

        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DrawingVisualRulerCache != null)
                {
                    drawCanvas.RemoveVisual(DrawingVisualRulerCache);
                    DrawingVisualRulerCache = null;
                }
            }
            if (e.Key == Key.Enter)
            {
                if (DrawingVisualRulerCache != null)
                {
                    DrawingVisualRulerCache.MovePoints = null;
                    DrawingVisualRulerCache.Render();
                    DrawingVisualRulerCache = null;
                }
            }
        }




    }


    public class ToolBarTop : ViewModelBase
    {
        public RelayCommand ZoomUniformToFill { get; set; }
        public RelayCommand ZoomUniform { get; set; }
        public RelayCommand ZoomIncrease { get; set; }
        public RelayCommand ZoomDecrease { get; set; }
        public RelayCommand ZoomNone { get; set; }

        public RelayCommand OpenProperty { get; set; }

        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas Image { get; set; }

        private ToolBarTopShowImage ShowImage { get; set; }
        private ToolBarMeasure ToolBarMeasure { get; set; }

        private FrameworkElement Parent { get; set; }


        public ToolBarTop(FrameworkElement Parent,ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            this.Parent = Parent;
            ZoomboxSub = zombox ?? throw new ArgumentNullException(nameof(zombox));
            Image = drawCanvas ?? throw new ArgumentNullException(nameof(drawCanvas));


            ShowImage = new ToolBarTopShowImage(zombox, drawCanvas);
            ToolBarMeasure = new ToolBarMeasure(Parent, zombox, drawCanvas);

            ZoomUniformToFill = new RelayCommand(a => ZoomboxSub.ZoomUniformToFill());
            ZoomUniform = new RelayCommand(a => ZoomboxSub.ZoomUniform());
            ZoomIncrease = new RelayCommand(a => ZoomboxSub.Zoom(1.25));
            ZoomDecrease = new RelayCommand(a => ZoomboxSub.Zoom(0.8));
            ZoomNone = new RelayCommand(a => ZoomboxSub.ZoomNone());
            OpenProperty = new RelayCommand(a => new DrawProperties().Show());
            this.Parent.PreviewKeyDown += PreviewKeyDown;
            zombox.Cursor = Cursors.Hand;
        }

        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                TranslateTransform translateTransform = new TranslateTransform();
                Vector vector = new Vector(-10, 0);
                translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
            }
            else if (e.Key == Key.Right)
            {
                TranslateTransform translateTransform = new TranslateTransform();
                Vector vector = new Vector(10, 0);
                translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
            }
            else if (e.Key == Key.Up)
            {
                TranslateTransform translateTransform = new TranslateTransform();
                Vector vector = new Vector(0, -10);
                translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
            }
            else if (e.Key == Key.Down)
            {
                TranslateTransform translateTransform = new TranslateTransform();
                Vector vector = new Vector(0, 10);
                translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
            }
            else if (e.Key == Key.Add)
            {
                ZoomboxSub.Zoom(1.1);
            }
            else if (e.Key == Key.Subtract)
            {
                ZoomboxSub.Zoom(0.9);
            }
        }


        /// <summary>
        /// 当前的缩放分辨率
        /// </summary>
        public double ZoomRatio
        {
            get => ZoomboxSub.ContentMatrix.M11;
            set => ZoomboxSub.Zoom(value);
        }

        private bool _ShowImageInfo;
        public bool ShowImageInfo
        {
            get => _ShowImageInfo; set
            {
                if (_ShowImageInfo == value) return;
                if (value) Activate = false;
                _ShowImageInfo = value;

                ShowImage.ShowImageInfo = value;
                NotifyPropertyChanged();
            }
        }


        private bool _Activate;

        public bool Activate
        {
            get => _Activate;
            set
            {
                if (_Activate == value) return;
                if (value) ShowImageInfo = false;
                _Activate = value;
                if (_Activate)
                {
                    ZoomboxSub.ActivateOn = ModifierKeys.Control;
                    ZoomboxSub.Cursor = Cursors.Cross;
                }
                else
                {
                    ZoomboxSub.ActivateOn = ModifierKeys.None;
                    ZoomboxSub.Cursor = Cursors.Hand;
                }
                NotifyPropertyChanged();
            }
        }






        private bool _DrawCircle;
        /// <summary>
        /// 是否画圆形
        /// </summary>
        public bool DrawCircle {  get => _DrawCircle;
            set
            {
                if (_DrawCircle == value) return;
                _DrawCircle = value;
                if (value)
                {
                    DrawRect = false;
                    DrawPolygon = false;
                    Activate = true;
                }
                NotifyPropertyChanged(); 
            }
        }

        private bool _DrawRect;
        /// <summary>
        /// 是否画圆形
        /// </summary>
        public bool DrawRect
        {
            get => _DrawRect;
            set
            {
                if (_DrawRect == value) return;
                _DrawRect = value;
                if (value)
                {
                    DrawCircle = false;
                    DrawPolygon = false;
                    Activate = true;
                }
                NotifyPropertyChanged();
            }
        }

        public bool Measure {
            get => _Measure;
            set 
                {
                if (_Measure == value) return;
                _Measure = value;
                if (value)
                {
                    DrawCircle = false;
                    DrawRect = false;
                    DrawPolygon = false;
                    Activate = true;
                }
                ToolBarMeasure.Measure = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Measure;



        private bool _DrawPolygon;

        public bool DrawPolygon
        {
            get => _DrawPolygon;
            set
            {
                if (_DrawPolygon == value) return;
                _DrawPolygon = value;
                if (value)
                {
                    DrawCircle = false;
                    DrawRect = false;
                    Activate = true;
                }
                NotifyPropertyChanged();
            }
        }

        private bool _EraseVisual;
        public bool EraseVisual {  get => _EraseVisual;
            set
            {
                if (_EraseVisual == value) return;
                    _EraseVisual = value;
                if (value)
                {
                    ZoomboxSub.Cursor = Input.Cursors.Eraser;
                }
                else
                {
                    ZoomboxSub.Cursor = Cursors.Arrow;
                }

                NotifyPropertyChanged();
            }
        }




    }
}
