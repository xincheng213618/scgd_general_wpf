﻿#pragma warning disable CS8602,CS8604
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Templates.POI
{
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
        }


        private void ImageShow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
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
                    ImageEditViewMode.DrawSelectRect(SelectRect, new Rect(MouseDownP, MouseDownP)); ;
                    drawCanvas.AddVisual(SelectRect);

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
                    return;
                }

                if (ImageEditViewMode.DrawCircle)
                {
                    No++;
                    DrawCircleCache = new DVCircleText() { AutoAttributeChanged = false };
                    DrawCircleCache.Attribute.Id = No;
                    DrawCircleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawCircleCache.Attribute.Center = MouseDownP;
                    DrawCircleCache.Attribute.Radius = PoiParam.PoiConfig.DefaultCircleRadius;
                    DrawCircleCache.Attribute.Text = "Point_" + No.ToString();
                    drawCanvas.AddVisual(DrawCircleCache);

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
                    return;
                }
                if (ImageEditViewMode.DrawRect)
                {
                    No++;

                    DrawingRectangleCache = new DVRectangleText() { AutoAttributeChanged = false };
                    DrawingRectangleCache.Attribute.Id = No;
                    DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, new Point(MouseDownP.X + 30, MouseDownP.Y + 30));
                    DrawingRectangleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawingRectangleCache.Attribute.Text = "Point_" + No.ToString();
                    drawCanvas.AddVisual(DrawingRectangleCache);

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

                    return;
                }


                if (drawCanvas.GetVisual(MouseDownP) is IDrawingVisual drawingVisual)
                {
                    PropertyGrid2.SelectedObject = drawingVisual.BaseAttribute;

                    ListView1.ScrollIntoView(drawingVisual);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(drawingVisual);

                    if (ImageEditViewMode.ImageEditMode == true)
                    {
                        if (ImageEditViewMode.SelectDrawingVisuals != null)
                            return;
                        if (drawingVisual is DrawingVisual visual)
                        {
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
                ImageEditViewMode.DrawSelectRect(SelectRect, new Rect(MouseDownP, MouseDownP)); ;
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
                    ImageEditViewMode.DrawSelectRect(SelectRect, new Rect(MouseDownP, point));

                    if (ImageEditViewMode.DrawCircle && DrawCircleCache !=null)
                    {
                        double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                        DrawCircleCache.Attribute.Radius = Radius;
                        DrawCircleCache.Render();
                    }
                    else if (ImageEditViewMode.DrawRect && DrawingRectangleCache!=null)
                    {
                        DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, point);
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
                            rectangle.Rect = new Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);
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
                                rectangle.Rect = new Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);
                            }
                            else if (item is ICircle Circl)
                            {
                                Circl.Center += point - LastMouseMove;
                            }
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

                if (ImageEditViewMode.SelectDrawingVisuals !=null)
                {
                    foreach (var item in ImageEditViewMode.SelectDrawingVisuals)
                    {
                        if (item is IDrawingVisual drawingVisual)
                        {
                            drawingVisual.Pen.Brush = Brushes.Red;
                            drawingVisual.Render();
                        }
                    }
                }

                if (drawCanvas.ContainsVisual(SelectRect))
                {
                    if (ImageEditViewMode.EraseVisual)
                    {
                        drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseDownP));
                        drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseUpP));
                        foreach (var item in drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
                        {
                            drawCanvas.RemoveVisual(item);
                        }
                    }
                    else
                    {
                        ImageEditViewMode.SelectDrawingVisuals = drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP)));
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

                    drawCanvas.RemoveVisual(SelectRect);
                }


                if (ImageEditViewMode.DrawPolygon && DrawingPolygonCache != null)
                {
                    DrawingPolygonCache.Points.Add(MouseUpP);
                    DrawingPolygonCache.MovePoints = null;
                    DrawingPolygonCache.Render();
                }
                else if (ImageEditViewMode.DrawCircle   && DrawCircleCache != null)
                {
                    DrawCircleCache.Render();
                    PropertyGrid2.SelectedObject = DrawCircleCache.BaseAttribute;

                    DrawCircleCache.AutoAttributeChanged = true;

                    ListView1.ScrollIntoView(DrawCircleCache);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawCircleCache);

                }
                else if (ImageEditViewMode.DrawRect)
                {
                    DrawingRectangleCache.Render();


                    PropertyGrid2.SelectedObject = DrawingRectangleCache.BaseAttribute;
                    DrawingRectangleCache.AutoAttributeChanged = true;
                    ListView1.ScrollIntoView(DrawingRectangleCache);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawingRectangleCache);

                }
                drawCanvas.ReleaseMouseCapture();

                if (ImageEditViewMode.SelectDrawingVisual is DVCircle circle)
                {
                    circle.IsDrawing = false;
                    circle.Render();
                }
                if (ImageEditViewMode.SelectDrawingVisual is IDrawingVisual drawingVisual1)
                {
                    drawingVisual1.Pen.Brush = Brushes.Red;
                    drawingVisual1.Render();
                }
                ImageEditViewMode.SelectDrawingVisual = null;
            }
        }

        private void ImageShow_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }




    }
}