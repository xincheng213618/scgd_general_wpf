using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.ComponentModel;
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

        [Browsable(false)]
        public double DefaultFontSize { get => _DefaultFontSize; set { _DefaultFontSize = value; OnPropertyChanged(); } }
        private double _DefaultFontSize = 18;

        public string DefaultText { get => _DefaultText; set { _DefaultText = value; OnPropertyChanged(); } }
        private string _DefaultText = "Text";

        public bool FollowZoom { get => _FollowZoom; set { _FollowZoom = value; OnPropertyChanged(); } }
        private bool _FollowZoom = true;
    }

    public class TextManager : IEditorToggleToolBase, IDisposable
    {
        private const string DefaultStyleSaveKeyPrefix = "TextManagerDefaultStyleSave_";

        public TextManagerConfig Config { get; set; } = new TextManagerConfig();
        private DefaultTextStyleConfig DefaultTextStyle => DefaultTextStyleConfig.Current;
        private Zoombox Zoombox => EditorContext.Zoombox;
        private DrawCanvas DrawCanvas => EditorContext.DrawCanvas;
        public ImageViewModel ImageViewModel => EditorContext.ImageViewModel;
        public EditorContext EditorContext { get; set; }

        public TextManager(EditorContext context)
        {
            EditorContext = context;
            ToolBarLocal = ToolBarLocal.Draw;
            Order = 8;
            Icon = new TextBlock() { Text = "A" };
            Config.DefaultFontSize = DefaultTextStyle.FontSize;
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
                    ImageViewModel.SlectStackPanel.Children.Add(new TextBlock { Text = "文本工具", Margin = new Thickness(0, 0, 0, 6), FontWeight = FontWeights.SemiBold });
                    ImageViewModel.SlectStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
                    ImageViewModel.SlectStackPanel.Children.Add(new TextBlock { Text = "默认文本样式", Margin = new Thickness(0, 12, 0, 6), FontWeight = FontWeights.SemiBold });
                    ImageViewModel.SlectStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(DefaultTextStyle));
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
            if (EditorContext.DrawingVisualLists.Count > 0 && EditorContext.DrawingVisualLists.Last() is DrawingVisualBase drawingVisual)
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
            Config.DefaultFontSize = DefaultTextStyle.FontSize;
            DefaultTextStyle.PropertyChanged += DefaultTextStyle_PropertyChanged;
            DrawCanvas.MouseMove += MouseMove;
            DrawCanvas.MouseEnter += MouseEnter;
            DrawCanvas.MouseLeave += MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp += Image_PreviewMouseUp;
        }

        private void UnLoad()
        {
            DefaultTextStyle.PropertyChanged -= DefaultTextStyle_PropertyChanged;
            DrawCanvas.MouseMove -= MouseMove;
            DrawCanvas.MouseEnter -= MouseEnter;
            DrawCanvas.MouseLeave -= MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp -= Image_PreviewMouseUp;
            TextCache = null;
            ImageViewModel.SelectEditorVisual.ClearRender();
        }

        private void DefaultTextStyle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DefaultTextStyleConfig.FontSize))
            {
                Config.DefaultFontSize = DefaultTextStyle.FontSize;
            }

            DebounceTimer.AddOrResetTimer(DefaultStyleSaveKeyPrefix + EditorContext.Id, 120, DefaultTextStyleConfig.SaveCurrent);
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
            textProperties.Text = "������������";
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
                    Config.DefaultFontSize = TextCache.Attribute.TextAttribute.FontSize * Zoombox.ContentMatrix.M11; // �����߼��ߴ�
                    DefaultTextStyle.FontSize = Config.DefaultFontSize;
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
                // ��ק�ı�߶� -> �ֺ�
                double deltaY = System.Math.Abs(point.Y - MouseDownP.Y);
                double fontSize = deltaY;
                if (fontSize < 5) fontSize = Config.DefaultFontSize / Zoombox.ContentMatrix.M11; // ��С
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
