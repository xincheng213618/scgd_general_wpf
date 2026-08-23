using System.ComponentModel;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;

namespace ColorVision.ImageEditor.Draw
{
    public enum RectangleTextPosition
    {
        Center,
        Top,
        Bottom,
        Left,
        Right
    }

    public class RectangleTextProperties : RectangleProperties, ITextProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();
        public bool IsShowText { get; set; } = true;
        public RectangleTextPosition Position
        {
            get => _position;
            set
            {
                if (_position == value)
                    return;

                _position = value;
                OnPropertyChanged();
            }
        }
        private RectangleTextPosition _position = RectangleTextPosition.Center;

        [Category("Attribute"), DisplayName("Text")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontSize")]
        public double FontSize { get => TextAttribute.FontSize; set { TextAttribute.FontSize = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("Brush"), JsonIgnore]
        public Brush Foreground { get => TextAttribute.Brush; set { TextAttribute.Brush = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontFamily"), JsonIgnore]
        public FontFamily FontFamily { get => TextAttribute.FontFamily; set { TextAttribute.FontFamily = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontStyle"), JsonIgnore]
        public FontStyle FontStyle { get => TextAttribute.FontStyle; set { TextAttribute.FontStyle = value; OnPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontWeight"), JsonIgnore]
        public FontWeight FontWeight { get => TextAttribute.FontWeight; set { TextAttribute.FontWeight = value; OnPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontStretch"), JsonIgnore]
        public FontStretch FontStretch { get => TextAttribute.FontStretch; set { TextAttribute.FontStretch = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FlowDirection"), JsonIgnore]
        public FlowDirection FlowDirection { get => TextAttribute.FlowDirection; set { TextAttribute.FlowDirection = value; OnPropertyChanged(); } }
    }



    public class DVRectangleText : DrawingVisualBase<RectangleTextProperties>, IDrawingVisual,IRectangle, ILayoutScaleDrawingVisual, ICompactInspectorProvider
    {
        private bool _deferAttributeRender;

        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVRectangleText()
        {
            Attribute = new RectangleTextProperties();
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }

        public DVRectangleText(RectangleTextProperties rectangleTextProperties)
        {
            Attribute = rectangleTextProperties;
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }

        private void OnAttributePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(RectangleTextProperties.Pen))
                LayoutBasePenThickness = null;
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(RectangleTextProperties.FontSize))
                LayoutBaseFontSize = null;

            if (!_deferAttributeRender)
                Render();
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            bool wasDeferred = _deferAttributeRender;
            _deferAttributeRender = true;
            try
            {
                ApplyLayoutScaleCore(context, Pen, value => Pen = value, TextAttribute.FontSize, value => TextAttribute.FontSize = value);
            }
            finally
            {
                _deferAttributeRender = wasDeferred;
            }
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            if (Attribute.Rect.IsEmpty || !ShapeGeometry.IsFinite(Attribute.Rect))
                return;

            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);

            double size = 0;
            if (Attribute.IsShowText)
            {
                string textToDraw = Attribute.IsShowText ? TextAttribute.Text : string.Empty;
                if (!string.IsNullOrEmpty(textToDraw))
                {
                    FormattedText formattedText = CreateFormattedText(textToDraw);
                    size = formattedText.Width / 2;
                    Point origin = new Point();
                    double halfWidth = formattedText.Width / 2;
                    double halfHeight = formattedText.Height / 2;
                    double rectHalfWidth = Attribute.Rect.Width / 2;
                    double rectHalfHeight = Attribute.Rect.Height / 2;
                    double rectCenterX = Attribute.Rect.X + rectHalfWidth;
                    double rectCenterY = Attribute.Rect.Y + rectHalfHeight;

                    // Calculate position based on the enum
                    switch (Attribute.Position) // Assuming Attribute has a 'Position' property of type RectangleTextPosition
                    {
                        case RectangleTextPosition.Center:
                        default:
                            origin.X = rectCenterX - halfWidth;
                            origin.Y = rectCenterY - halfHeight;
                            break;
                        case RectangleTextPosition.Top:
                            origin.X = rectCenterX - halfWidth;
                            origin.Y = Attribute.Rect.Y - formattedText.Height; // Above the rect
                                                                                // Or inside top: origin.Y = Attribute.Rect.Y; 
                            break;
                        case RectangleTextPosition.Bottom:
                            origin.X = rectCenterX - halfWidth;
                            origin.Y = Attribute.Rect.Bottom; // Below the rect
                                                              // Or inside bottom: origin.Y = Attribute.Rect.Bottom - formattedText.Height;
                            break;
                        case RectangleTextPosition.Left:
                            origin.X = Attribute.Rect.X - formattedText.Width; // Left of rect
                            origin.Y = rectCenterY - halfHeight;
                            break;
                        case RectangleTextPosition.Right:
                            origin.X = Attribute.Rect.Right; // Right of rect
                            origin.Y = rectCenterY - halfHeight;
                            break;
                    }

                    dc.DrawText(formattedText, origin);
                }
            }
            if (IsMessageVisible && !string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                FormattedText formattedText = CreateFormattedText(Attribute.Msg);
                dc.DrawText(formattedText, new Point(Attribute.Rect.X + size + Attribute.Rect.Width / 2 + Attribute.Pen.Thickness, Attribute.Rect.Y + Attribute.Rect.Height / 2 - formattedText.Height / 2));
            }
        }

        private FormattedText CreateFormattedText(string text)
        {
            return new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                TextRenderCore.NormalizeFlowDirection(TextAttribute.FlowDirection),
                new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch),
                TextRenderCore.NormalizeFontSize(TextAttribute.FontSize),
                TextAttribute.Brush,
                TextRenderCore.NormalizePixelsPerDip(VisualTreeHelper.GetDpi(this).PixelsPerDip));
        }

        public override Rect GetRect()
        {
            return Rect.IsEmpty || ShapeGeometry.IsFinite(Rect) ? Rect : System.Windows.Rect.Empty;
        }
        public override void SetRect(Rect rect)
        {
            if (!rect.IsEmpty && !ShapeGeometry.IsFinite(rect))
                return;

            bool wasDeferred = _deferAttributeRender;
            _deferAttributeRender = true;
            try
            {
                Rect = rect;
            }
            finally
            {
                _deferAttributeRender = wasDeferred;
            }

            if (!wasDeferred)
                Render();
        }

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems()
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Brush), Order = 10, EditorKind = CompactInspectorEditorKind.Brush, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Fill },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Text), Icon = CompactInspectorIcons.CreateText("T"), Order = 20, Width = 120, EditorKind = CompactInspectorEditorKind.Text, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Text },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.FontSize), Icon = CompactInspectorIcons.CreateText("A"), Width = 56, Order = 30, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_FontSize },
            };
        }
    }



}
