using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Draw
{
    public class TextProperties : BaseProperties, ITextProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute
        {
            get => _textAttribute;
            set
            {
                TextAttribute next = value ?? new TextAttribute();
                if (ReferenceEquals(_textAttribute, next))
                {
                    return;
                }

                _textAttribute.PropertyChanged -= TextAttribute_PropertyChanged;
                _textAttribute = next;
                _textAttribute.PropertyChanged += TextAttribute_PropertyChanged;
                OnPropertyChanged();
            }
        }
        private TextAttribute _textAttribute;

        public bool IsShowText
        {
            get => _isShowText;
            set
            {
                if (_isShowText == value)
                {
                    return;
                }

                _isShowText = value;
                OnPropertyChanged();
            }
        }
        private bool _isShowText = true;

        public TextProperties()
        {
            _textAttribute = new TextAttribute();
            _textAttribute.PropertyChanged += TextAttribute_PropertyChanged;
        }

        [Category("Text"), DisplayName("文本")]
        public string Text { get => TextAttribute.Text; set => TextAttribute.Text = value; }

        [Category("Text"), DisplayName("字体大小")]
        public double FontSize { get => TextAttribute.FontSize; set => TextAttribute.FontSize = value; }

        [Category("Text"), DisplayName("颜色"), JsonIgnore]
        public Brush Foreground { get => TextAttribute.Brush; set => TextAttribute.Brush = value; }

        [Category("Text"), DisplayName("字体"), JsonIgnore]
        public FontFamily FontFamily { get => TextAttribute.FontFamily; set => TextAttribute.FontFamily = value; }

        [Category("Text"), DisplayName("FontStyle"), JsonIgnore]
        public FontStyle FontStyle { get => TextAttribute.FontStyle; set => TextAttribute.FontStyle = value; }
        [Category("Text"), DisplayName("FontWeight"), JsonIgnore]
        public FontWeight FontWeight { get => TextAttribute.FontWeight; set => TextAttribute.FontWeight = value; }
        [Category("Text"), DisplayName("FontStretch"), JsonIgnore]
        public FontStretch FontStretch { get => TextAttribute.FontStretch; set => TextAttribute.FontStretch = value; }

        [Category("Text"), DisplayName("FlowDirection"), JsonIgnore]
        public FlowDirection FlowDirection { get => TextAttribute.FlowDirection; set => TextAttribute.FlowDirection = value; }

        [Category("Text"), DisplayName("位置")]
        public Point Position { get => _Position; set { if (_Position == value) return; _Position = value; OnPropertyChanged(); } }
        private Point _Position = new Point(50,50);

        [Browsable(false)]
        public Rect Rect { get => _Rect; set { if (_Rect == value) return; _Rect = value; OnPropertyChanged(); } }
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

        private void TextAttribute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            string propertyName = e.PropertyName switch
            {
                nameof(TextAttribute.Text) => nameof(Text),
                nameof(TextAttribute.FontSize) => nameof(FontSize),
                nameof(TextAttribute.Brush) => nameof(Foreground),
                nameof(TextAttribute.FontFamily) => nameof(FontFamily),
                nameof(TextAttribute.FontStyle) => nameof(FontStyle),
                nameof(TextAttribute.FontWeight) => nameof(FontWeight),
                nameof(TextAttribute.FontStretch) => nameof(FontStretch),
                nameof(TextAttribute.FlowDirection) => nameof(FlowDirection),
                _ => nameof(TextAttribute),
            };
            OnPropertyChanged(propertyName);
        }
    }

    public class DVText : DrawingVisualBase<TextProperties>, IDrawingVisual, IEditableDrawingVisual, ILayoutScaleDrawingVisual, ICompactInspectorProvider
    {
        public TextAttribute TextAttribute => Attribute.TextAttribute;

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        private TextBox? _editTextBox;
        private Panel? _editHost;
        private TextEditingContext? _textContext;
        private string _originalText = string.Empty;
        private bool _isEditing;
        private DrawingVisualScaleContext _layoutScaleContext = new(false, 1, 0);
        private Rect _renderBounds = Rect.Empty;
        private Matrix _editorTransform;
        private bool _hasEditorTransform;
        private Transform? _trackedCanvasTransform;
        private bool _tracksCanvasTransformChanges;
        private bool _canvasTransformUpdatePending;
        private ActionCommand? _creationCommand;

        public DVText()
        {
            Attribute = new TextProperties();
            Attribute.Text = string.Empty;
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10; // 与其它图元保持一致缩放策略
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }
        public DVText(TextProperties textProperties)
        {
            Attribute = textProperties;
            if (!double.IsFinite(Attribute.FontSize) || Attribute.FontSize <= 0)
                TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }

        internal void TrackCreationCommand(ActionCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            _creationCommand = command;
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            double scale = double.IsFinite(context.Scale) && context.Scale > 0 ? context.Scale : 1;
            double fontSizeOverride = double.IsFinite(context.TextFontSizeOverride) && context.TextFontSizeOverride > 0
                ? context.TextFontSizeOverride
                : 0;
            DrawingVisualScaleContext normalizedContext = new(context.IsLayoutUpdated, scale, fontSizeOverride);
            if (_layoutScaleContext == normalizedContext)
            {
                return;
            }

            _layoutScaleContext = normalizedContext;
            if (_isEditing)
            {
                UpdateEditorBounds();
            }
            else
            {
                Render();
            }
        }

        public override void Render()
        {
            // 如果处于编辑模式，不渲染 DrawingVisual
            if (_isEditing) return;

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double authoredFontSize = TextRenderCore.NormalizeFontSize(TextAttribute.FontSize);
            double renderFontSize = GetRenderFontSize();
            FormattedText formattedText = TextRenderCore.CreateFormattedText(TextAttribute, TextAttribute.Text, renderFontSize, pixelsPerDip, measureEmptyText: true);
            _renderBounds = TextRenderCore.GetBounds(formattedText, Attribute.Position);
            Attribute.Rect = renderFontSize == authoredFontSize
                ? _renderBounds
                : TextRenderCore.Measure(TextAttribute, TextAttribute.Text, Attribute.Position, authoredFontSize, pixelsPerDip, measureEmptyText: true);

            using DrawingContext dc = RenderOpen();
            if (!Attribute.IsShowText || string.IsNullOrEmpty(TextAttribute.Text))
            {
                return;
            }

            dc.DrawRectangle(Attribute.Background ?? Brushes.Transparent, null, _renderBounds);
            dc.DrawText(formattedText, Attribute.Position);
        }

        public override Rect GetRect() => _renderBounds.IsEmpty ? Attribute.Rect : _renderBounds;

        public override void SetRect(Rect rect)
        {
            Attribute.Position = new Point(rect.X, rect.Y);
        }

        private void Attribute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isEditing && _editTextBox != null)
            {
                switch (e.PropertyName)
                {
                    case nameof(TextProperties.Text):
                        SynchronizeEditorText();
                        break;
                    case nameof(TextProperties.FontSize):
                    case nameof(TextProperties.FontFamily):
                    case nameof(TextProperties.FontStyle):
                    case nameof(TextProperties.FontWeight):
                    case nameof(TextProperties.FontStretch):
                    case nameof(TextProperties.FlowDirection):
                        ApplyEditorStyle(_editTextBox);
                        UpdateEditorBounds();
                        break;
                    case nameof(TextProperties.TextAttribute):
                        ApplyEditorStyle(_editTextBox);
                        if (!SynchronizeEditorText())
                        {
                            UpdateEditorBounds();
                        }
                        break;
                    case nameof(TextProperties.Foreground):
                    case nameof(TextProperties.Background):
                        ApplyEditorStyle(_editTextBox);
                        break;
                    case nameof(TextProperties.Position):
                        UpdateEditorTransform();
                        break;
                }

                return;
            }

            if (AffectsRendering(e.PropertyName))
            {
                Render();
            }
        }

        private static bool AffectsRendering(string? propertyName)
        {
            return string.IsNullOrEmpty(propertyName) || propertyName is
                nameof(TextProperties.Text) or
                nameof(TextProperties.FontSize) or
                nameof(TextProperties.Foreground) or
                nameof(TextProperties.FontFamily) or
                nameof(TextProperties.FontStyle) or
                nameof(TextProperties.FontWeight) or
                nameof(TextProperties.FontStretch) or
                nameof(TextProperties.FlowDirection) or
                nameof(TextProperties.TextAttribute) or
                nameof(TextProperties.IsShowText) or
                nameof(TextProperties.Position) or
                nameof(TextProperties.Background);
        }

        private bool SynchronizeEditorText()
        {
            if (_editTextBox == null)
            {
                return false;
            }

            string modelText = Attribute.Text ?? string.Empty;
            _originalText = modelText;
            if (!string.Equals(_editTextBox.Text, modelText, StringComparison.Ordinal))
            {
                _editTextBox.Text = modelText;
                _editTextBox.CaretIndex = modelText.Length;
                return true;
            }

            return false;
        }

        private double GetRenderFontSize()
        {
            double authoredFontSize = TextRenderCore.NormalizeFontSize(TextAttribute.FontSize);
            if (_layoutScaleContext.IsLayoutUpdated)
            {
                return authoredFontSize * _layoutScaleContext.Scale;
            }

            return _layoutScaleContext.TextFontSizeOverride > 0
                ? _layoutScaleContext.TextFontSizeOverride
                : authoredFontSize;
        }

        private FormattedText CreateFormattedText(string text, double fontSize)
        {
            return TextRenderCore.CreateFormattedText(
                TextAttribute,
                text,
                fontSize,
                VisualTreeHelper.GetDpi(this).PixelsPerDip,
                measureEmptyText: true);
        }

        private void ClearVisual()
        {
            using DrawingContext dc = RenderOpen();
        }

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems()
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Text), Icon = CompactInspectorIcons.CreateText("T"), Order = 10, Width = 140, EditorKind = CompactInspectorEditorKind.Text, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Text },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Foreground), Order = 20, EditorKind = CompactInspectorEditorKind.Brush, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Color },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Background), Order = 30, EditorKind = CompactInspectorEditorKind.Brush },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.FontSize), Icon = CompactInspectorIcons.CreateText("A"), Width = 56, Order = 40, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_FontSize },
            };
        }

        private void UpdateEditorBounds()
        {
            if (_editTextBox == null || _textContext == null)
            {
                return;
            }

            double editorFontSize = GetRenderFontSize();
            FormattedText formattedText = CreateFormattedText(_editTextBox.Text, editorFontSize);
            double textWidth = Math.Max(formattedText.WidthIncludingTrailingWhitespace, 1);
            double textHeight = Math.Max(formattedText.Height, editorFontSize);

            _editTextBox.FontSize = editorFontSize;
            _editTextBox.MinWidth = Math.Max(editorFontSize, 1);
            _editTextBox.MinHeight = Math.Max(editorFontSize, 1);
            _editTextBox.Width = Math.Max(textWidth, _editTextBox.MinWidth);
            _editTextBox.Height = Math.Max(textHeight, _editTextBox.MinHeight);
        }

        private void UpdateEditorTransform()
        {
            if (_editTextBox == null || _textContext == null)
            {
                return;
            }

            Matrix transform = GetEditorTransform();
            if (!_hasEditorTransform || _editorTransform != transform)
            {
                _editorTransform = transform;
                _hasEditorTransform = true;
                if (_editTextBox.RenderTransform is MatrixTransform editorTransform && !editorTransform.IsFrozen)
                {
                    editorTransform.Matrix = transform;
                }
                else
                {
                    _editTextBox.RenderTransform = new MatrixTransform(transform);
                }
            }
        }

        private Matrix GetEditorTransform()
        {
            if (_textContext == null || _editHost == null)
            {
                return Matrix.Identity;
            }

            try
            {
                GeneralTransform transform = _textContext.DrawCanvas.TransformToVisual(_editHost);
                Point origin = transform.Transform(Attribute.Position);
                Point horizontal = transform.Transform(Attribute.Position + new Vector(1, 0));
                Point vertical = transform.Transform(Attribute.Position + new Vector(0, 1));
                Matrix matrix = new(
                    horizontal.X - origin.X,
                    horizontal.Y - origin.Y,
                    vertical.X - origin.X,
                    vertical.Y - origin.Y,
                    origin.X,
                    origin.Y);

                if (IsFinite(matrix))
                {
                    return matrix;
                }
            }
            catch (InvalidOperationException)
            {
            }

            double zoomRatio = _textContext.ZoomRatio;
            if (!double.IsFinite(zoomRatio) || zoomRatio <= 0)
            {
                zoomRatio = 1;
            }

            Point fallbackPosition;
            try
            {
                fallbackPosition = _textContext.TranslatePointToTextEditorOverlay(Attribute.Position);
            }
            catch (InvalidOperationException)
            {
                fallbackPosition = new Point(Attribute.Position.X * zoomRatio, Attribute.Position.Y * zoomRatio);
            }

            return new Matrix(zoomRatio, 0, 0, zoomRatio, fallbackPosition.X, fallbackPosition.Y);
        }

        private static bool IsFinite(Matrix matrix)
        {
            return double.IsFinite(matrix.M11)
                && double.IsFinite(matrix.M12)
                && double.IsFinite(matrix.M21)
                && double.IsFinite(matrix.M22)
                && double.IsFinite(matrix.OffsetX)
                && double.IsFinite(matrix.OffsetY);
        }

        private TextBox CreateEditorTextBox()
        {
            TextBox textBox = new()
            {
                Text = Attribute.Text,
                BorderThickness = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(0),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                FocusVisualStyle = null,
                MinWidth = 1,
                MinHeight = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            ApplyEditorStyle(textBox);
            TextOptions.SetTextFormattingMode(textBox, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(textBox, TextRenderingMode.Auto);
            return textBox;
        }

        private void ApplyEditorStyle(TextBox textBox)
        {
            textBox.FontSize = GetRenderFontSize();
            textBox.FontFamily = TextAttribute.FontFamily;
            textBox.FontStyle = TextAttribute.FontStyle;
            textBox.FontWeight = TextAttribute.FontWeight;
            textBox.FontStretch = TextAttribute.FontStretch;
            textBox.FlowDirection = TextAttribute.FlowDirection;
            textBox.Foreground = TextAttribute.Brush;
            textBox.CaretBrush = TextAttribute.Brush;
            textBox.Background = Attribute.Background ?? Brushes.Transparent;
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
            if (ReferenceEquals(FindOwningContextMenu(e.NewFocus)?.PlacementTarget, _editTextBox))
            {
                return;
            }

            if (_isEditing && e.NewFocus != null)
            {
                EndEditCore(saveChanges: true, restoreSelection: false);
            }
        }

        private static ContextMenu? FindOwningContextMenu(IInputElement? focusedElement)
        {
            DependencyObject? current = focusedElement as DependencyObject;
            while (current != null)
            {
                if (current is ContextMenu contextMenu)
                    return contextMenu;

                if (current is MenuItem menuItem && ItemsControl.ItemsControlFromItemContainer(menuItem) is ItemsControl owner)
                {
                    current = owner;
                    continue;
                }

                current = LogicalTreeHelper.GetParent(current)
                    ?? (current is Visual visual ? VisualTreeHelper.GetParent(visual) : null);
            }

            return null;
        }

        private void OnEditorHostUnloaded(object sender, RoutedEventArgs e)
        {
            EndEdit(true);
        }

        private void OnZoomChanged(object? sender, EventArgs e)
        {
            RequestEditorTransformUpdate();
        }

        private void OnEditorLayoutUpdated(object? sender, EventArgs e)
        {
            TrackCanvasTransform();
            RequestEditorTransformUpdate();
        }

        private void OnCanvasTransformChanged(object? sender, EventArgs e)
        {
            RequestEditorTransformUpdate();
        }

        private void RequestEditorTransformUpdate()
        {
            if (_canvasTransformUpdatePending || _editTextBox == null)
            {
                return;
            }

            _canvasTransformUpdatePending = true;
            _editTextBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                _canvasTransformUpdatePending = false;
                UpdateEditorTransform();
            }), DispatcherPriority.Loaded);
        }

        private void TrackCanvasTransform()
        {
            Transform? currentTransform = _textContext?.DrawCanvas.RenderTransform;
            if (ReferenceEquals(_trackedCanvasTransform, currentTransform))
            {
                return;
            }

            if (_tracksCanvasTransformChanges && _trackedCanvasTransform != null)
            {
                if (!_trackedCanvasTransform.IsFrozen)
                {
                    _trackedCanvasTransform.Changed -= OnCanvasTransformChanged;
                }
                _tracksCanvasTransformChanges = false;
            }

            _trackedCanvasTransform = currentTransform;
            if (_trackedCanvasTransform != null && !_trackedCanvasTransform.IsFrozen)
            {
                _trackedCanvasTransform.Changed += OnCanvasTransformChanged;
                _tracksCanvasTransformChanges = true;
            }
        }

        private void OnCanvasVisualsRemove(object? sender, VisualChangedEventArgs e)
        {
            if (_isEditing && ReferenceEquals(e.Visual, this))
            {
                ActionCommand? creationCommand = _creationCommand;
                DetachEditingSession();
                Render();
                _creationCommand = null;
                _originalText = string.Empty;
                _editHost = null;
                _textContext = null;
                if (creationCommand != null && sender is DrawCanvas canvas && !canvas.IsVisualRemovalCommandInProgress(this))
                {
                    canvas.DiscardActionCommand(creationCommand);
                }
            }
        }

        private static int IndexOfVisual(DrawCanvas canvas, Visual visual)
        {
            for (int index = 0; index < canvas.Visuals.Count; index++)
            {
                if (ReferenceEquals(canvas.Visuals[index], visual))
                {
                    return index;
                }
            }

            return -1;
        }

        private void CancelNewEmptyText(TextEditingContext context, ActionCommand creationCommand)
        {
            DrawCanvas canvas = context.DrawCanvas;
            if (canvas.ContainsVisual(this))
                canvas.RemoveVisual(this);
            canvas.DiscardActionCommand(creationCommand);
        }

        private void RemoveExistingText(TextEditingContext context, string originalText, string finalText)
        {
            DrawCanvas canvas = context.DrawCanvas;
            int index = IndexOfVisual(canvas, this);
            if (index < 0)
            {
                return;
            }

            canvas.RemoveVisual(this);
            canvas.AddActionCommand(new ActionCommand(
                () =>
                {
                    Attribute.Text = originalText;
                    canvas.InsertVisual(index, this);
                },
                () =>
                {
                    Attribute.Text = finalText;
                    canvas.RemoveVisual(this);
                })
            {
                Header = ColorVision.ImageEditor.Properties.Resources.Draw_Edit,
            });
        }

        private void AddTextEditCommand(TextEditingContext context, string originalText, string finalText)
        {
            DrawCanvas canvas = context.DrawCanvas;
            canvas.AddActionCommand(new ActionCommand(
                () => Attribute.Text = originalText,
                () => Attribute.Text = finalText)
            {
                Header = ColorVision.ImageEditor.Properties.Resources.Draw_Edit,
            });
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

        private void DetachEditingSession()
        {
            if (_editHost != null)
            {
                _editHost.Unloaded -= OnEditorHostUnloaded;
            }

            DetachEditorTextBox();

            if (_textContext != null)
            {
                _textContext.Zoombox.ContentMatrixChanged -= OnZoomChanged;
                _textContext.DrawCanvas.LayoutUpdated -= OnEditorLayoutUpdated;
                _textContext.DrawCanvas.VisualsRemove -= OnCanvasVisualsRemove;
            }

            if (_tracksCanvasTransformChanges && _trackedCanvasTransform != null)
            {
                if (!_trackedCanvasTransform.IsFrozen)
                {
                    _trackedCanvasTransform.Changed -= OnCanvasTransformChanged;
                }
            }
            _trackedCanvasTransform = null;
            _tracksCanvasTransformChanges = false;
            _canvasTransformUpdatePending = false;

            _isEditing = false;
            Attribute.IsEditing = false;
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
        public void BeginEdit(TextEditingContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (_isEditing)
            {
                FocusEditor();
                return;
            }

            _textContext = context;
            _editHost = context.TextEditorOverlay;
            _originalText = Attribute.Text;
            _isEditing = true;
            _hasEditorTransform = false;
            Attribute.IsEditing = true;
            context.SelectionVisual.ClearRender();
            context.Zoombox.ContentMatrixChanged += OnZoomChanged;
            context.DrawCanvas.LayoutUpdated += OnEditorLayoutUpdated;
            context.DrawCanvas.VisualsRemove += OnCanvasVisualsRemove;
            TrackCanvasTransform();

            _editTextBox = CreateEditorTextBox();
            _editTextBox.TextChanged += OnEditorTextChanged;
            _editTextBox.PreviewKeyDown += OnEditorPreviewKeyDown;
            _editTextBox.LostKeyboardFocus += OnEditorLostKeyboardFocus;
            Canvas.SetLeft(_editTextBox, 0);
            Canvas.SetTop(_editTextBox, 0);

            _editHost.Children.Add(_editTextBox);
            _editHost.Unloaded += OnEditorHostUnloaded;
            Panel.SetZIndex(_editTextBox, 1000);

            ClearVisual();
            UpdateEditorBounds();
            UpdateEditorTransform();
            FocusEditor();
        }

        /// <summary>
        /// 结束编辑
        /// </summary>
        public void EndEdit(bool saveChanges)
        {
            EndEditCore(saveChanges, restoreSelection: true);
        }

        private void EndEditCore(bool saveChanges, bool restoreSelection)
        {
            if (!_isEditing)
            {
                return;
            }

            TextEditingContext? context = _textContext;
            ActionCommand? creationCommand = _creationCommand;
            string originalText = _originalText;
            string finalText = saveChanges && _editTextBox != null ? _editTextBox.Text : originalText;

            DetachEditingSession();

            bool textChanged = !string.Equals(originalText, finalText, StringComparison.Ordinal);
            bool modelNeedsUpdate = !string.Equals(Attribute.Text, finalText, StringComparison.Ordinal);
            if (modelNeedsUpdate)
            {
                Attribute.Text = finalText;
            }

            bool removeEmptyText = string.IsNullOrWhiteSpace(finalText) && (creationCommand != null || textChanged);
            if (removeEmptyText && context != null)
            {
                if (creationCommand != null)
                {
                    CancelNewEmptyText(context, creationCommand);
                }
                else
                {
                    RemoveExistingText(context, originalText, finalText);
                }
            }
            else
            {
                if (!modelNeedsUpdate)
                {
                    Render();
                }
                if (creationCommand == null && textChanged && context != null)
                {
                    AddTextEditCommand(context, originalText, finalText);
                }
                if (restoreSelection)
                {
                    context?.SelectionVisual.SetRender(this);
                }
            }

            _creationCommand = null;
            _originalText = string.Empty;
            _editHost = null;
            _textContext = null;
        }

        /// <summary>
        /// 处理双击事件
        /// </summary>
        public bool HandleDoubleClick(TextEditingContext context, Point point)
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
        void BeginEdit(TextEditingContext context);

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
        bool HandleDoubleClick(TextEditingContext context, Point point);
    }
}
