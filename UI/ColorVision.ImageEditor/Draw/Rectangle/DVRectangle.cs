﻿using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class DVRectangle : DrawingVisualBase<RectangleProperties>, IDrawingVisual, IRectangle
    {
        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public bool AutoAttributeChanged { get; set; } = true;

        public DVRectangle()
        {
            Attribute = new RectangleProperties();
            Attribute.Id = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 1);
            Attribute.Rect = new Rect(50, 50, 100, 100);
            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged) Render();
            };
        }
        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
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
