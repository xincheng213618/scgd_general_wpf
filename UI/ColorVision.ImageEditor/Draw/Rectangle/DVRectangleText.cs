using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

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
        public RectangleTextPosition Position { get; set;} = RectangleTextPosition.Center;

        [Category("Attribute"), DisplayName("Text")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontSize")]
        public double FontSize { get => TextAttribute.FontSize; set { TextAttribute.FontSize = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("Brush")]
        public Brush Foreground { get => TextAttribute.Brush; set { TextAttribute.Brush = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontFamily")]
        public FontFamily FontFamily { get => TextAttribute.FontFamily; set { TextAttribute.FontFamily = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontStyle")]
        public FontStyle FontStyle { get => TextAttribute.FontStyle; set { TextAttribute.FontStyle = value; OnPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontWeight")]
        public FontWeight FontWeight { get => TextAttribute.FontWeight; set { TextAttribute.FontWeight = value; OnPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontStretch")]
        public FontStretch FontStretch { get => TextAttribute.FontStretch; set { TextAttribute.FontStretch = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FlowDirection")]
        public FlowDirection FlowDirection { get => TextAttribute.FlowDirection; set { TextAttribute.FlowDirection = value; OnPropertyChanged(); } }
    }



    public class DVRectangleText : DrawingVisualBase<RectangleTextProperties>, IDrawingVisual,IRectangle
    {
        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVRectangleText()
        {
            Attribute = new RectangleTextProperties();
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            Attribute.PropertyChanged += (s, e) => Render(); 
        }

        public DVRectangleText(RectangleTextProperties rectangleTextProperties)
        {
            Attribute = rectangleTextProperties;
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);

            double size = 0;
            if (Attribute.IsShowText)
            {
                string textToDraw = Attribute.IsShowText ? TextAttribute.Text : string.Empty;
                if (!string.IsNullOrEmpty(textToDraw))
                {
                    FormattedText formattedText = new(
                        textToDraw,
                        CultureInfo.CurrentCulture,
                        TextAttribute.FlowDirection,
                        new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch),
                        TextAttribute.FontSize,
                        TextAttribute.Brush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
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
            if (!string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                FormattedText formattedText = new FormattedText(Attribute.Msg, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(Attribute.Rect.X + size + Attribute.Rect.Width / 2 + Attribute.Pen.Thickness, Attribute.Rect.Y + Attribute.Rect.Height / 2 - formattedText.Height / 2));
            }
        }
        public override Rect GetRect()
        {
            return Rect;
        }
        public override void SetRect(Rect rect)
        {
            Rect = rect;
            Render();
        }
    }



}
