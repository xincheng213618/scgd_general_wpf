using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class TextManagerConfig : ViewModelBase
    {
        public bool IsLocked { get => _IsLocked; set { _IsLocked = value; OnPropertyChanged(); } }
        private bool _IsLocked;

        public double DefaultFontSize { get => _DefaultFontSize; set { _DefaultFontSize = value; OnPropertyChanged(); } }
        private double _DefaultFontSize = 18;

        public string DefaultText { get => _DefaultText; set { _DefaultText = value; OnPropertyChanged(); } }
        private string _DefaultText = "Text";

        public bool FollowZoom { get => _FollowZoom; set { _FollowZoom = value; OnPropertyChanged(); } }
        private bool _FollowZoom = true;
    }

    public class TextManager : IEditorToggleToolBase, IDisposable
    {
        public TextManagerConfig Config { get; set; } = new TextManagerConfig();
        private Zoombox Zoombox => EditorContext.Zoombox;
        private DrawCanvas DrawCanvas => EditorContext.DrawCanvas;
        public ImageViewModel ImageViewModel => EditorContext.ImageViewModel;
        public EditorContext EditorContext { get; set; }

        public TextManager(EditorContext context)
        {
            EditorContext = context;
            ToolBarLocal = ToolBarLocal.Draw;
            Order = 8;
            Icon = new TextBlock() { Text = "T" };
        }

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
        private bool _IsChecked;


        private DVText? TextCache;
        private Point MouseDownP;
        private bool IsMouseDown;
        private int CheckNo()
        {
            if (ImageViewModel.DrawingVisualLists.Count > 0 && ImageViewModel.DrawingVisualLists.Last() is DrawingVisualBase drawingVisual)
            {
                return drawingVisual.ID + 1;
            }
            else
            {
                return 1;
            }
        }

        private void Load()
        {
            DrawCanvas.MouseMove += MouseMove;
            DrawCanvas.MouseEnter += MouseEnter;
            DrawCanvas.MouseLeave += MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp += Image_PreviewMouseUp;
        }

        private void UnLoad()
        {
            DrawCanvas.MouseMove -= MouseMove;
            DrawCanvas.MouseEnter -= MouseEnter;
            DrawCanvas.MouseLeave -= MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp -= Image_PreviewMouseUp;
            TextCache = null;
            ImageViewModel.SelectEditorVisual.ClearRender();
        }

        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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

            if (TextCache != null) return;

            int did = CheckNo();
            TextProperties textProperties = new TextProperties();
            textProperties.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000"));
            textProperties.Text = "请在这里输入";
            textProperties.Id = did;
            textProperties.Text = Config.DefaultText + "_" + did;
            textProperties.Position = MouseDownP;
            textProperties.Pen = new Pen(Brushes.Red, 1 / Zoombox.ContentMatrix.M11);
            textProperties.TextAttribute.FontSize = Config.DefaultFontSize;
            textProperties.Rect = new Rect(MouseDownP.X, MouseDownP.Y,100,100);
            TextCache = new DVText(textProperties);
            TextCache.Render();
            DrawCanvas.AddVisualCommand(TextCache);
            e.Handled = true;
        }

        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.ReleaseMouseCapture();
            IsMouseDown = false;
            if (TextCache != null)
            {
                ImageViewModel.SelectEditorVisual.SetRender(TextCache);
                if (!Config.IsLocked)
                {
                    Config.DefaultFontSize = TextCache.Attribute.TextAttribute.FontSize * Zoombox.ContentMatrix.M11; // 保存逻辑尺寸
                }
                TextCache = null;
                IsChecked = false;
            }
            e.Handled = true;
        }

        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseDown && TextCache != null)
            {
                var point = e.GetPosition(DrawCanvas);
                // 拖拽改变高度 -> 字号
                double deltaY = System.Math.Abs(point.Y - MouseDownP.Y);
                double fontSize = deltaY;
                if (fontSize < 5) fontSize = Config.DefaultFontSize / Zoombox.ContentMatrix.M11; // 最小
                TextCache.Attribute.TextAttribute.FontSize = fontSize / (Config.FollowZoom ? 1 : Zoombox.ContentMatrix.M11);
                TextCache.Render();
            }
            e.Handled = true;
        }

        private void MouseEnter(object sender, MouseEventArgs e) { }
        private void MouseLeave(object sender, MouseEventArgs e) { }

        public void Dispose()
        {
            UnLoad();
            GC.SuppressFinalize(this);
        }
    }
}
