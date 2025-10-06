using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{


    public class LineManager : IEditorToggleToolBase, IDisposable
    {
        private Zoombox ZoomboxSub => EditorContext.Zoombox;
        private DrawCanvas DrawCanvas => EditorContext.DrawCanvas;
        public ImageViewModel ImageViewModel => EditorContext.ImageViewModel;
        public EditorContext EditorContext { get; set; }

        public LineManager(EditorContext context)
        {
            EditorContext = context;
            ToolBarLocal = ToolBarLocal.Draw;
            Icon =  new TextBlock() { Text = "L"};
        }

        public DVLine? DVLineCache { get; set; }


        private bool _IsChecked;
        public override bool IsChecked
        {
            get => _IsChecked; set
            {
                if (_IsChecked == value) return;
                _IsChecked = value;
                if (value)
                {
                    ImageViewModel.DrawEditorManager.SetCurrentDrawEditor(this);
                    Load();
                }
                else
                {
                    ImageViewModel.DrawEditorManager.SetCurrentDrawEditor(null);
                    UnLoad();
                }
                OnPropertyChanged();
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
            DVLineCache = null;

        }

        Point MouseDownP { get; set; }
        Point MouseUpP { get; set; }

        bool IsMouseDown;


        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;
            DrawCanvas.Focus();

            if (DVLineCache == null)
            {
                DVLineCache = new DVLine();
                DVLineCache.Points.Add(MouseDownP);
                DVLineCache.Points.Add(MouseDownP);

                DVLineCache.Attribute.Pen = new Pen(Brushes.Red, 1 / ZoomboxSub.ContentMatrix.M11);
                DVLineCache.Render();
                DrawCanvas.AddVisualCommand(DVLineCache);
            }
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = false;
            if (DVLineCache != null)
            {
                MouseUpP = e.GetPosition(DrawCanvas);
                DVLineCache.Points.RemoveAt(DVLineCache.Points.Count - 1);
                DVLineCache.Points.Add(MouseUpP);
                DVLineCache.Render();
                ImageViewModel.SelectEditorVisual.SetRender(DVLineCache);
                IsChecked = false;
                DVLineCache = null;
            }
            e.Handled = true;
        }



        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (DVLineCache !=null)
            {
                var point = e.GetPosition(DrawCanvas);
                DVLineCache.Points.RemoveAt(DVLineCache.Points.Count - 1);
                DVLineCache.Points.Add(point);
                DVLineCache.Render();
            }
            e.Handled = true;
        }

        private void MouseEnter(object sender, MouseEventArgs e)
        {
        }

        private void MouseLeave(object sender, MouseEventArgs e)
        {

        }


        public void Dispose()
        {
            UnLoad();
            GC.SuppressFinalize(this);
        }
    }
}
