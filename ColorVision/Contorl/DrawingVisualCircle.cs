#pragma warning disable CA1711,CA2211
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using ColorVision.MVVM;

namespace ColorVision
{

    public class DrawingVisualBase : DrawingVisual, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static int No = 1;
    }

    public class DrawAttributeBase : ViewModelBase
    {
        private int _ID;
        [Category("DrawingVisual"), DisplayName("序号")]
        public int ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }

        private bool _IsShow = true;
        [Category("DrawingVisual"), DisplayName("是否显示")]
        public bool IsShow { get => _IsShow; set { _IsShow = value; NotifyPropertyChanged(); } }

    }



    public interface IDrawingVisual
    {
        public abstract DrawAttributeBase GetAttribute();
    }


    public class CircleAttribute : DrawAttributeBase
    {
        private Brush _Brush;

        [Category("DrawingVisualCircle"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }

        private Pen _Pen;

        [Category("DrawingVisualCircle"), DisplayName("笔刷"), BrowsableAttribute(false)]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }

        private Point _Center;

        [Category("DrawingVisualCircle"), DisplayName("点")]
        public Point Center { get => _Center; set { if (_Center.Equals(value)) return;  _Center = value; NotifyPropertyChanged(); } }

        private double _Radius;

        [Category("DrawingVisualCircle"), DisplayName("半径")]
        public double Radius { get => _Radius; set { _Radius = value; NotifyPropertyChanged(); } }
    }

    public class RectangleAttribute : DrawAttributeBase
    {
        private Brush _Brush;

        [Category("RectangleAttribute"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }

        private Pen _Pen;
        [Category("DrawingVisualCircle"), DisplayName("笔刷"), BrowsableAttribute(false)]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }

        private Rect _Rect;

        [Category("RectangleAttribute"), DisplayName("矩形")]
        public Rect Rect { get => _Rect; set { _Rect = value; NotifyPropertyChanged(); } }
    }



    public class DrawingVisualCircle : DrawingVisualBase,IDrawingVisual
    {
        public CircleAttribute Attribute { get; set; }
        public DrawAttributeBase GetAttribute() => Attribute;

        public bool AutoAttributeChanged { get; set; } = true;

        public DrawingVisualCircle()
        {
            Attribute = new CircleAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;
            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged)
                    Render();
                if (e.PropertyName == "Center")
                {
                    NotifyPropertyChanged(nameof(CenterX));
                    NotifyPropertyChanged(nameof(CenterY));
                }
                else if (e.PropertyName == "Radius")
                {
                    NotifyPropertyChanged(nameof(Radius));
                }
            };
        }

        public Point Center { get => Attribute.Center; set => Attribute.Center =value; }

        public double CenterX { get => Attribute.Center.X; set => Attribute.Center = new Point(value, Attribute.Center.Y); }
        public double CenterY { get => Attribute.Center.Y; set => Attribute.Center = new Point(Attribute.Center.X, value); }

        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }

        public int ID { get => Attribute.ID; set => Attribute.ID = value; }



        public virtual void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }


    }

    public class DrawingVisualCircleWord: DrawingVisualCircle
    {
        public override void Render()
        {
            Brush brush = Brushes.Red;
            FontFamily fontFamily = new FontFamily("Arial");
            double fontSize = Attribute.Pen.Thickness * 10;
            using DrawingContext dc = RenderOpen();
            FormattedText formattedText = new FormattedText("Point_"+ID.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(formattedText,new Point(Attribute.Center.X - Attribute.Radius, Attribute.Center.Y - fontSize));
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }
    }



    public class PolygonAttribute : DrawAttributeBase
    {
        private Brush _Brush;

        [Category("RectangleAttribute"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }

        private Pen _Pen;

        [Category("RectangleAttribute"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }

        private List<Point> _Points;
        public  List<Point> Points { get => _Points; set { _Points = value; NotifyPropertyChanged(); } }
    }



    public class DrawingVisualPolygon: DrawingVisualBase, IDrawingVisual
    {
        public PolygonAttribute Attribute { get; set; }

        public DrawAttributeBase GetAttribute() => Attribute;

        public bool AutoAttributeChanged { get; set; } = true;
        
        public bool IsDrawing { get; set; } = true;

        public DrawingVisualPolygon()
        {
            Attribute = new PolygonAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Points = new List<Point>();
            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged)
                    Render();
            };
        }
        public int ID { get => Attribute.ID; set => Attribute.ID = value; }

        public void Render()
        {
            using DrawingContext dc = RenderOpen();

            if (Attribute.Points.Count > 1)
            {
                for (int i = 0; i < Attribute.Points.Count-1; i++)
                {
                    dc.DrawLine(Attribute.Pen, Attribute.Points[i], Attribute.Points[i+1]);
                }
                if (!IsDrawing)
                    dc.DrawLine(Attribute.Pen, Attribute.Points[Attribute.Points.Count-1], Attribute.Points[0]);
            }
        }


    }





    public class DrawingVisualRectangle : DrawingVisualBase, IDrawingVisual
    {
        public RectangleAttribute Attribute { get; set; }
        public DrawAttributeBase GetAttribute() => Attribute;


        public bool AutoAttributeChanged { get; set; } = true;

        public DrawingVisualRectangle()
        {
            Attribute = new RectangleAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 1);
            Attribute.Rect = new Rect(50, 50, 100, 100);
            Attribute.PropertyChanged += (s, e) => 
            {
                if (AutoAttributeChanged) Render();
            };
        }
        public int ID { get => Attribute.ID; set => Attribute.ID = value; }



        public void Render()
        {
            using DrawingContext dc = RenderOpen();


            //RotateTransform form = new RotateTransform(50, Attribute.Rect.Left, Attribute.Rect.Top);
            //dc.PushTransform(form);
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }



    }



}
