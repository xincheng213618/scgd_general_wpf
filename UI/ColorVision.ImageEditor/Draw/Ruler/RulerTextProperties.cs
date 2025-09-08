using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class RulerTextProperties : BaseProperties
    {
        public List<Point> Points { get; set; } = new List<Point>();

        [Category("DrawingVisual"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen;

        [Category("Circle"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; OnPropertyChanged(); } }
        private Brush _Brush;


        [Category("Circle"), DisplayName("TextAttribute")]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();

        [Category("TextAttribute"), DisplayName("Text")]
        public  string Text { get => TextAttribute.Text; set { TextAttribute.Text = value; OnPropertyChanged(); } }
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
}
