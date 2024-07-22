#pragma warning disable CS8602,CS8604
using ColorVision.UI.Draw;
using ColorVision.Engine.Templates.POI;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Templates.POI
{
    public partial class WindowFocusPoint : Window
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

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }


        private void ImageShow_Initialized(object sender, EventArgs e)
        {
            ImageShow.ContextMenuOpening += MainWindow_ContextMenuOpening;
        }
        private void MainWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var Point = Mouse.GetPosition(ImageShow);
            var DrawingVisual = ImageShow.GetVisual(Point);

            if (DrawingVisual != null && DrawingVisual is IDrawingVisual drawing)
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

        private static void DrawSelectRect(DrawingVisual drawingVisual, Rect rect)
        {
            using DrawingContext dc = drawingVisual.RenderOpen();
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77F3F3F3")), new Pen(Brushes.Blue, 1), rect);
        }

        private DrawingVisual SelectRect = new();

        private bool IsMouseDown;
        private Point MouseDownP;

        private DrawingVisual? SelectDrawingVisual;
        private DVPolygon? DrawingPolygonCache;
        private DVCircleText? DrawCircleCache;
        private DVRectangleText? DrawingRectangleCache;


        private void ImageShow_MouseLeave(object sender, MouseEventArgs e)
        {

        }

        private void ImageShow_MouseEnter(object sender, MouseEventArgs e)
        {

        }
        private void ImageShow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (PoiParam.DatumArea.IsUserDraw)
            {
                if (PoiParam.DatumArea.IsAreaPolygon && ToolBarTop.DrawPolygon && DrawingPolygonCache != null) 
                { 
                    ImageShow.RemoveVisual(DrawingPolygonCache);
                    PoiParam.DatumArea.Polygons.Clear();
                    foreach (var item in DrawingPolygonCache.Attribute.Points)
                    {
                        PoiParam.DatumArea.Polygons.Add(new PolygonPoint(item.X, item.Y));
                    }
                    DrawingPolygonCache = null;
                    RenderDatumArea();
                }

                if (PoiParam.DatumArea.IsAreaMask && ToolBarTop.DrawPolygon)
                {
                    ImageShow.RemoveVisual(DrawingPolygonCache);
                    if (DrawingPolygonCache != null && DrawingPolygonCache.Attribute.Points.Count == 4)
                    {
                        PoiParam.DatumArea.Polygon1X = (int)DrawingPolygonCache.Attribute.Points[0].X;
                        PoiParam.DatumArea.Polygon1Y = (int)DrawingPolygonCache.Attribute.Points[0].Y;
                        PoiParam.DatumArea.Polygon2X = (int)DrawingPolygonCache.Attribute.Points[1].X;
                        PoiParam.DatumArea.Polygon2Y = (int)DrawingPolygonCache.Attribute.Points[1].Y;
                        PoiParam.DatumArea.Polygon3X = (int)DrawingPolygonCache.Attribute.Points[2].X;
                        PoiParam.DatumArea.Polygon3Y = (int)DrawingPolygonCache.Attribute.Points[2].Y;
                        PoiParam.DatumArea.Polygon4X = (int)DrawingPolygonCache.Attribute.Points[3].X;
                        PoiParam.DatumArea.Polygon4Y = (int)DrawingPolygonCache.Attribute.Points[3].Y;

                        RenderDatumArea();
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
        }

        private void ImageShow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                MouseDownP = e.GetPosition(drawCanvas);
                IsMouseDown = true;
                drawCanvas.CaptureMouse();


                Brush brush = PoiParam.DatumArea.IsUserDraw ? Brushes.Blue : Brushes.Red;

                if (PoiParam.DatumArea.IsUserDraw)
                {
                    if (PoiParam.DatumArea.IsAreaCircle)
                        ToolBarTop.DrawCircle = true;
                    if (PoiParam.DatumArea.IsAreaRect)
                        ToolBarTop.DrawRect = true;
                    if (PoiParam.DatumArea.IsAreaPolygon)
                        ToolBarTop.DrawPolygon = true;
                    if (PoiParam.DatumArea.IsAreaMask)
                        ToolBarTop.DrawPolygon = true;
                }


                if (ToolBarTop.EraseVisual)
                {
                    DrawSelectRect(SelectRect, new Rect(MouseDownP, MouseDownP)); ;
                    drawCanvas.AddVisual(SelectRect);
                }
                else if (ToolBarTop.DrawCircle)
                {
                    No++;
                    DrawCircleCache = new DVCircleText() { AutoAttributeChanged = false };
                    DrawCircleCache.Attribute.Id = No;
                    DrawCircleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawCircleCache.Attribute.Center = MouseDownP;
                    DrawCircleCache.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                    DrawCircleCache.Attribute.Text = "Point_" + No.ToString();
                    drawCanvas.AddVisual(DrawCircleCache);
                }
                else if (ToolBarTop.DrawRect)
                {
                    No++;

                    DrawingRectangleCache = new DVRectangleText() { AutoAttributeChanged = false };
                    DrawingRectangleCache.Attribute.Id = No;
                    DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, new Point(MouseDownP.X + 30, MouseDownP.Y + 30));
                    DrawingRectangleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawingRectangleCache.Attribute.Text = "Point_" + No.ToString();
                    drawCanvas.AddVisual(DrawingRectangleCache);
                }
                else if (ToolBarTop.DrawPolygon)
                {
                    if (DrawingPolygonCache == null)
                    {
                        DrawingPolygonCache = new DVPolygon();
                        DrawingPolygonCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                        drawCanvas.AddVisual(DrawingPolygonCache);
                    }
                }
                else if (drawCanvas.GetVisual(MouseDownP) is IDrawingVisual drawingVisual)
                {
                    PropertyGrid2.SelectedObject = drawingVisual.BaseAttribute;

                    ListView1.ScrollIntoView(drawingVisual);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(drawingVisual);

                    if (ToolBarTop.ImageEditMode == true)
                    {
                        if (drawingVisual is DrawingVisual visual)
                            SelectDrawingVisual = visual;

                        if (SelectDrawingVisual is DVCircle Circl)
                        {
                            Circl.IsDrawing = true;
                        }
                    }
                }
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

                if (ToolBarTop.DrawPolygon)
                {
                    if (DrawingPolygonCache != null)
                    {
                        DrawingPolygonCache.MovePoints = point;
                        DrawingPolygonCache.Render();
                    }
                }

                if (IsMouseDown)
                {

                    if (ToolBarTop.EraseVisual)
                    {
                        DrawSelectRect(SelectRect, new Rect(MouseDownP, point)); ;
                    }
                    else if (ToolBarTop.DrawCircle && DrawCircleCache !=null)
                    {
                        double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                        DrawCircleCache.Attribute.Radius = Radius;
                        DrawCircleCache.Render();
                    }
                    else if (ToolBarTop.DrawRect && DrawingRectangleCache!=null)
                    {
                        DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, point);
                        DrawingRectangleCache.Render();
                    }
                    else if (ToolBarTop.DrawPolygon)
                    {

                    }
                    else if (SelectDrawingVisual != null)
                    {
                        if (SelectDrawingVisual is IRectangle rectangle)
                        {
                            var OldRect = rectangle.Rect;
                            rectangle.Rect = new Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);
                        }
                        else if (SelectDrawingVisual is ICircle Circl)
                        {
                            Circl.Center += point - LastMouseMove;
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
                IsMouseDown = false;
                var MouseUpP = e.GetPosition(drawCanvas);



                if (PoiParam.DatumArea.IsUserDraw)
                {
                    if (PoiParam.DatumArea.IsAreaCircle && ToolBarTop.DrawCircle)
                    {
                        PoiParam.DatumArea.CenterX = (int)DrawCircleCache.Attribute.Center.X;
                        PoiParam.DatumArea.CenterY = (int)DrawCircleCache.Attribute.Center.Y;
                        PoiParam.DatumArea.AreaCircleRadius = (int)DrawCircleCache.Attribute.Radius;
                        drawCanvas.RemoveVisual(DrawCircleCache);
                    }

                    if (PoiParam.DatumArea.IsAreaRect && ToolBarTop.DrawRect)
                    {
                        PoiParam.DatumArea.CenterX = (int)(DrawingRectangleCache.Attribute.Rect.Width / 2 + DrawingRectangleCache.Attribute.Rect.X);
                        PoiParam.DatumArea.CenterY = (int)(DrawingRectangleCache.Attribute.Rect.Height / 2 + DrawingRectangleCache.Attribute.Rect.Y);
                        PoiParam.DatumArea.AreaRectWidth = (int)DrawingRectangleCache.Attribute.Rect.Width;
                        PoiParam.DatumArea.AreaRectHeight = (int)DrawingRectangleCache.Attribute.Rect.Height;
                        drawCanvas.RemoveVisual(DrawingRectangleCache);
                    }
                    RenderDatumArea();
                }


                if (ToolBarTop.EraseVisual)
                {
                    drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseDownP));
                    drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseUpP));
                    foreach (var item in drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
                    {
                        drawCanvas.RemoveVisual(item);
                    }
                    drawCanvas.RemoveVisual(SelectRect);
                }
                else if (ToolBarTop.DrawPolygon && DrawingPolygonCache != null)
                {
                    DrawingPolygonCache.Points.Add(MouseUpP);
                    DrawingPolygonCache.MovePoints = null;
                    DrawingPolygonCache.Render();
                }
                else if (ToolBarTop.DrawCircle && DrawCircleCache != null)
                {
                    DrawCircleCache.Render();
                    PropertyGrid2.SelectedObject = DrawCircleCache.BaseAttribute;

                    DrawCircleCache.AutoAttributeChanged = true;

                    ListView1.ScrollIntoView(DrawCircleCache);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawCircleCache);

                }
                else if (ToolBarTop.DrawRect)
                {
                    DrawingRectangleCache.Render();


                    PropertyGrid2.SelectedObject = DrawingRectangleCache.BaseAttribute;
                    DrawingRectangleCache.AutoAttributeChanged = true;
                    ListView1.ScrollIntoView(DrawingRectangleCache);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawingRectangleCache);

                }
                drawCanvas.ReleaseMouseCapture();

                if (SelectDrawingVisual is DVCircle circle)
                {
                    circle.IsDrawing = false;
                    circle.Render();
                }
                SelectDrawingVisual = null;
            }
        }

        private void ImageShow_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }




    }
}
