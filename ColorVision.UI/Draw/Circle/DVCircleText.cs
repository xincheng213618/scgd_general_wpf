﻿using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Draw
{
    public class CircleTextProperties: CircleProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();

        [Category("Attribute"), DisplayName("Text")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value;  NotifyPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontSize")]
        public double FontSize { get => TextAttribute.FontSize; set { TextAttribute.FontSize = value; NotifyPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("Brush")]
        public Brush Foreground { get => TextAttribute.Brush; set { TextAttribute.Brush = value; NotifyPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontFamily")]
        public FontFamily FontFamily { get => TextAttribute.FontFamily; set { TextAttribute.FontFamily = value; NotifyPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontStyle")]
        public FontStyle FontStyle { get => TextAttribute.FontStyle; set { TextAttribute.FontStyle = value; NotifyPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontWeight")]
        public FontWeight FontWeight { get => TextAttribute.FontWeight; set { TextAttribute.FontWeight = value; NotifyPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontStretch")]
        public FontStretch FontStretch { get => TextAttribute.FontStretch; set { TextAttribute.FontStretch = value; NotifyPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FlowDirection")]
        public FlowDirection FlowDirection { get => TextAttribute.FlowDirection; set { TextAttribute.FlowDirection = value; NotifyPropertyChanged(); } }
    }



    public class DVCircleText : DrawingVisualBase<CircleTextProperties>, IDrawingVisual,ICircle
    {
        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }
        public bool AutoAttributeChanged { get; set; } = true;

        public BaseProperties BaseAttribute => Attribute;
        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVCircleText()
        {
            Attribute = new CircleTextProperties();
            Attribute.Id = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;
            Attribute.PropertyChanged += (s, e) =>
            { 
                if (AutoAttributeChanged && e.PropertyName != "Id")
                {
                    Render();
                }
            };
        }



        public override void Render()
        {
            TextAttribute.Text = Attribute.Text;
            TextAttribute.Text =  string.IsNullOrWhiteSpace(TextAttribute.Text)? Attribute.Id.ToString(): TextAttribute.Text;
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            using DrawingContext dc = RenderOpen();

            FormattedText formattedText = new(TextAttribute.Text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(formattedText, new Point(Attribute.Center.X - Attribute.Radius, Attribute.Center.Y - TextAttribute.FontSize));
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }
    }



}