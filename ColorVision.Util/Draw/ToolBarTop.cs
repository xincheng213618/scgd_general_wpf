using ColorVision.MVVM;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using ColorVision.Draw;
using Gu.Wpf.Geometry;
using System.Reflection;
using ColorVision.Draw.Ruler;

namespace ColorVision.Draw
{


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

        private ToolShowImage ShowImage { get; set; }
        private ToolBarMeasure ToolBarMeasure { get; set; }

        private FrameworkElement Parent { get; set; }

        public ToolBarScaleRuler ToolBarScaleRuler { get; set; }

        public ToolReferenceLine ToolConcentricCircle { get; set; }


        public ToolBarTop(FrameworkElement Parent,ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            this.Parent = Parent;
            ZoomboxSub = zombox ?? throw new ArgumentNullException(nameof(zombox));
            Image = drawCanvas ?? throw new ArgumentNullException(nameof(drawCanvas));


            ShowImage = new ToolShowImage(zombox, drawCanvas);
            ToolBarMeasure = new ToolBarMeasure(Parent, zombox, drawCanvas);
            ToolBarScaleRuler = new ToolBarScaleRuler(Parent, zombox, drawCanvas);
            ToolConcentricCircle = new ToolReferenceLine(zombox, drawCanvas);
            ToolBarScaleRuler.IsShow = false;

            ZoomUniformToFill = new RelayCommand(a => ZoomboxSub.ZoomUniformToFill());
            ZoomUniform = new RelayCommand(a => ZoomboxSub.ZoomUniform());
            ZoomIncrease = new RelayCommand(a => ZoomboxSub.Zoom(1.25));
            ZoomDecrease = new RelayCommand(a => ZoomboxSub.Zoom(0.8));
            ZoomNone = new RelayCommand(a => ZoomboxSub.ZoomNone());
            OpenProperty = new RelayCommand(a => new DrawProperties() {Owner = Window.GetWindow(Parent),WindowStartupLocation =WindowStartupLocation.CenterOwner }.Show());
            this.Parent.PreviewKeyDown += PreviewKeyDown;
            zombox.Cursor = Cursors.Hand;
        }

        public void OpenImage()
        {
            ToolBarScaleRuler.IsShow = true;
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

        public bool ScaleRulerShow
        { 
            get => ToolBarScaleRuler.IsShow;
            set
            {
                if (ToolBarScaleRuler.IsShow == value) return;
                ToolBarScaleRuler.IsShow = value;
                NotifyPropertyChanged();
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

                ShowImage.IsShow = value;
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

                    LastChoice = string.Empty;
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
                    Activate = true;
                    LastChoice = nameof(DrawCircle);
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
                    Activate = true;
                    LastChoice = nameof(DrawRect);
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
                    Activate = true;
                    LastChoice = nameof(Measure);
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
                    Activate = true;
                    LastChoice = nameof(DrawPolygon);
                }

                NotifyPropertyChanged();
            }
        }

        private bool _ConcentricCircle;

        public bool ConcentricCircle
        {
            get => _ConcentricCircle;
            set
            {
                if (_ConcentricCircle == value) return;
                _ConcentricCircle = value;
                ToolConcentricCircle.IsShow = value;
                if (value)
                {
                    Activate = true;
                    LastChoice = nameof(ConcentricCircle);
                }
                NotifyPropertyChanged();
            }
        }





        public string LastChoice { get => _LastChoice; set 
            {
                if (value == _LastChoice)
                    return;
                if (!string.IsNullOrWhiteSpace(_LastChoice))
                {
                    Type type = this.GetType();
                    PropertyInfo property = type.GetProperty(_LastChoice);
                    property?.SetValue(this, false);
                }
                _LastChoice = value;

            }
        }
        private string _LastChoice { get; set; }

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
                if (value)
                {
                    Activate = true;
                    LastChoice = nameof(EraseVisual);
                }


                NotifyPropertyChanged();
            }
        }




    }
}
