using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Draw
{
    public class TextProperties : BaseProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();
        public bool IsShowText { get; set; } = true;

        [Category("Text"), DisplayName("文本")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("字体大小")]
        public double FontSize { get => TextAttribute.FontSize; set { TextAttribute.FontSize = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("颜色"), JsonIgnore]
        public Brush Foreground { get => TextAttribute.Brush; set { TextAttribute.Brush = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("字体"), JsonIgnore]
        public FontFamily FontFamily { get => TextAttribute.FontFamily; set { TextAttribute.FontFamily = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("FontStyle"), JsonIgnore]
        public FontStyle FontStyle { get => TextAttribute.FontStyle; set { TextAttribute.FontStyle = value; OnPropertyChanged(); } }
        [Category("Text"), DisplayName("FontWeight"), JsonIgnore]
        public FontWeight FontWeight { get => TextAttribute.FontWeight; set { TextAttribute.FontWeight = value; OnPropertyChanged(); } }
        [Category("Text"), DisplayName("FontStretch"), JsonIgnore]
        public FontStretch FontStretch { get => TextAttribute.FontStretch; set { TextAttribute.FontStretch = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("FlowDirection"), JsonIgnore]
        public FlowDirection FlowDirection { get => TextAttribute.FlowDirection; set { TextAttribute.FlowDirection = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("位置")]
        public Point Position { get => _Position; set { if (_Position == value) return; _Position = value; OnPropertyChanged(); } }
        private Point _Position = new Point(50,50);

        [Browsable(false)]
        public Rect Rect { get => _Rect; set { _Rect = value; OnPropertyChanged(); } }
        private Rect _Rect = new Rect(50,50,0,0);

        [Browsable(false), JsonIgnore]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen = new Pen(Brushes.Red,1);

        [Category("Text"), DisplayName("背景"), JsonIgnore]
        public Brush Background { get => _Background; set { _Background = value; OnPropertyChanged(); } }
        private Brush _Background = Brushes.Transparent;

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        [Browsable(false)]
        public bool IsEditing { get => _IsEditing; set { _IsEditing = value; OnPropertyChanged(); } }
        private bool _IsEditing;
    }

    public class DVText : DrawingVisualBase<TextProperties>, IDrawingVisual, IEditableDrawingVisual, ILayoutScaleDrawingVisual, ICompactInspectorProvider
    {
        public TextAttribute TextAttribute => Attribute.TextAttribute;

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        private TextBox? _editTextBox;
        private Panel? _editHost;
        private DrawEditorContext? _editorContext;
        private string _originalText = string.Empty;
        private bool _isEditing;

        public DVText()
        {
            Attribute = new TextProperties();
            Attribute.Text = string.Empty;
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10; // 与其它图元保持一致缩放策略
            Attribute.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(TextProperties.Rect))
                    Render();
            };
        }
        public DVText(TextProperties textProperties)
        {
            Attribute = textProperties;
            if (Attribute.FontSize <= 0)
                TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            Attribute.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(TextProperties.Rect))
                    Render();
            };
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            ApplyLayoutScaleCore(context, Pen, value => Pen = value, TextAttribute.FontSize, value => TextAttribute.FontSize = value);
        }

        public override void Render()
        {
            // 如果处于编辑模式，不渲染 DrawingVisual
            if (_isEditing) return;

            using DrawingContext dc = RenderOpen();
            FormattedText formattedText = new(
                TextAttribute.Text,
                CultureInfo.CurrentCulture,
                TextAttribute.FlowDirection,
                new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch),
                TextAttribute.FontSize,
                TextAttribute.Brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // 更新 Rect 以包含文本实际尺寸
            var textWidth = formattedText.Width;
            var textHeight = formattedText.Height;
            Attribute.Rect = new Rect(Attribute.Position.X, Attribute.Position.Y, textWidth, textHeight);

            if (Attribute.Background != null && Attribute.Background != Brushes.Transparent)
            {
                dc.DrawRectangle(Attribute.Background, null, Attribute.Rect);
            }
            dc.DrawText(formattedText, Attribute.Position);
        }

        public override Rect GetRect() => Attribute.Rect;

        public override void SetRect(Rect rect)
        {
            Attribute.Rect = rect;
            Attribute.Position = new Point(rect.X, rect.Y);
            Render();
        }

        private double GetEditorScreenFontSize()
        {
            double zoomRatio = _editorContext?.ZoomRatio ?? 1;
            if (double.IsNaN(zoomRatio) || double.IsInfinity(zoomRatio) || zoomRatio <= 0)
            {
                zoomRatio = 1;
            }

            return Math.Max(TextAttribute.FontSize * zoomRatio, 1);
        }

        private FormattedText CreateFormattedText(string text, double fontSize)
        {
            string measuredText = string.IsNullOrEmpty(text) ? " " : text;
            return new FormattedText(
                measuredText,
                CultureInfo.CurrentCulture,
                TextAttribute.FlowDirection,
                new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch),
                fontSize,
                TextAttribute.Brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private void ClearVisual()
        {
            using DrawingContext dc = RenderOpen();
        }

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems(EditorContext context)
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Text), Icon = CompactInspectorIcons.CreateText("T"), Order = 10, Width = 140, EditorKind = CompactInspectorEditorKind.Text, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Text },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Foreground), Order = 20, EditorKind = CompactInspectorEditorKind.Brush, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Color },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.FontSize), Icon = CompactInspectorIcons.CreateText("A"), Width = 56, Order = 30, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_FontSize },
            };
        }

        private void UpdateEditorBounds()
        {
            if (_editTextBox == null || _editorContext == null)
            {
                return;
            }

            double editorFontSize = GetEditorScreenFontSize();
            FormattedText formattedText = CreateFormattedText(_editTextBox.Text, editorFontSize);
            double textWidth = Math.Max(formattedText.WidthIncludingTrailingWhitespace, 1);
            double textHeight = Math.Max(formattedText.Height, editorFontSize);
            Point overlayPoint = _editorContext.TranslatePointToTextEditorOverlay(Attribute.Position);

            _editTextBox.FontSize = editorFontSize;
            _editTextBox.MinWidth = Math.Max(editorFontSize, 12);
            _editTextBox.MinHeight = Math.Max(editorFontSize + 2, 12);
            _editTextBox.Width = Math.Max(textWidth + 4, _editTextBox.MinWidth);
            _editTextBox.Height = Math.Max(textHeight + 4, _editTextBox.MinHeight);

            Canvas.SetLeft(_editTextBox, overlayPoint.X);
            Canvas.SetTop(_editTextBox, overlayPoint.Y);

            Attribute.Rect = new Rect(Attribute.Position.X, Attribute.Position.Y, textWidth, textHeight);
        }

        private TextBox CreateEditorTextBox()
        {
            return new TextBox
            {
                Text = Attribute.Text,
                FontSize = GetEditorScreenFontSize(),
                FontFamily = TextAttribute.FontFamily,
                FontStyle = TextAttribute.FontStyle,
                FontWeight = TextAttribute.FontWeight,
                FontStretch = TextAttribute.FontStretch,
                FlowDirection = TextAttribute.FlowDirection,
                Foreground = TextAttribute.Brush,
                CaretBrush = TextAttribute.Brush,
                Background = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.DeepSkyBlue,
                Padding = new Thickness(0),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                MinWidth = 12,
                MinHeight = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        private void FocusEditor()
        {
            if (_editTextBox == null)
            {
                return;
            }

            _editTextBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_editTextBox == null)
                {
                    return;
                }

                _editTextBox.Focus();
                Keyboard.Focus(_editTextBox);
                _editTextBox.SelectAll();
            }), DispatcherPriority.Input);
        }

        private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_editTextBox == null)
            {
                return;
            }

            Attribute.Text = _editTextBox.Text;
            UpdateEditorBounds();
        }

        private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                EndEdit(false);
                return;
            }

            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                EndEdit(true);
            }
        }

        private void OnEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_isEditing)
            {
                EndEdit(true);
            }
        }

        private void OnZoomChanged(object? sender, EventArgs e)
        {
            UpdateEditorBounds();
        }

        private bool ShouldRemoveEmptyText()
        {
            return string.IsNullOrWhiteSpace(Attribute.Text);
        }

        private void RemoveFromCanvas()
        {
            if (_editorContext == null)
            {
                return;
            }

            _editorContext.SelectionVisual.ClearRender();
            _editorContext.DrawCanvas.RemoveVisualCommand(this);
        }

        private void DetachEditorTextBox()
        {
            if (_editTextBox == null)
            {
                return;
            }

            _editTextBox.TextChanged -= OnEditorTextChanged;
            _editTextBox.PreviewKeyDown -= OnEditorPreviewKeyDown;
            _editTextBox.LostKeyboardFocus -= OnEditorLostKeyboardFocus;

            if (_editHost != null && _editHost.Children.Contains(_editTextBox))
            {
                _editHost.Children.Remove(_editTextBox);
            }

            _editTextBox = null;
        }

        #region IEditableDrawingVisual 实现

        /// <summary>
        /// 是否支持双击编辑
        /// </summary>
        public bool SupportsDoubleClickEditing => true;

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        public bool IsEditing => _isEditing;

        /// <summary>
        /// 开始编辑
        /// </summary>
        public void BeginEdit(EditorContext context)
        {
            BeginEdit(context.DrawEditorContext);
        }

        public void BeginEdit(DrawEditorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (_isEditing)
            {
                FocusEditor();
                return;
            }

            _editorContext = context;
            _editHost = context.TextEditorOverlay;
            _originalText = Attribute.Text;
            _isEditing = true;
            Attribute.IsEditing = true;
            context.SelectionVisual.ClearRender();
            context.Zoombox.ContentMatrixChanged += OnZoomChanged;

            _editTextBox = CreateEditorTextBox();
            _editTextBox.TextChanged += OnEditorTextChanged;
            _editTextBox.PreviewKeyDown += OnEditorPreviewKeyDown;
            _editTextBox.LostKeyboardFocus += OnEditorLostKeyboardFocus;

            _editHost.Children.Add(_editTextBox);
            Panel.SetZIndex(_editTextBox, 1000);

            ClearVisual();
            UpdateEditorBounds();
            FocusEditor();
        }

        /// <summary>
        /// 结束编辑
        /// </summary>
        public void EndEdit(bool saveChanges)
        {
            if (!_isEditing)
            {
                return;
            }

            if (saveChanges && _editTextBox != null)
            {
                Attribute.Text = _editTextBox.Text;
            }
            else
            {
                Attribute.Text = _originalText;
            }

            DetachEditorTextBox();

            if (_editorContext != null)
            {
                _editorContext.Zoombox.ContentMatrixChanged -= OnZoomChanged;
            }

            _isEditing = false;
            Attribute.IsEditing = false;

            bool removeEmptyText = ShouldRemoveEmptyText();
            if (!removeEmptyText)
            {
                Render();
            }

            if (removeEmptyText)
            {
                RemoveFromCanvas();
            }
            else
            {
                _editorContext?.SelectionVisual.SetRender(this);
            }

            _editHost = null;
            _editorContext = null;
        }

        /// <summary>
        /// 处理双击事件
        /// </summary>
        public bool HandleDoubleClick(EditorContext context, Point point)
        {
            return HandleDoubleClick(context.DrawEditorContext, point);
        }

        public bool HandleDoubleClick(DrawEditorContext context, Point point)
        {
            if (GetRect().Contains(point))
            {
                BeginEdit(context);
                return true;
            }
            return false;
        }

        #endregion
    }

    /// <summary>
    /// 可编辑的绘图视觉接口
    /// </summary>
    public interface IEditableDrawingVisual
    {
        /// <summary>
        /// 是否支持双击编辑
        /// </summary>
        bool SupportsDoubleClickEditing { get; }

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        bool IsEditing { get; }

        /// <summary>
        /// 开始编辑
        /// </summary>
        void BeginEdit(DrawEditorContext context);

        /// <summary>
        /// 结束编辑
        /// </summary>
        /// <param name="saveChanges">是否保存更改</param>
        void EndEdit(bool saveChanges);

        /// <summary>
        /// 处理双击事件
        /// </summary>
        /// <param name="point">点击位置</param>
        /// <returns>是否处理了事件</returns>
        bool HandleDoubleClick(DrawEditorContext context, Point point);
    }
}
