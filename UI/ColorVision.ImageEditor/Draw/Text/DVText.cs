using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class TextProperties : BaseProperties, ITextProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();

        public bool IsShowText { get; set; } = true;

        [Category("Text"), DisplayName("文本")] 
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("字体大小")] 
        public double FontSize { get => TextAttribute.FontSize; set { TextAttribute.FontSize = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("颜色")] 
        public Brush Foreground { get => TextAttribute.Brush; set { TextAttribute.Brush = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("字体")] 
        public FontFamily FontFamily { get => TextAttribute.FontFamily; set { TextAttribute.FontFamily = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("FontStyle")] 
        public FontStyle FontStyle { get => TextAttribute.FontStyle; set { TextAttribute.FontStyle = value; OnPropertyChanged(); } }
        [Category("Text"), DisplayName("FontWeight")] 
        public FontWeight FontWeight { get => TextAttribute.FontWeight; set { TextAttribute.FontWeight = value; OnPropertyChanged(); } }
        [Category("Text"), DisplayName("FontStretch")] 
        public FontStretch FontStretch { get => TextAttribute.FontStretch; set { TextAttribute.FontStretch = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("FlowDirection")] 
        public FlowDirection FlowDirection { get => TextAttribute.FlowDirection; set { TextAttribute.FlowDirection = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("位置")] 
        public Point Position { get => _Position; set { if (_Position == value) return; _Position = value; OnPropertyChanged(); } }
        private Point _Position = new Point(50,50);

        [Browsable(false)]
        public Rect Rect { get => _Rect; set { _Rect = value; OnPropertyChanged(); } }
        private Rect _Rect = new Rect(50,50,0,0);

        [Browsable(false)]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen = new Pen(Brushes.Red,1);

        [Category("Text"), DisplayName("背景")] 
        public Brush Background { get => _Background; set { _Background = value; OnPropertyChanged(); } }
        private Brush _Background = Brushes.Transparent;
    }

    public class DVText : DrawingVisualBase<TextProperties>, IDrawingVisual
    {
        public TextAttribute TextAttribute => Attribute.TextAttribute;

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVText()
        {
            Attribute = new TextProperties();
            Attribute.Text = "请在这里输入";
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10; // 与其它图元保持一致缩放策略
            Attribute.PropertyChanged += (s,e)=> Render();
        }
        public DVText(TextProperties textProperties)
        {
            Attribute = textProperties;
            if (Attribute.FontSize <= 0)
                TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            //Attribute.PropertyChanged += (s,e)=> Render();
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            if (!Attribute.IsShowText || string.IsNullOrEmpty(TextAttribute.Text))
            {
                Attribute.Rect = new Rect(Attribute.Position.X, Attribute.Position.Y, 0, 0);
                return;
            }
            FormattedText formattedText = new(
                TextAttribute.Text,
                CultureInfo.CurrentCulture,
                TextAttribute.FlowDirection,
                new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch),
                TextAttribute.FontSize,
                TextAttribute.Brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // 画背景
            if (Attribute.Background != null && Attribute.Background != Brushes.Transparent)
            {
                dc.DrawRectangle(Attribute.Background, null, new Rect(Attribute.Position, new Size(formattedText.Width, formattedText.Height)));
            }
            // 文本
            dc.DrawText(formattedText, Attribute.Position);

            // Msg 追加显示在文本右侧
            if (!string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                FormattedText formattedMsg = new(
                    Attribute.Msg,
                    CultureInfo.CurrentCulture,
                    TextAttribute.FlowDirection,
                    new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch),
                    TextAttribute.FontSize,
                    TextAttribute.Brush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedMsg, new Point(Attribute.Position.X + formattedText.Width + Attribute.Pen.Thickness, Attribute.Position.Y));
                Attribute.Rect = new Rect(Attribute.Position.X, Attribute.Position.Y, formattedText.Width + formattedMsg.Width + Attribute.Pen.Thickness, System.Math.Max(formattedText.Height, formattedMsg.Height));
            }
            else
            {
                Attribute.Rect = new Rect(Attribute.Position.X, Attribute.Position.Y, formattedText.Width, formattedText.Height);
            }
        }

        public override Rect GetRect() => Attribute.Rect;

        public override void SetRect(Rect rect)
        {
            // 移动
            Attribute.Position = new Point(rect.X, rect.Y);
            // 根据高度调整字体，保持与矩形/圆拖拽类似体验
            if (rect.Height > 0)
            {
                TextAttribute.FontSize = rect.Height; // 直接映射高度
            }
            Render();
        }
    }
}
