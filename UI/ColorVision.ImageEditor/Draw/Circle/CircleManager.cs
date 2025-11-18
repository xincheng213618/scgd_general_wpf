#pragma warning disable CS0414,CS8625
using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class CircleManagerConfig : ViewModelBase
    {
        [DisplayName("连续模式")]
        public bool IsContinuous { get => _IsContinuous; set { _IsContinuous = value; OnPropertyChanged(); } }
        private bool _IsContinuous;

        public bool IsLocked { get => _IsLocked; set { _IsLocked = value; OnPropertyChanged(); } }
        private bool _IsLocked;

        public double DefalutRadius { get => _DefalutRadius; set { _DefalutRadius = value; OnPropertyChanged(); } }
        private double _DefalutRadius = 30;
    }

    public class CircleManager: IEditorToggleToolBase, IDisposable
    {
        public CircleManagerConfig Config { get; set; } = new CircleManagerConfig();
        private Zoombox Zoombox1 => EditorContext.Zoombox;
        private DrawCanvas DrawCanvas => EditorContext.DrawCanvas;
        public ImageViewModel ImageViewModel => EditorContext.ImageViewModel;

        public EditorContext EditorContext { get; set; }

        public CircleManager(EditorContext context)
        {
            EditorContext = context;
            Order = 3;
            ToolBarLocal = ToolBarLocal.Draw;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageCircle");
        }



        private DVCircleText DrawCircleCache;

        public override bool IsChecked
        {
            get => _IsChecked; set
            {
                if (_IsChecked == value) return;
                _IsChecked = value;
                if (value)
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(this);
                    ImageViewModel.SlectStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
                    Load();
                }
                else
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(null);
                    ImageViewModel.SlectStackPanel.Children.Clear();
                    UnLoad();
                }
                OnPropertyChanged();
            }
        }

        public int CheckNo()
        {
            if (EditorContext.DrawingVisualLists.Count > 0 && EditorContext.DrawingVisualLists.Last() is DrawingVisualBase drawingVisual)
            {
                return drawingVisual.ID + 1;
            }
            else
            {
                return 1;
            }
        }

        public void Load()
        {
            DrawCanvas.MouseMove += MouseMove;
            DrawCanvas.MouseEnter += MouseEnter;
            DrawCanvas.MouseLeave += MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp += Image_PreviewMouseUp;
        }

        public void UnLoad()
        {
            DrawCanvas.MouseMove -= MouseMove;
            DrawCanvas.MouseEnter -= MouseEnter;
            DrawCanvas.MouseLeave -= MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp -= Image_PreviewMouseUp;
            DrawCircleCache = null;

            ImageViewModel.SelectEditorVisual.ClearRender();
        }

        Point MouseDownP { get; set; }
        Point MouseUpP { get; set; }

        bool IsMouseDown;

        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                return;
            }

            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;

            if (ImageViewModel.SelectEditorVisual.GetContainingRect(MouseDownP))
            {
                return;
            }
            else
            {
                ImageViewModel.SelectEditorVisual.ClearRender();
            }

            if (DrawCircleCache != null) return;

            CircleTextProperties circleTextProperties = new CircleTextProperties();
            int did = CheckNo();
            circleTextProperties.Id = did;
            circleTextProperties.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
            circleTextProperties.Center = MouseDownP;
            circleTextProperties.Radius = Config.DefalutRadius;
            circleTextProperties.Text = "Point_" + did;
            DVCircleText dVCircle = new DVCircleText(circleTextProperties);

            DrawCircleCache = dVCircle;


            DrawCanvas.AddVisualCommand(DrawCircleCache);
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.ReleaseMouseCapture();

            IsMouseDown = false;
            if (DrawCircleCache != null)
            {
                MouseUpP = e.GetPosition(DrawCanvas);

                if (DrawCircleCache.Attribute.Radius == Config.DefalutRadius)
                    DrawCircleCache.Render();

                ImageViewModel.SelectEditorVisual.SetRender(DrawCircleCache);

                if (!Config.IsLocked)
                    Config.DefalutRadius = DrawCircleCache.Radius;

                if (!Config.IsContinuous)
                    IsChecked = false;

                DrawCircleCache = null;
            }
            e.Handled = true;
        }



        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseDown)
            {
                if (DrawCircleCache != null)
                {
                    var point = e.GetPosition(DrawCanvas);

                    double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                    DrawCircleCache.Attribute.Radius = Radius;
                    DrawCircleCache.Render();
                }
            }
            e.Handled = true;
        }

        private void MouseEnter(object sender, MouseEventArgs e)
        {
        }

        private void MouseLeave(object sender, MouseEventArgs e)
        {
        }




        private bool _IsChecked;




        public void Dispose()
        {
            UnLoad();

            GC.SuppressFinalize(this);
        }
    }
}
