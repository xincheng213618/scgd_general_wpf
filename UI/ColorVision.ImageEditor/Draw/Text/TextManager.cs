using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System;
using System.Collections.Generic;
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
        private string _DefaultText = string.Empty;

        public bool FollowZoom { get => _FollowZoom; set { _FollowZoom = value; OnPropertyChanged(); } }
        private bool _FollowZoom = true;
    }

    public class TextManager : DrawEditorToggleToolBase, ICompactInspectorProvider, IDisposable
    {
        private const string DefaultStyleSaveKeyPrefix = "TextManagerDefaultStyleSave_";

        public TextManagerConfig Config { get; set; } = new TextManagerConfig();
        private static DefaultTextStyleConfig DefaultTextStyle => DefaultTextStyleConfig.Current;
        private Zoombox Zoombox => EditorContext.Zoombox;
        private DrawCanvas DrawCanvas => EditorContext.DrawCanvas;
        public DrawEditorContext EditorContext { get; set; }


        public TextManager(DrawEditorContext context)
        {
            EditorContext = context;
            ToolBarLocal = ToolBarLocal.Draw;
            Order = 8;
            Icon = new TextBlock() { Text = "A" };
            Config.DefaultFontSize = DefaultTextStyle.FontSize;
            Config.PropertyChanged += Config_PropertyChanged;
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
                    Load();
                }
                else
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(null);
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
            EditorContext.SelectionVisual.ClearRender();
        }

        private void DefaultTextStyle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DefaultTextStyleConfig.FontSize))
            {
                Config.DefaultFontSize = DefaultTextStyle.FontSize;
            }

            DebounceTimer.AddOrResetTimer(DefaultStyleSaveKeyPrefix + EditorContext.Id, 120, DefaultTextStyleConfig.SaveCurrent);
        }

        private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TextManagerConfig.DefaultFontSize) && DefaultTextStyle.FontSize != Config.DefaultFontSize)
            {
                DefaultTextStyle.FontSize = Config.DefaultFontSize;
            }
        }

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems(EditorContext context)
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = DefaultTextStyle, PropertyName = nameof(DefaultTextStyle.FontSize), Icon = CompactInspectorIcons.CreateText("A"), Width = 56, Order = 10, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_DefaultFontSize },
                new CompactInspectorPropertyItem { Source = DefaultTextStyle, PropertyName = nameof(DefaultTextStyle.Brush), Order = 20, EditorKind = CompactInspectorEditorKind.Brush, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_DefaultColor },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.FollowZoom), Icon = CompactInspectorIcons.CreateText("⤢"), Order = 30, EditorKind = CompactInspectorEditorKind.Toggle, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_FollowZoom },
            };
        }

        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;

            if (EditorContext.SelectionVisual.GetContainingRect(MouseDownP))
            {
                return;
            }
            else
            {
                EditorContext.SelectionVisual.ClearRender();
            }

            if (TextCache != null) return;

            int did = CheckNo();
            TextProperties textProperties = new TextProperties();
            textProperties.Background = Brushes.Transparent;
            textProperties.Id = did;
            textProperties.Text = Config.DefaultText;
            textProperties.Position = MouseDownP;
            textProperties.Pen = new Pen(Brushes.Transparent, 1 / Zoombox.ContentMatrix.M11);
            textProperties.TextAttribute.FontSize = Config.DefaultFontSize;
            textProperties.Rect = new Rect(MouseDownP.X, MouseDownP.Y, 1, Config.DefaultFontSize);
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
                DVText createdText = TextCache;
                TextCache = null;
                EditorContext.SelectionVisual.SetRender(createdText);
                createdText.BeginEdit(EditorContext);
                IsChecked = false;
            }
            e.Handled = true;
        }

        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseDown && TextCache != null)
            {
                e.Handled = true;
            }
        }

        private void MouseEnter(object sender, MouseEventArgs e) { }
        private void MouseLeave(object sender, MouseEventArgs e) { }

        public void Dispose()
        {
            Config.PropertyChanged -= Config_PropertyChanged;
            UnLoad();
            GC.SuppressFinalize(this);
        }
    }
}
