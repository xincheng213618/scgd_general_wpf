using System;
using System.ComponentModel;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;

namespace ColorVision.ImageEditor.Draw
{

    public class CircleTextProperties: CircleProperties,ITextProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();

        public bool IsShowText { get; set; } = true;

        [Category("Attribute"), DisplayName("Text")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value;  OnPropertyChanged(); } }

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



    public class DVCircleText : DrawingVisualBase<CircleTextProperties>, IDrawingVisual,ICircle, ILayoutScaleDrawingVisual, ICompactInspectorProvider
    {
        private static readonly Vector[] TextOutlineDirections =
        {
            new Vector(-1, 0),
            new Vector(1, 0),
            new Vector(0, -1),
            new Vector(0, 1),
            new Vector(-1, -1),
            new Vector(-1, 1),
            new Vector(1, -1),
            new Vector(1, 1)
        };
        private static readonly Brush TextOutlineBrush = CreateFrozenBrush(Color.FromArgb(224, 0, 0, 0));

        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVCircleText()
        {
            Attribute = new CircleTextProperties();
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public DVCircleText(CircleTextProperties circleTextProperties)
        {
            Attribute = circleTextProperties;
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            ApplyLayoutScaleCore(context, Pen, value => Pen = value, TextAttribute.FontSize, value => TextAttribute.FontSize = value);
        }



        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.RadiusY);

            double size = 0;
            if (Attribute.IsShowText)
            {
                FormattedText formattedText = CreateFormattedText(TextAttribute.Text, TextAttribute.Brush);
                size = formattedText.Width / 2;
                DrawOutlinedText(dc, TextAttribute.Text, new Point(Attribute.Center.X - size, Attribute.Center.Y - formattedText.Height / 2), TextAttribute.Brush);
            }

            if (!string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                FormattedText formattedText = CreateFormattedText(Attribute.Msg, TextAttribute.Brush);
                DrawOutlinedText(dc, Attribute.Msg, new Point(Attribute.Center.X + size + Radius / 2, Attribute.Center.Y - formattedText.Height / 2), TextAttribute.Brush);
            }
        }

        private void DrawOutlinedText(DrawingContext dc, string text, Point origin, Brush foreground)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            FormattedText outlineText = CreateFormattedText(text, TextOutlineBrush);
            double offset = Math.Max(1.0, TextAttribute.FontSize / 18.0);
            foreach (Vector direction in TextOutlineDirections)
            {
                dc.DrawText(outlineText, origin + direction * offset);
            }

            FormattedText mainText = CreateFormattedText(text, foreground);
            dc.DrawText(mainText, origin);
        }

        private FormattedText CreateFormattedText(string text, Brush brush)
        {
            return new FormattedText(text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        public override Rect GetRect()
        {
            return new Rect(Attribute.Center.X - Attribute.Radius, Attribute.Center.Y - Attribute.RadiusY, Attribute.Radius * 2, Attribute.RadiusY * 2);
        }
        public override void SetRect(Rect rect)
        {
            Attribute.Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            Attribute.Radius = rect.Width / 2;
            Attribute.RadiusY = rect.Height / 2;
            Render();
        }

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems(EditorContext context)
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Brush), Order = 10, EditorKind = CompactInspectorEditorKind.Brush, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Fill },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Text), Icon = CompactInspectorIcons.CreateText("T"), Order = 20, Width = 120, EditorKind = CompactInspectorEditorKind.Text, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Text },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.FontSize), Icon = CompactInspectorIcons.CreateText("A"), Width = 56, Order = 30, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_FontSize },
            };
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            SolidColorBrush brush = new(color);
            brush.Freeze();
            return brush;
        }
    }



}
