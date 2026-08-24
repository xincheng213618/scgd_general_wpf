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
        private bool _deferAttributeRender;

        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVCircleText()
        {
            Attribute = new CircleTextProperties();
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }

        public DVCircleText(CircleTextProperties circleTextProperties)
        {
            Attribute = circleTextProperties;
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }

        private void OnAttributePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(CircleTextProperties.Pen))
                LayoutBasePenThickness = null;
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(CircleTextProperties.FontSize))
                LayoutBaseFontSize = null;

            if (!_deferAttributeRender)
                Render();
        }

        internal void SetRadiusAndRender(double radius)
        {
            bool wasDeferred = _deferAttributeRender;
            _deferAttributeRender = true;
            try
            {
                Attribute.Radius = radius;
            }
            finally
            {
                _deferAttributeRender = wasDeferred;
            }

            if (!wasDeferred)
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
            if (!ShapeGeometry.TryGetEllipseBounds(Attribute.Center, Attribute.Radius, Attribute.RadiusY, out Rect bounds))
                return;

            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, bounds.Width / 2, bounds.Height / 2);

            double size = 0;
            if (Attribute.IsShowText)
            {
                FormattedText formattedText = CreateFormattedText(TextAttribute.Text, TextAttribute.Brush);
                size = formattedText.Width / 2;
                if (!string.IsNullOrWhiteSpace(TextAttribute.Text))
                {
                    dc.DrawText(formattedText, new Point(Attribute.Center.X - size, Attribute.Center.Y - formattedText.Height / 2));
                }
            }

            if (IsMessageVisible && !string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                FormattedText formattedText = CreateFormattedText(Attribute.Msg, TextAttribute.Brush);
                dc.DrawText(formattedText, new Point(Attribute.Center.X + size + bounds.Width / 4, Attribute.Center.Y - formattedText.Height / 2));
            }
        }

        private FormattedText CreateFormattedText(string text, Brush brush)
        {
            return new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                TextRenderCore.NormalizeFlowDirection(TextAttribute.FlowDirection),
                new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch),
                TextRenderCore.NormalizeFontSize(TextAttribute.FontSize),
                brush,
                TextRenderCore.NormalizePixelsPerDip(VisualTreeHelper.GetDpi(this).PixelsPerDip));
        }

        public override Rect GetRect()
        {
            return ShapeGeometry.TryGetEllipseBounds(Attribute.Center, Attribute.Radius, Attribute.RadiusY, out Rect bounds)
                ? bounds
                : Rect.Empty;
        }
        public override void SetRect(Rect rect)
        {
            if (!ShapeGeometry.IsFinite(rect))
                return;

            bool wasDeferred = _deferAttributeRender;
            _deferAttributeRender = true;
            try
            {
                Attribute.Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
                Attribute.Radius = rect.Width / 2;
                Attribute.RadiusY = rect.Height / 2;
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
