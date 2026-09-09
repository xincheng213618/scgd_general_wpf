#pragma warning disable CA1859
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Conoscope.Core
{
    public enum ConoscopeCoordinateReferenceMode
    {
        [Display(Name = "Con_Axis_AzimuthLine", ResourceType = typeof(Properties.Resources))]
        AzimuthLine = 0,

        [Display(Name = "Con_Axis_PolarCircle", ResourceType = typeof(Properties.Resources))]
        PolarCircle = 1
    }

    public class ConoscopeCoordinateReferenceChangedEventArgs : EventArgs
    {
        public ConoscopeCoordinateReferenceChangedEventArgs(ConoscopeCoordinateReferenceMode mode, double angle, double radiusAngle, Point position, bool isFinal, bool isValueChanged)
        {
            Mode = mode;
            Angle = angle;
            RadiusAngle = radiusAngle;
            Position = position;
            IsFinal = isFinal;
            IsValueChanged = isValueChanged;
        }

        public ConoscopeCoordinateReferenceMode Mode { get; }
        public double Angle { get; }
        public double RadiusAngle { get; }
        public Point Position { get; }
        public bool IsFinal { get; }
        public bool IsValueChanged { get; }
    }

    public class ConoscopeCoordinateAxisParam : ViewModelBase
    {
        [Browsable(false), JsonIgnore]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen = new Pen(Brushes.Yellow, 1.0);

        [Display(Name = "Con_Axis_EnableInteract", GroupName = "Con_Category_CoordAxis", ResourceType = typeof(Properties.Resources))]
        public bool IsInteractionEnabled { get => _IsInteractionEnabled; set { _IsInteractionEnabled = value; OnPropertyChanged(); } }
        private bool _IsInteractionEnabled = true;

        [Display(Name = "Con_Axis_MaxAngle", GroupName = "Con_Category_CoordAxis", ResourceType = typeof(Properties.Resources))]
        public double MaxAngle { get => _MaxAngle; set { _MaxAngle = value; OnPropertyChanged(); } }
        private double _MaxAngle = 60;

        [Browsable(false), JsonIgnore]
        public double ConoscopeCoefficient { get => _ConoscopeCoefficient; set { _ConoscopeCoefficient = value; OnPropertyChanged(); } }
        private double _ConoscopeCoefficient;

        [Browsable(false), JsonIgnore]
        public ConoscopeCoordinateSystem CoordinateSystem { get => _CoordinateSystem; set { _CoordinateSystem = value; OnPropertyChanged(); } }
        private ConoscopeCoordinateSystem _CoordinateSystem;

        [Display(Name = "Con_Axis_CenterX", GroupName = "Con_Category_CoordAxis", ResourceType = typeof(Properties.Resources))]
        public double CenterX { get => _CenterX; set { _CenterX = value; OnPropertyChanged(); } }
        private double _CenterX;

        [Display(Name = "Con_Axis_CenterY", GroupName = "Con_Category_CoordAxis", ResourceType = typeof(Properties.Resources))]
        public double CenterY { get => _CenterY; set { _CenterY = value; OnPropertyChanged(); } }
        private double _CenterY;

        [Display(Name = "Con_Axis_Radius", GroupName = "Con_Category_CoordAxis", ResourceType = typeof(Properties.Resources))]
        public double AxisRadius { get => _AxisRadius; set { _AxisRadius = value; OnPropertyChanged(); } }
        private double _AxisRadius;

        [Display(Name = "Con_Axis_AzimuthStep", GroupName = "Con_Category_CoordAxis", ResourceType = typeof(Properties.Resources))]
        public double AzimuthStep { get => _AzimuthStep; set { _AzimuthStep = Math.Max(1, value); OnPropertyChanged(); } }
        private double _AzimuthStep = 30;

        [Display(Name = "Con_Axis_PolarStep", GroupName = "Con_Category_CoordAxis", ResourceType = typeof(Properties.Resources))]
        public double PolarStep { get => _PolarStep; set { _PolarStep = Math.Max(1, value); OnPropertyChanged(); } }
        private double _PolarStep = 10;

        [Display(Name = "Con_Axis_LineWidth", GroupName = "Con_Category_CoordAxis", ResourceType = typeof(Properties.Resources))]
        public double LineWidth { get => _LineWidth; set { _LineWidth = Math.Max(0.1, value); OnPropertyChanged(); } }
        private double _LineWidth = 1.0;

        [Display(Name = "Con_Axis_Color", GroupName = "Con_Category_CoordAxis", ResourceType = typeof(Properties.Resources)), JsonIgnore]
        public Brush AxisBrush { get => _AxisBrush; set { _AxisBrush = value; OnPropertyChanged(); if (Pen != null) Pen.Brush = value; } }
        private Brush _AxisBrush = Brushes.Yellow;

        [Display(Name = "Con_Axis_RefMode", GroupName = "Con_Category_RefLine", ResourceType = typeof(Properties.Resources))]
        public ConoscopeCoordinateReferenceMode ReferenceMode { get => _ReferenceMode; set { _ReferenceMode = value; OnPropertyChanged(); } }
        private ConoscopeCoordinateReferenceMode _ReferenceMode = ConoscopeCoordinateReferenceMode.AzimuthLine;

        [Display(Name = "Con_Axis_RefAzimuth", GroupName = "Con_Category_RefLine", ResourceType = typeof(Properties.Resources))]
        public double ReferenceAngle { get => _ReferenceAngle; set { _ReferenceAngle = NormalizeAzimuthAngle(value); OnPropertyChanged(); } }
        private double _ReferenceAngle = 90;

        [Display(Name = "Con_Axis_RefPolar", GroupName = "Con_Category_RefLine", ResourceType = typeof(Properties.Resources))]
        public double ReferenceRadiusAngle { get => _ReferenceRadiusAngle; set { _ReferenceRadiusAngle = value; OnPropertyChanged(); } }
        private double _ReferenceRadiusAngle = 30;

        [Display(Name = "Con_Axis_RefLineWidth", GroupName = "Con_Category_RefLine", ResourceType = typeof(Properties.Resources))]
        public double ReferenceLineWidth { get => _ReferenceLineWidth; set { _ReferenceLineWidth = Math.Max(0.1, value); OnPropertyChanged(); } }
        private double _ReferenceLineWidth = 2.0;

        [Display(Name = "Con_Axis_RefColor", GroupName = "Con_Category_RefLine", ResourceType = typeof(Properties.Resources)), JsonIgnore]
        public Brush ReferenceBrush { get => _ReferenceBrush; set { _ReferenceBrush = value; OnPropertyChanged(); } }
        private Brush _ReferenceBrush = Brushes.Red;

        [Display(Name = "Con_Axis_ShowMask", GroupName = "Con_Category_Mask", ResourceType = typeof(Properties.Resources))]
        public bool IsMaskVisible { get => _IsMaskVisible; set { _IsMaskVisible = value; OnPropertyChanged(); } }
        private bool _IsMaskVisible = true;

        [Display(Name = "Con_Axis_MaskOpacity", GroupName = "Con_Category_Mask", ResourceType = typeof(Properties.Resources))]
        public byte MaskOpacity { get => _MaskOpacity; set { _MaskOpacity = value; OnPropertyChanged(); } }
        private byte _MaskOpacity = 255;

        [Display(Name = "Con_Axis_MaskColor", GroupName = "Con_Category_Mask", ResourceType = typeof(Properties.Resources))]
        public Color MaskColor { get => _MaskColor; set { _MaskColor = value; OnPropertyChanged(); } }
        private Color _MaskColor = Color.FromRgb(0, 0, 0);

        [Display(Name = "Con_Axis_ShowText", GroupName = "Con_Category_Text", ResourceType = typeof(Properties.Resources))]
        public bool IsTextVisible { get => _IsTextVisible; set { _IsTextVisible = value; OnPropertyChanged(); } }
        private bool _IsTextVisible = true;

        [Display(Name = "Con_Axis_TextSize", GroupName = "Con_Category_Text", ResourceType = typeof(Properties.Resources))]
        public double FontSize { get => _FontSize; set { _FontSize = Math.Max(1, value); OnPropertyChanged(); } }
        private double _FontSize = 24;

        [Display(Name = "Con_Axis_TextColor", GroupName = "Con_Category_Text", ResourceType = typeof(Properties.Resources)), JsonIgnore]
        public Brush TextBrush { get => _TextBrush; set { _TextBrush = value; OnPropertyChanged(); } }
        private Brush _TextBrush = Brushes.Yellow;

        public static double NormalizeAzimuthAngle(double angle)
        {
            angle %= 180.0;
            if (angle < 0)
            {
                angle += 180.0;
            }

            return angle;
        }
    }

    public class ConoscopeCoordinateAxisVisual : DrawingVisualBase, IDrawingVisual, IDisposable
    {
        private const double HitTolerance = 14;
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
        private readonly SolidColorBrush clearBrush = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255));
        public ConoscopeCoordinateAxisParam Attribute { get; set; }

        public ConoscopeCoordinateAxisVisual(ConoscopeCoordinateAxisParam param)
        {
            Attribute = param;
            Attribute.Pen = new Pen(Attribute.AxisBrush, Attribute.LineWidth);
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }

        private void Attribute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Render();
        }

        public void Dispose()
        {
            Attribute.PropertyChanged -= Attribute_PropertyChanged;
            GC.SuppressFinalize(this);
        }

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public double Ratio { get; set; } = 1;
        public double ActualWidth { get; set; }
        public double ActualHeight { get; set; }

        public Point Center => new Point(Attribute.CenterX, Attribute.CenterY);
        public double AxisRadius => Math.Max(0, Attribute.AxisRadius);

        public void Configure(double actualWidth, double actualHeight, Point center, double axisRadius, double maxAngle, double coefficient, double ratio)
        {
            ActualWidth = actualWidth;
            ActualHeight = actualHeight;
            Ratio = ratio <= double.Epsilon ? 1 : ratio;
            Attribute.CenterX = center.X;
            Attribute.CenterY = center.Y;
            Attribute.AxisRadius = axisRadius;
            Attribute.MaxAngle = maxAngle;
            Attribute.ConoscopeCoefficient = coefficient;
            Attribute.ReferenceRadiusAngle = Clamp(Attribute.ReferenceRadiusAngle, 0, maxAngle);
            Render();
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawRectangle(clearBrush, new Pen(Brushes.Transparent, 0), new Rect(0, 0, ActualWidth, ActualHeight));

            if (ActualWidth <= 0 || ActualHeight <= 0 || AxisRadius <= 0)
            {
                return;
            }

            double ratio = Ratio <= double.Epsilon ? 1 : Ratio;
            Pen axisPen = new Pen(Attribute.AxisBrush, Attribute.LineWidth / ratio);
            Pen referencePen = new Pen(Attribute.ReferenceBrush, Attribute.ReferenceLineWidth / ratio);
            Point center = Center;

            if (ConoscopeHorizontalVerticalProjection.IsProjectedCoordinateSystem(Attribute.CoordinateSystem))
            {
                StreamGeometry domain = CreateHorizontalVerticalDomainGeometry(center);
                DrawMask(dc, domain);
                dc.PushClip(domain);
                DrawHorizontalVerticalGrid(dc, center, axisPen);
                DrawHorizontalVerticalReference(dc, center, referencePen);
                dc.Pop();
                DrawHorizontalVerticalLabels(dc, center);
            }
            else
            {
                DrawMask(dc, new EllipseGeometry(center, AxisRadius, AxisRadius));
                DrawConcentricCircles(dc, center, axisPen);
                DrawAzimuthLines(dc, center, axisPen);
                DrawReference(dc, center, referencePen);
            }
        }

        public bool ContainsInteractivePoint(Point point)
        {
            double tolerance = HitTolerance / Math.Max(Ratio, 1);
            if (ConoscopeHorizontalVerticalProjection.IsProjectedCoordinateSystem(Attribute.CoordinateSystem))
            {
                return TryGetHorizontalVerticalAngles(point, tolerance, out double horizontalAngle, out double verticalAngle)
                    && ConoscopeHorizontalVerticalProjection.TryConvertHorizontalVerticalToPolar(
                        Attribute.CoordinateSystem,
                        horizontalAngle,
                        verticalAngle,
                        Attribute.MaxAngle,
                        out _,
                        out _);
            }

            return (point - Center).Length <= AxisRadius + tolerance;
        }

        public bool UpdateReferenceFromPoint(Point point)
        {
            if (!ContainsInteractivePoint(point))
            {
                return false;
            }

            double angle;
            double radiusAngle;
            if (ConoscopeHorizontalVerticalProjection.IsProjectedCoordinateSystem(Attribute.CoordinateSystem))
            {
                if (!TryGetHorizontalVerticalAngles(point, 0, out double horizontalAngle, out double verticalAngle)
                    || !ConoscopeHorizontalVerticalProjection.TryConvertHorizontalVerticalToPolar(
                        Attribute.CoordinateSystem,
                        horizontalAngle,
                        verticalAngle,
                        Attribute.MaxAngle,
                        out radiusAngle,
                        out angle))
                {
                    return false;
                }
            }
            else
            {
                angle = GetAzimuthAngle(Center, point);
                radiusAngle = Clamp((point - Center).Length / AxisRadius * Attribute.MaxAngle, 0, Attribute.MaxAngle);
            }

            if (Attribute.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                angle = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(angle);
                if (Math.Abs(Attribute.ReferenceAngle - angle) < 0.05)
                {
                    return false;
                }

                Attribute.ReferenceAngle = angle;
                return true;
            }

            if (Math.Abs(Attribute.ReferenceRadiusAngle - radiusAngle) < 0.05)
            {
                return false;
            }

            Attribute.ReferenceRadiusAngle = radiusAngle;
            return true;
        }

        public override Rect GetRect()
        {
            return new Rect(0, 0, ActualWidth, ActualHeight);
        }

        public static (Point Start, Point End) GetAzimuthLineEndpoints(Point center, double radius, double angle)
        {
            Point positive = GetPointOnAxis(center, radius, angle);
            Point negative = new Point(center.X - (positive.X - center.X), center.Y - (positive.Y - center.Y));
            return (positive, negative);
        }

        private void DrawMask(DrawingContext dc, Geometry innerGeometry)
        {
            if (!Attribute.IsMaskVisible)
            {
                return;
            }

            RectangleGeometry outerRect = new RectangleGeometry(new Rect(-5, -5, ActualWidth + 10, ActualHeight + 10));
            GeometryGroup maskGeometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
            maskGeometry.Children.Add(outerRect);
            maskGeometry.Children.Add(innerGeometry);

            SolidColorBrush maskBrush = new SolidColorBrush(Color.FromArgb(Attribute.MaskOpacity, Attribute.MaskColor.R, Attribute.MaskColor.G, Attribute.MaskColor.B));
            dc.DrawGeometry(maskBrush, null, maskGeometry);
        }

        private StreamGeometry CreateHorizontalVerticalDomainGeometry(Point center)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                const int sampleCount = 360;
                for (int index = 0; index <= sampleCount; index++)
                {
                    double azimuthAngle = index * 360.0 / sampleCount;
                    Point point = GetHorizontalVerticalPoint(center, Attribute.MaxAngle, azimuthAngle);
                    if (index == 0)
                    {
                        context.BeginFigure(point, isFilled: true, isClosed: true);
                    }
                    else
                    {
                        context.LineTo(point, isStroked: true, isSmoothJoin: true);
                    }
                }
            }

            geometry.Freeze();
            return geometry;
        }

        private void DrawHorizontalVerticalGrid(DrawingContext dc, Point center, Pen axisPen)
        {
            double step = Math.Max(1, Attribute.PolarStep);
            for (double angle = -Attribute.MaxAngle; angle <= Attribute.MaxAngle + 0.001; angle += step)
            {
                double offset = AxisRadius * angle / Attribute.MaxAngle;
                dc.DrawLine(axisPen, new Point(center.X + offset, center.Y - AxisRadius), new Point(center.X + offset, center.Y + AxisRadius));
                dc.DrawLine(axisPen, new Point(center.X - AxisRadius, center.Y - offset), new Point(center.X + AxisRadius, center.Y - offset));
            }
        }

        private void DrawHorizontalVerticalReference(DrawingContext dc, Point center, Pen referencePen)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                if (Attribute.ReferenceMode == ConoscopeCoordinateReferenceMode.PolarCircle)
                {
                    const int sampleCount = 360;
                    for (int index = 0; index <= sampleCount; index++)
                    {
                        Point point = GetHorizontalVerticalPoint(center, Attribute.ReferenceRadiusAngle, index * 360.0 / sampleCount);
                        if (index == 0)
                        {
                            context.BeginFigure(point, isFilled: false, isClosed: true);
                        }
                        else
                        {
                            context.LineTo(point, isStroked: true, isSmoothJoin: true);
                        }
                    }
                }
                else
                {
                    const int sampleCount = 240;
                    for (int index = 0; index <= sampleCount; index++)
                    {
                        double signedPolarAngle = -Attribute.MaxAngle + index * Attribute.MaxAngle * 2 / sampleCount;
                        double azimuthAngle = signedPolarAngle < 0 ? Attribute.ReferenceAngle + 180 : Attribute.ReferenceAngle;
                        Point point = GetHorizontalVerticalPoint(center, Math.Abs(signedPolarAngle), azimuthAngle);
                        if (index == 0)
                        {
                            context.BeginFigure(point, isFilled: false, isClosed: false);
                        }
                        else
                        {
                            context.LineTo(point, isStroked: true, isSmoothJoin: true);
                        }
                    }
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(null, referencePen, geometry);
        }

        private void DrawHorizontalVerticalLabels(DrawingContext dc, Point center)
        {
            if (!Attribute.IsTextVisible)
            {
                return;
            }

            double ratio = Math.Max(Ratio, 1);
            double step = Math.Max(1, Attribute.PolarStep);
            for (double angle = -Attribute.MaxAngle; angle <= Attribute.MaxAngle + 0.001; angle += step)
            {
                if (Math.Abs(angle) < 0.001)
                {
                    continue;
                }

                double offset = AxisRadius * angle / Attribute.MaxAngle;
                string text = $"{angle:F0}°";
                FormattedText formattedText = CreateFormattedText(text, Attribute.TextBrush);
                DrawOutlinedTextClamped(
                    dc,
                    text,
                    new Point(center.X + offset - formattedText.Width / 2, center.Y + 4 / ratio),
                    formattedText);
                DrawOutlinedTextClamped(
                    dc,
                    text,
                    new Point(center.X + 4 / ratio, center.Y - offset - formattedText.Height / 2),
                    formattedText);
            }

            DrawOutlinedText(dc, "H", new Point(center.X + AxisRadius - 22 / ratio, center.Y - 26 / ratio), Attribute.TextBrush);
            DrawOutlinedText(dc, "V", new Point(center.X - 26 / ratio, center.Y - AxisRadius + 4 / ratio), Attribute.TextBrush);
        }

        private void DrawOutlinedTextClamped(DrawingContext dc, string text, Point desiredOrigin, FormattedText formattedText)
        {
            double margin = 3 / Math.Max(Ratio, 1);
            Point origin = new Point(
                Clamp(desiredOrigin.X, margin, Math.Max(margin, ActualWidth - formattedText.Width - margin)),
                Clamp(desiredOrigin.Y, margin, Math.Max(margin, ActualHeight - formattedText.Height - margin)));
            DrawOutlinedText(dc, text, origin, Attribute.TextBrush);
        }

        private Point GetHorizontalVerticalPoint(Point center, double polarAngle, double azimuthAngle)
        {
            if (!ConoscopeHorizontalVerticalProjection.TryConvertPolarToHorizontalVertical(
                Attribute.CoordinateSystem,
                polarAngle,
                azimuthAngle,
                Attribute.MaxAngle,
                out double horizontalAngle,
                out double verticalAngle))
            {
                return center;
            }

            return new Point(
                center.X + horizontalAngle / Attribute.MaxAngle * AxisRadius,
                center.Y - verticalAngle / Attribute.MaxAngle * AxisRadius);
        }

        private bool TryGetHorizontalVerticalAngles(Point point, double tolerance, out double horizontalAngle, out double verticalAngle)
        {
            horizontalAngle = (point.X - Center.X) / AxisRadius * Attribute.MaxAngle;
            verticalAngle = (Center.Y - point.Y) / AxisRadius * Attribute.MaxAngle;
            double angularTolerance = tolerance / AxisRadius * Attribute.MaxAngle;
            return Math.Abs(horizontalAngle) <= Attribute.MaxAngle + angularTolerance
                && Math.Abs(verticalAngle) <= Attribute.MaxAngle + angularTolerance;
        }

        private void DrawConcentricCircles(DrawingContext dc, Point center, Pen axisPen)
        {
            double step = Math.Max(1, Attribute.PolarStep);
            for (double radiusAngle = step; radiusAngle <= Attribute.MaxAngle + 0.001; radiusAngle += step)
            {
                double radius = RadiusFromAngle(radiusAngle);
                dc.DrawEllipse(null, axisPen, center, radius, radius);
            }
        }

        private void DrawAzimuthLines(DrawingContext dc, Point center, Pen axisPen)
        {
            double step = Math.Max(1, Attribute.AzimuthStep);
            for (double angle = 0; angle < 360; angle += step)
            {
                Point end = GetPointOnAxis(center, AxisRadius, angle);
                dc.DrawLine(axisPen, center, end);
            }

            if (!Attribute.IsTextVisible)
            {
                return;
            }

            for (double angle = 0; angle <= 180 + 0.001; angle += step)
            {
                DrawAngleLabel(dc, center, angle);
            }
        }

        private void DrawReference(DrawingContext dc, Point center, Pen referencePen)
        {
            if (Attribute.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                var endpoints = GetAzimuthLineEndpoints(center, AxisRadius, Attribute.ReferenceAngle);
                dc.DrawLine(referencePen, endpoints.Start, endpoints.End);
            }
            else
            {
                double radius = RadiusFromAngle(Attribute.ReferenceRadiusAngle);
                dc.DrawEllipse(null, referencePen, center, radius, radius);
            }

            if (!Attribute.IsTextVisible)
            {
                return;
            }

            string text = Attribute.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? $"{Attribute.ReferenceAngle:F1}(A)"
                : $"{Attribute.ReferenceRadiusAngle:F1}(R)";
            DrawText(dc, text, center + new Vector(12 / Math.Max(Ratio, 1), 12 / Math.Max(Ratio, 1)), Attribute.ReferenceBrush);
        }

        private void DrawAngleLabel(DrawingContext dc, Point center, double angle)
        {
            Point labelPoint = GetPointOnAxis(center, AxisRadius + 18 / Math.Max(Ratio, 1), angle);
            string text = $"{angle:F0}(A)";
            FormattedText formattedText = CreateFormattedText(text, Attribute.TextBrush);
            Point origin = new Point(labelPoint.X - formattedText.Width / 2, labelPoint.Y - formattedText.Height / 2);
            DrawOutlinedText(dc, text, origin, Attribute.TextBrush);
        }

        private void DrawText(DrawingContext dc, string text, Point point, Brush brush)
        {
            DrawOutlinedText(dc, text, point, brush);
        }

        private void DrawOutlinedText(DrawingContext dc, string text, Point origin, Brush brush)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            FormattedText outlineText = CreateFormattedText(text, TextOutlineBrush);
            double offset = 1.25 / Math.Max(Ratio, 1);
            foreach (Vector direction in TextOutlineDirections)
            {
                dc.DrawText(outlineText, origin + direction * offset);
            }

            FormattedText mainText = CreateFormattedText(text, brush);
            dc.DrawText(mainText, origin);
        }

        private FormattedText CreateFormattedText(string text, Brush brush)
        {
            double ratio = Ratio <= double.Epsilon ? 1 : Ratio;
            return new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                Attribute.FontSize / ratio,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private double RadiusFromAngle(double radiusAngle)
        {
            if (Attribute.MaxAngle <= double.Epsilon)
            {
                return 0;
            }

            return AxisRadius * radiusAngle / Attribute.MaxAngle;
        }

        private static Point GetPointOnAxis(Point center, double radius, double angle)
        {
            double radians = angle * Math.PI / 180.0;
            return new Point(center.X + radius * Math.Cos(radians), center.Y - radius * Math.Sin(radians));
        }

        private static double GetAzimuthAngle(Point center, Point point)
        {
            double deltaX = point.X - center.X;
            double deltaY = center.Y - point.Y;
            double angle = Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI;
            return ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(angle);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            SolidColorBrush brush = new(color);
            brush.Freeze();
            return brush;
        }
    }

    public sealed class ConoscopeCoordinateAxisController : IDisposable
    {
        private readonly DrawCanvas drawCanvas;
        private readonly Zoombox zoombox;
        private bool isDragging;

        internal bool IsUpdatingReference { get; private set; }

        public ConoscopeCoordinateAxisController(DrawCanvas drawCanvas, Zoombox zoombox, ConoscopeCoordinateAxisParam param)
        {
            this.drawCanvas = drawCanvas;
            this.zoombox = zoombox;
            Axis = new ConoscopeCoordinateAxisVisual(param);

            drawCanvas.PreviewMouseLeftButtonDown += DrawCanvas_PreviewMouseLeftButtonDown;
            drawCanvas.MouseMove += DrawCanvas_MouseMove;
            drawCanvas.PreviewMouseUp += DrawCanvas_PreviewMouseUp;
            drawCanvas.MouseLeave += DrawCanvas_MouseLeave;
            zoombox.LayoutUpdated += Zoombox_LayoutUpdated;
        }

        public event EventHandler<ConoscopeCoordinateReferenceChangedEventArgs>? ReferenceChanged;
        public event EventHandler<ConoscopeCoordinateReferenceChangedEventArgs>? PointerMoved;
        public event EventHandler? PointerLeft;

        public ConoscopeCoordinateAxisVisual Axis { get; }

        public void Configure(Point center, double axisRadius, double maxAngle, double coefficient)
        {
            Axis.Configure(drawCanvas.ActualWidth, drawCanvas.ActualHeight, center, axisRadius, maxAngle, coefficient, zoombox.ContentMatrix.M11);
        }

        public void Show()
        {
            if (!drawCanvas.ContainsVisual(Axis))
            {
                drawCanvas.AddVisual(Axis);
            }
        }

        public void BringToFront()
        {
            drawCanvas.TopVisual(Axis);
        }

        public void Dispose()
        {
            drawCanvas.PreviewMouseLeftButtonDown -= DrawCanvas_PreviewMouseLeftButtonDown;
            drawCanvas.MouseMove -= DrawCanvas_MouseMove;
            drawCanvas.PreviewMouseUp -= DrawCanvas_PreviewMouseUp;
            drawCanvas.MouseLeave -= DrawCanvas_MouseLeave;
            zoombox.LayoutUpdated -= Zoombox_LayoutUpdated;

            if (drawCanvas.ContainsVisual(Axis))
            {
                drawCanvas.RemoveVisual(Axis);
            }

            Axis.Dispose();
        }

        private void DrawCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!Axis.Attribute.IsInteractionEnabled)
            {
                return;
            }

            if (IsReferenceInteractionBypassed())
            {
                PointerLeft?.Invoke(this, EventArgs.Empty);
                return;
            }

            Point point = e.GetPosition(drawCanvas);
            if (!Axis.ContainsInteractivePoint(point))
            {
                return;
            }

            isDragging = true;
            drawCanvas.CaptureMouse();
            bool isValueChanged = UpdateReferenceFromPoint(point);
            PointerLeft?.Invoke(this, EventArgs.Empty);
            RaiseReferenceChanged(false, point, isValueChanged);

            e.Handled = true;
        }

        private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!Axis.Attribute.IsInteractionEnabled)
            {
                return;
            }

            if (IsReferenceInteractionBypassed())
            {
                if (!isDragging)
                {
                    PointerLeft?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            Point point = e.GetPosition(drawCanvas);
            if (!isDragging)
            {
                if (Axis.ContainsInteractivePoint(point))
                {
                    RaisePointerMoved(point);
                }
                else
                {
                    PointerLeft?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            bool isValueChanged = UpdateReferenceFromPoint(point);
            RaiseReferenceChanged(false, point, isValueChanged);
            e.Handled = true;
        }

        private void DrawCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            PointerLeft?.Invoke(this, EventArgs.Empty);
        }

        private void DrawCanvas_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDragging)
            {
                return;
            }

            isDragging = false;
            drawCanvas.ReleaseMouseCapture();
            Point point = e.GetPosition(drawCanvas);
            bool isValueChanged = UpdateReferenceFromPoint(point);
            RaiseReferenceChanged(true, point, isValueChanged);
            e.Handled = true;
        }

        private bool UpdateReferenceFromPoint(Point point)
        {
            IsUpdatingReference = true;
            try
            {
                return Axis.UpdateReferenceFromPoint(point);
            }
            finally
            {
                IsUpdatingReference = false;
            }
        }

        private void Zoombox_LayoutUpdated(object? sender, EventArgs e)
        {
            double ratio = zoombox.ContentMatrix.M11;
            if (Math.Abs(Axis.Ratio - ratio) < 0.0001 && Math.Abs(Axis.ActualWidth - drawCanvas.ActualWidth) < 0.0001 && Math.Abs(Axis.ActualHeight - drawCanvas.ActualHeight) < 0.0001)
            {
                return;
            }

            Axis.Ratio = ratio <= double.Epsilon ? 1 : ratio;
            Axis.ActualWidth = drawCanvas.ActualWidth;
            Axis.ActualHeight = drawCanvas.ActualHeight;
            Axis.Render();
        }

        private static bool IsReferenceInteractionBypassed()
        {
            return (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        }

        private void RaiseReferenceChanged(bool isFinal, Point position, bool isValueChanged)
        {
            ReferenceChanged?.Invoke(this, new ConoscopeCoordinateReferenceChangedEventArgs(
                Axis.Attribute.ReferenceMode,
                Axis.Attribute.ReferenceAngle,
                Axis.Attribute.ReferenceRadiusAngle,
            position,
            isFinal,
            isValueChanged));
        }

        private void RaisePointerMoved(Point position)
        {
            PointerMoved?.Invoke(this, new ConoscopeCoordinateReferenceChangedEventArgs(
                Axis.Attribute.ReferenceMode,
                Axis.Attribute.ReferenceAngle,
                Axis.Attribute.ReferenceRadiusAngle,
                position,
                false,
                false));
        }
    }
}
