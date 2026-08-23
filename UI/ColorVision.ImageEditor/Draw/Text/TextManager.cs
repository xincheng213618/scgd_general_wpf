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
        [Browsable(false)]
        public bool IsLocked { get => _IsLocked; set { if (_IsLocked == value) return; _IsLocked = value; OnPropertyChanged(); } }
        private bool _IsLocked;

        [Browsable(false)]
        public double DefaultFontSize
        {
            get => _DefaultFontSize;
            set
            {
                double next = double.IsFinite(value) && value > 0 ? TextRenderCore.NormalizeFontSize(value) : 10;
                if (_DefaultFontSize.Equals(next))
                    return;

                _DefaultFontSize = next;
                OnPropertyChanged();
            }
        }
        private double _DefaultFontSize = 18;

        public string DefaultText { get => _DefaultText; set { if (string.Equals(_DefaultText, value, StringComparison.Ordinal)) return; _DefaultText = value; OnPropertyChanged(); } }
        private string _DefaultText = string.Empty;

        [Browsable(false)]
        public bool FollowZoom { get => _FollowZoom; set { if (_FollowZoom == value) return; _FollowZoom = value; OnPropertyChanged(); } }
        private bool _FollowZoom = true;
    }

    public class TextManager : TextDrawingToolBase, ICompactInspectorProvider, IDisposable
    {
        private const string DefaultStyleSaveKeyPrefix = "TextManagerDefaultStyleSave_";

        public TextManagerConfig Config
        {
            get => _config;
            set
            {
                TextManagerConfig next = value ?? new TextManagerConfig();
                if (ReferenceEquals(_config, next))
                {
                    return;
                }

                _config.PropertyChanged -= Config_PropertyChanged;
                _config = next;
                _config.PropertyChanged += Config_PropertyChanged;
            }
        }
        private TextManagerConfig _config = new TextManagerConfig();
        private static DefaultTextStyleConfig DefaultTextStyle => DefaultTextStyleConfig.Current;


        public TextManager(TextEditingContext context)
            : base(context)
        {
            Order = 8;
            Icon = new TextBlock() { Text = "A" };
            _config.DefaultFontSize = DefaultTextStyle.FontSize;
            _config.PropertyChanged += Config_PropertyChanged;
        }

        public override bool IsChecked
        {
            get => _IsChecked; set
            {
                if (_IsChecked == value) return;
                _IsChecked = value;
                if (value)
                {
                    TextContext.DrawEditorManager.SetCurrentDrawEditor(this);
                    Load();
                }
                else
                {
                    TextContext.DrawEditorManager.SetCurrentDrawEditor(null);
                    UnLoad();
                }
                OnPropertyChanged();
            }
        }
        private bool _IsChecked;


        private DVText? TextCache;
        private ActionCommand? PendingCreationCommand;
        private Point MouseDownP;
        private bool IsMouseDown;
        private int CheckNo()
        {
            if (TextContext.DrawingVisualLists.Count > 0 && TextContext.DrawingVisualLists.Last() is DrawingVisualBase drawingVisual)
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
            DrawCanvas.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp += Image_PreviewMouseUp;
            DrawCanvas.LostMouseCapture += DrawCanvas_LostMouseCapture;
            DrawCanvas.VisualsRemove += DrawCanvas_VisualsRemove;
        }

        private void UnLoad()
        {
            DefaultTextStyle.PropertyChanged -= DefaultTextStyle_PropertyChanged;
            DrawCanvas.MouseMove -= MouseMove;
            DrawCanvas.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp -= Image_PreviewMouseUp;
            DrawCanvas.LostMouseCapture -= DrawCanvas_LostMouseCapture;
            DrawCanvas.VisualsRemove -= DrawCanvas_VisualsRemove;
            bool releaseMouseCapture = IsMouseDown && DrawCanvas.IsMouseCaptured;
            IsMouseDown = false;
            if (releaseMouseCapture)
                DrawCanvas.ReleaseMouseCapture();
            CancelPendingCreation();
            SelectionVisual.ClearRender();
        }

        private void DrawCanvas_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (!IsMouseDown)
                return;

            IsMouseDown = false;
            CancelPendingCreation();
            IsChecked = false;
        }

        private void CancelPendingCreation()
        {
            DVText? pendingText = TextCache;
            ActionCommand? creationCommand = PendingCreationCommand;
            TextCache = null;
            PendingCreationCommand = null;

            if (pendingText != null && DrawCanvas.ContainsVisual(pendingText))
            {
                DrawCanvas.RemoveVisual(pendingText);
            }

            if (creationCommand != null)
            {
                DrawCanvas.DiscardActionCommand(creationCommand);
            }
        }

        private void DefaultTextStyle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DefaultTextStyleConfig.FontSize))
            {
                Config.DefaultFontSize = DefaultTextStyle.FontSize;
            }

            DebounceTimer.AddOrResetTimer(DefaultStyleSaveKeyPrefix, 120, DefaultTextStyleConfig.SaveCurrent);
        }

        private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TextManagerConfig.DefaultFontSize) && DefaultTextStyle.FontSize != Config.DefaultFontSize)
            {
                DefaultTextStyle.FontSize = Config.DefaultFontSize;
            }
        }

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems()
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = DefaultTextStyle, PropertyName = nameof(DefaultTextStyle.FontSize), Icon = CompactInspectorIcons.CreateText("A"), Width = 56, Order = 10, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_DefaultFontSize },
                new CompactInspectorPropertyItem { Source = DefaultTextStyle, PropertyName = nameof(DefaultTextStyle.Brush), Order = 20, EditorKind = CompactInspectorEditorKind.Brush, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_DefaultColor },
            };
        }

        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;

            if (SelectionVisual.GetContainingRect(MouseDownP))
            {
                return;
            }
            else
            {
                SelectionVisual.ClearRender();
            }

            if (TextCache != null) return;

            int did = CheckNo();
            TextProperties textProperties = new TextProperties();
            textProperties.Background = Brushes.Transparent;
            textProperties.Id = did;
            textProperties.Text = Config.DefaultText;
            textProperties.Position = MouseDownP;
            double fontSize = TextRenderCore.NormalizeFontSize(Config.DefaultFontSize);
            double zoomRatio = Math.Abs(Zoombox.ContentMatrix.M11);
            if (!double.IsFinite(zoomRatio) || zoomRatio <= 0)
                zoomRatio = 1;
            textProperties.Pen = new Pen(Brushes.Transparent, 1 / zoomRatio);
            textProperties.TextAttribute.FontSize = fontSize;
            textProperties.Rect = new Rect(MouseDownP.X, MouseDownP.Y, 1, fontSize);
            DVText createdText = new(textProperties);
            TextCache = createdText;
            createdText.Render();
            PendingCreationCommand = null;
            ActionCommand? creationCommand = DrawCanvas.AddVisualCommandCore(createdText);
            if (creationCommand == null || !IsChecked || !ReferenceEquals(TextCache, createdText) || !DrawCanvas.ContainsVisual(createdText))
            {
                if (DrawCanvas.ContainsVisual(createdText))
                    DrawCanvas.RemoveVisual(createdText);
                if (creationCommand != null)
                    DrawCanvas.DiscardActionCommand(creationCommand);
                if (ReferenceEquals(TextCache, createdText))
                    TextCache = null;
                e.Handled = true;
                return;
            }

            PendingCreationCommand = creationCommand;
            createdText.TrackCreationCommand(creationCommand);
            e.Handled = true;
        }

        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsMouseDown || e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            IsMouseDown = false;
            DrawCanvas.ReleaseMouseCapture();
            if (TextCache != null)
            {
                DVText createdText = TextCache;
                TextCache = null;
                PendingCreationCommand = null;
                createdText.BeginEdit(TextContext);
                IsChecked = false;
            }
            e.Handled = true;
        }

        private void DrawCanvas_VisualsRemove(object? sender, VisualChangedEventArgs e)
        {
            if (TextCache == null || !ReferenceEquals(e.Visual, TextCache))
            {
                return;
            }

            DVText removedText = TextCache;
            ActionCommand? creationCommand = PendingCreationCommand;
            TextCache = null;
            PendingCreationCommand = null;
            IsMouseDown = false;
            DrawCanvas.ReleaseMouseCapture();

            if (creationCommand != null && !DrawCanvas.IsVisualRemovalCommandInProgress(removedText))
            {
                DrawCanvas.DiscardActionCommand(creationCommand);
            }
        }

        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseDown && TextCache != null)
            {
                e.Handled = true;
            }
        }

        public void Dispose()
        {
            if (IsChecked)
            {
                IsChecked = false;
            }
            else
            {
                CancelPendingCreation();
            }

            _config.PropertyChanged -= Config_PropertyChanged;
            GC.SuppressFinalize(this);
        }
    }
}
