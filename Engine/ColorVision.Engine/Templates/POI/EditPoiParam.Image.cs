#pragma warning disable CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.POI
{
    public class RadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (double.TryParse(value.ToString(), out double result))
            {
                if (result % 1 == 0 || result % 1 == 0.5)
                {
                    return result;
                }
            }
            return Binding.DoNothing;
        }
    }

    public class RadiusValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (double.TryParse(value.ToString(), out double result))
            {
                if (result % 1 == 0 || result % 1 == 0.5)
                {
                    return ValidationResult.ValidResult;
                }
            }
            return new ValidationResult(false, "Value must be an integer or end with .5");
        }
    }

    public class RoundToNearestHalfConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return Math.Round(doubleValue * 2, MidpointRounding.AwayFromZero) / 2;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return Math.Round(doubleValue * 2, MidpointRounding.AwayFromZero) / 2;
            }
            return value;
        }
    }


    public partial class EditPoiParam : Window
    {
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9]+").IsMatch(e.Text);
        }
        private void ImageShow_Initialized(object sender, EventArgs e)
        {
            ImageShow.ContextMenuOpening += MainWindow_ContextMenuOpening;
        }
        private void MainWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var Point = Mouse.GetPosition(ImageShow);
            var DrawingVisual = ImageShow.GetVisual(Point);

            if (DrawingVisual != null && ImageEditViewMode.SelectDrawingVisual != DrawingVisual && DrawingVisual is IDrawingVisual drawing)
            {
                var ContextMenu = new ContextMenu();

                MenuItem menuItem = new() { Header = "隐藏(_H)" };
                menuItem.Click += (s, e) =>
                {
                    drawing.BaseAttribute.IsShow = false;
                };
                MenuItem menuIte2 = new() { Header = "删除(_D)" };

                menuIte2.Click += (s, e) =>
                {
                    ImageShow.RemoveVisual(DrawingVisual);
                    PropertyGrid2.SelectedObject = null;
                };
                ContextMenu.Items.Add(menuItem);
                ContextMenu.Items.Add(menuIte2);
                ImageShow.ContextMenu = ContextMenu;
            }
            else
            {
                ImageShow.ContextMenu = null;
            }

        }


        private DrawingVisual SelectRect = new DrawingVisual();

        private bool IsMouseDown;
        private Point MouseDownP;

        private DVPolygon? DrawingPolygonCache;

        private DVCircleText? DrawCircleCache;
        private DVRectangleText? DrawingRectangleCache;



        private void ImageShow_MouseLeave(object sender, MouseEventArgs e)
        {

        }

        private void ImageShow_MouseEnter(object sender, MouseEventArgs e)
        {

        }
        bool IsRightButtonDown = false;

        private void ImageShow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (PoiParam.PoiConfig.IsUserDraw)
            {
                if (PoiParam.PoiConfig.IsAreaPolygon && ImageEditViewMode.DrawPolygon && DrawingPolygonCache != null) 
                { 
                    ImageShow.RemoveVisual(DrawingPolygonCache);
                    PoiParam.PoiConfig.Polygons.Clear();
                    foreach (var item in DrawingPolygonCache.Attribute.Points)
                    {
                        PoiParam.PoiConfig.Polygons.Add(new PolygonPoint(item.X, item.Y));
                    }
                    DrawingPolygonCache = null;
                    RenderPoiConfig();
                }

                if (PoiParam.PoiConfig.IsAreaMask && ImageEditViewMode.DrawPolygon)
                {
                    ImageShow.RemoveVisual(DrawingPolygonCache);
                    if (DrawingPolygonCache != null && DrawingPolygonCache.Attribute.Points.Count == 4)
                    {
                        PoiParam.PoiConfig.Polygon1X = (int)DrawingPolygonCache.Attribute.Points[0].X;
                        PoiParam.PoiConfig.Polygon1Y = (int)DrawingPolygonCache.Attribute.Points[0].Y;
                        PoiParam.PoiConfig.Polygon2X = (int)DrawingPolygonCache.Attribute.Points[1].X;
                        PoiParam.PoiConfig.Polygon2Y = (int)DrawingPolygonCache.Attribute.Points[1].Y;
                        PoiParam.PoiConfig.Polygon3X = (int)DrawingPolygonCache.Attribute.Points[2].X;
                        PoiParam.PoiConfig.Polygon3Y = (int)DrawingPolygonCache.Attribute.Points[2].Y;
                        PoiParam.PoiConfig.Polygon4X = (int)DrawingPolygonCache.Attribute.Points[3].X;
                        PoiParam.PoiConfig.Polygon4Y = (int)DrawingPolygonCache.Attribute.Points[3].Y;

                        RenderPoiConfig();
                    }
                    else
                    {
                        MessageBox.Show("必须要是4个点");
                    }
                    DrawingPolygonCache = null;

                }
            }

            if (DrawingPolygonCache != null)
            {
                DrawingPolygonCache.MovePoints = null;
                DrawingPolygonCache.Render();
                DrawingPolygonCache = null;
            }
            IsRightButtonDown = true;
        }


        public void SelectDrawingVisualsClear()
        {
            if (ImageEditViewMode.SelectDrawingVisuals != null)
            {
                foreach (var item in ImageEditViewMode.SelectDrawingVisuals)
                {
                    if (item is IDrawingVisual id)
                    {
                        id.Pen.Brush = Brushes.Red;
                        id.Render();
                    }
                }
                ImageEditViewMode.SelectDrawingVisuals = null;
            }
        }

        public void SelectDrawingVisualClear()
        {
            if (ImageEditViewMode.SelectDrawingVisual != null)
            {
                if (ImageEditViewMode.SelectDrawingVisual is IDrawingVisual id)
                {
                    id.Pen.Brush = Brushes.Red;
                    id.Render();
                }
                ImageEditViewMode.SelectDrawingVisual = null;
            }
        }
        private void ImageShow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                if (ImageEditViewMode.Gridline.IsShow == true)
                    return;
                if (ImageEditViewMode.ConcentricCircle == true)
                    return;

                MouseDownP = e.GetPosition(drawCanvas);
                IsMouseDown = true;
                drawCanvas.CaptureMouse();

                Brush brush = PoiParam.PoiConfig.IsUserDraw ? Brushes.Blue : Brushes.Red;

                if (PoiParam.PoiConfig.IsUserDraw)
                {
                    if (PoiParam.PoiConfig.IsAreaCircle)
                        ImageEditViewMode.DrawCircle = true;
                    if (PoiParam.PoiConfig.IsAreaRect)
                        ImageEditViewMode.DrawRect = true;
                    if (PoiParam.PoiConfig.IsAreaPolygon)
                        ImageEditViewMode.DrawPolygon = true;
                    if (PoiParam.PoiConfig.IsAreaMask)
                        ImageEditViewMode.DrawPolygon = true;
                }


                if (ImageEditViewMode.EraseVisual)
                {
                    ImageViewModel.DrawSelectRect(SelectRect, new System.Windows.Rect(MouseDownP, MouseDownP)); ;
                    drawCanvas.AddVisual(SelectRect);

                    SelectDrawingVisualsClear();
                    SelectDrawingVisualClear();
                    return;
                }

                if (ImageEditViewMode.DrawCircle)
                {
                    No++;
                    DrawCircleCache = new DVCircleText() { AutoAttributeChanged = false };
                    DrawCircleCache.Attribute.Id = No;
                    DrawCircleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawCircleCache.Attribute.Center = MouseDownP;
                    DrawCircleCache.Attribute.Radius = PoiConfig.DefalutRadius;
                    DrawCircleCache.Attribute.Text = "Point_" + No.ToString();

                    drawCanvas.AddVisual(DrawCircleCache);


                    SelectDrawingVisualsClear();
                    SelectDrawingVisualClear();
                    return;
                }
                if (ImageEditViewMode.DrawRect)
                {
                    No++;

                    DrawingRectangleCache = new DVRectangleText() { AutoAttributeChanged = false };
                    DrawingRectangleCache.Attribute.Id = No;
                    DrawingRectangleCache.Attribute.Rect = new System.Windows.Rect(MouseDownP, new Point(MouseDownP.X + PoiConfig.DefalutWidth, MouseDownP.Y + PoiConfig.DefalutHeight));
                    DrawingRectangleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawingRectangleCache.Attribute.Text = "Point_" + No.ToString();

                    drawCanvas.AddVisual(DrawingRectangleCache);


                    SelectDrawingVisualsClear();
                    SelectDrawingVisualClear();
                    return;
                }
                if (ImageEditViewMode.DrawPolygon)
                {
                    if (DrawingPolygonCache == null)
                    {
                        DrawingPolygonCache = new DVPolygon();
                        DrawingPolygonCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                        drawCanvas.AddVisual(DrawingPolygonCache);
                    }


                    SelectDrawingVisualsClear();
                    SelectDrawingVisualClear();
                    return;
                }


                if (drawCanvas.GetVisual(MouseDownP) is IDrawingVisual drawingVisual)
                {
                    PropertyGrid2.SelectedObject = drawingVisual.BaseAttribute;

                    ListView1.ScrollIntoView(drawingVisual);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(drawingVisual);

                    if (ImageEditViewMode.ImageEditMode == true)
                    {
                        if (ImageEditViewMode.SelectDrawingVisuals != null && drawingVisual is DrawingVisual visual1 && ImageEditViewMode.SelectDrawingVisuals.Contains(visual1))
                            return;

                        if (drawingVisual is DrawingVisual visual)
                        {
                            if (ImageEditViewMode.SelectDrawingVisual != visual)
                            {
                                if (ImageEditViewMode.SelectDrawingVisual is IDrawingVisual id)
                                {
                                    id.Pen.Brush = Brushes.Red;
                                    id.Render();
                                }
                                ImageEditViewMode.SelectDrawingVisual = null;
                            }
                            ImageEditViewMode.SelectDrawingVisual = visual;
                            drawingVisual.Pen.Brush = Brushes.Yellow;
                            drawingVisual.Render();
                        }
                        if (ImageEditViewMode.SelectDrawingVisual is DVCircle Circl)
                        {
                            Circl.IsDrawing = true;
                        }
                        if (ImageEditViewMode.SelectDrawingVisuals != null)
                        {
                            foreach (var item in ImageEditViewMode.SelectDrawingVisuals)
                            {
                                if (item is IDrawingVisual id)
                                {
                                    id.Pen.Brush = Brushes.Red;
                                    id.Render();
                                }
                            }
                            ImageEditViewMode.SelectDrawingVisuals = null;
                        }
                    }
                    return;
                }

                SelectDrawingVisualsClear();
                SelectDrawingVisualClear();

                ImageViewModel.DrawSelectRect(SelectRect, new System.Windows.Rect(MouseDownP, MouseDownP)); ;
                drawCanvas.AddVisual(SelectRect);
            }
        }
        Point LastMouseMove;

        private void ImageShow_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && (Zoombox1.ActivateOn == ModifierKeys.None || !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);

                var controlWidth = drawCanvas.ActualWidth;
                var controlHeight = drawCanvas.ActualHeight;

                if (ImageEditViewMode.DrawPolygon)
                {
                    if (DrawingPolygonCache != null)
                    {
                        DrawingPolygonCache.MovePoints = point;
                        DrawingPolygonCache.Render();
                    }
                }

                if (IsMouseDown)
                {
                    ImageViewModel.DrawSelectRect(SelectRect, new System.Windows.Rect(MouseDownP, point));

                    if (ImageEditViewMode.DrawCircle && DrawCircleCache !=null)
                    {
                        double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                        DrawCircleCache.Attribute.Radius = Radius;
                        DrawCircleCache.Render();
                    }
                    else if (ImageEditViewMode.DrawRect && DrawingRectangleCache!=null)
                    {
                        DrawingRectangleCache.Attribute.Rect = new System.Windows.Rect(MouseDownP, point);
                        DrawingRectangleCache.Render();
                    }
                    else if (ImageEditViewMode.DrawPolygon)
                    {

                    }
                    if (ImageEditViewMode.SelectDrawingVisual != null)
                    {
                        if (ImageEditViewMode.SelectDrawingVisual is IRectangle rectangle)
                        {
                            var OldRect = rectangle.Rect;
                            rectangle.Rect = new System.Windows.Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);
                        }
                        else if (ImageEditViewMode.SelectDrawingVisual is ICircle Circl)
                        {
                            Circl.Center += point - LastMouseMove;
                        }
                    }
                    if (ImageEditViewMode.SelectDrawingVisuals != null)
                    {
                        foreach (var item in ImageEditViewMode.SelectDrawingVisuals)
                        {
                            if (item is IRectangle rectangle)
                            {
                                var OldRect = rectangle.Rect;
                                rectangle.Rect = new System.Windows.Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);
                            }
                            else if (item is ICircle Circl)
                            {
                                Circl.Center += point - LastMouseMove;
                            }
                        }
                    }
                }

                if (IsRightButtonDown)
                {
                    if (ImageEditViewMode.SelectDrawingVisual is ICircle circle)
                    {
                        double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                        circle.Radius = Radius;
                    }

                    if (ImageEditViewMode.SelectDrawingVisual is IRectangle rectangle)
                    {
                        var OldRect = rectangle.Rect;
                        double x = OldRect.X;
                        double y = OldRect.Y;
                        double width = OldRect.Width + point.X - LastMouseMove.X;
                        double height = OldRect.Height + point.Y - LastMouseMove.Y;
                        if (width >= 1 && height >= 1)
                        {
                            rectangle.Rect = new System.Windows.Rect(x, y, width, height);
                        }
                    }
                }
                LastMouseMove = point;
            }
        }
        private void ImageShow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                if (IsMouseDown)
                {
                    IsMouseDown = false;
                    var MouseUpP = e.GetPosition(drawCanvas);

                    if (PoiParam.PoiConfig.IsUserDraw)
                    {
                        if (PoiParam.PoiConfig.IsAreaCircle && ImageEditViewMode.DrawCircle)
                        {
                            PoiParam.PoiConfig.CenterX = (int)DrawCircleCache.Attribute.Center.X;
                            PoiParam.PoiConfig.CenterY = (int)DrawCircleCache.Attribute.Center.Y;
                            PoiParam.PoiConfig.AreaCircleRadius = (int)DrawCircleCache.Attribute.Radius;
                            drawCanvas.RemoveVisual(DrawCircleCache);
                        }

                        if (PoiParam.PoiConfig.IsAreaRect && ImageEditViewMode.DrawRect)
                        {
                            PoiParam.PoiConfig.CenterX = (int)(DrawingRectangleCache.Attribute.Rect.Width / 2 + DrawingRectangleCache.Attribute.Rect.X);
                            PoiParam.PoiConfig.CenterY = (int)(DrawingRectangleCache.Attribute.Rect.Height / 2 + DrawingRectangleCache.Attribute.Rect.Y);
                            PoiParam.PoiConfig.AreaRectWidth = (int)DrawingRectangleCache.Attribute.Rect.Width;
                            PoiParam.PoiConfig.AreaRectHeight = (int)DrawingRectangleCache.Attribute.Rect.Height;
                            drawCanvas.RemoveVisual(DrawingRectangleCache);
                        }
                        RenderPoiConfig();
                    }

                    if (drawCanvas.GetVisual(MouseUpP) is not DrawingVisual dv || ImageEditViewMode.SelectDrawingVisuals == null || !ImageEditViewMode.SelectDrawingVisuals.Contains(dv))
                        SelectDrawingVisualsClear();

                    if (drawCanvas.ContainsVisual(SelectRect))
                    {
                        if (ImageEditViewMode.EraseVisual)
                        {
                            drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseDownP));
                            drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseUpP));
                            foreach (var item in drawCanvas.GetVisuals(new RectangleGeometry(new System.Windows.Rect(MouseDownP, MouseUpP))))
                            {
                                drawCanvas.RemoveVisual(item, false);
                            }
                        }
                        else
                        {
                            ImageEditViewMode.SelectDrawingVisuals = drawCanvas.GetVisuals(new RectangleGeometry(new System.Windows.Rect(MouseDownP, MouseUpP)));
                            foreach (var item in ImageEditViewMode.SelectDrawingVisuals)
                            {
                                if (item is IDrawingVisual drawingVisual)
                                {
                                    drawingVisual.Pen.Brush = Brushes.Yellow;
                                    drawingVisual.Render();
                                }
                            }

                            if (ImageEditViewMode.SelectDrawingVisuals.Count == 0)
                                ImageEditViewMode.SelectDrawingVisuals = null;
                        }

                        drawCanvas.RemoveVisual(SelectRect,false);
                    }


                    if (ImageEditViewMode.DrawPolygon && DrawingPolygonCache != null)
                    {
                        DrawingPolygonCache.Points.Add(MouseUpP);
                        DrawingPolygonCache.MovePoints = null;
                        DrawingPolygonCache.Render();
                    }
                    else if (ImageEditViewMode.DrawCircle && DrawCircleCache != null)
                    {
                        DrawCircleCache.Render();
                        PropertyGrid2.SelectedObject = DrawCircleCache.BaseAttribute;

                        DrawCircleCache.AutoAttributeChanged = true;

                        ListView1.ScrollIntoView(DrawCircleCache);
                        ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawCircleCache);

                        PoiConfig.DefalutRadius = DrawCircleCache.Attribute.Radius;

                    }
                    else if (ImageEditViewMode.DrawRect)
                    {
                        DrawingRectangleCache.Render();


                        PropertyGrid2.SelectedObject = DrawingRectangleCache.BaseAttribute;
                        DrawingRectangleCache.AutoAttributeChanged = true;
                        ListView1.ScrollIntoView(DrawingRectangleCache);
                        ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawingRectangleCache);

                        PoiConfig.DefalutWidth = DrawingRectangleCache.Attribute.Rect.Width;
                        PoiConfig.DefalutHeight = DrawingRectangleCache.Attribute.Rect.Height;
                    }
                    drawCanvas.ReleaseMouseCapture();
                    if (ImageEditViewMode.SelectDrawingVisual is DVCircle circle)
                    {
                        circle.IsDrawing = false;
                        circle.Render();
                    }
                    if (ImageEditViewMode.SelectDrawingVisual != null)
                    {
                        if (ImageEditViewMode.SelectDrawingVisual is IRectangle rectangle)
                        {
                            var l = MouseUpP - MouseDownP;

                            Action undoaction = new Action(() =>
                            {
                                var OldRect = rectangle.Rect;
                                rectangle.Rect = new System.Windows.Rect(OldRect.X - l.X, OldRect.Y - l.Y, OldRect.Width, OldRect.Height);
                            });
                            Action redoaction = new Action(() =>
                            {
                                var OldRect = rectangle.Rect;
                                rectangle.Rect = new System.Windows.Rect(OldRect.X + l.X, OldRect.Y + l.Y, OldRect.Width, OldRect.Height);
                            });
                            ImageShow.AddActionCommand(new ActionCommand(undoaction, redoaction) { Header = "移动IRectangle" });
                        }
                        else if (ImageEditViewMode.SelectDrawingVisual is ICircle Circl)
                        {
                            var l = MouseUpP - MouseDownP;
                            Action undoaction = new Action(() =>
                            {
                                Circl.Center -= l;
                            });
                            Action redoaction = new Action(() =>
                            {
                                Circl.Center += l;
                            });
                            ImageShow.AddActionCommand(new ActionCommand(undoaction, redoaction) { Header = "移动ICircle" });
                        }
                    }
                }
            }
            if (IsRightButtonDown)
                IsRightButtonDown = false;
        }

        private void ImageShow_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }




    }
}
