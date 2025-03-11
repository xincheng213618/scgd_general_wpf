#pragma warning disable CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.POI
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
        private void ImageShow_Initialized(object sender, EventArgs e)
        {
            ImageShow.ContextMenuOpening += MainWindow_ContextMenuOpening;
        }
        private void MainWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var Point = Mouse.GetPosition(ImageShow);
            var DrawingVisual = ImageShow.GetVisual(Point);

            if (DrawingVisual != null && ImageViewModel.SelectDrawingVisual != DrawingVisual && DrawingVisual is IDrawingVisual drawing)
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

        bool IsRightButtonDown;

        private void ImageShow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (PoiConfig.IsUserDraw)
            {
                if (PoiConfig.IsAreaPolygon && ImageViewModel.DrawPolygon && DrawingPolygonCache != null) 
                { 
                    ImageShow.RemoveVisual(DrawingPolygonCache);
                    PoiConfig.Polygons.Clear();
                    foreach (var item in DrawingPolygonCache.Attribute.Points)
                    {
                        PoiConfig.Polygons.Add(new PolygonPoint(item.X, item.Y));
                    }
                    DrawingPolygonCache = null;
                    RenderPoiConfig();
                }

                if (PoiConfig.IsAreaMask && ImageViewModel.DrawPolygon)
                {
                    ImageShow.RemoveVisual(DrawingPolygonCache);
                    if (DrawingPolygonCache != null && DrawingPolygonCache.Attribute.Points.Count == 4)
                    {
                        PoiConfig.Polygon1X = (int)DrawingPolygonCache.Attribute.Points[0].X;
                        PoiConfig.Polygon1Y = (int)DrawingPolygonCache.Attribute.Points[0].Y;
                        PoiConfig.Polygon2X = (int)DrawingPolygonCache.Attribute.Points[1].X;
                        PoiConfig.Polygon2Y = (int)DrawingPolygonCache.Attribute.Points[1].Y;
                        PoiConfig.Polygon3X = (int)DrawingPolygonCache.Attribute.Points[2].X;
                        PoiConfig.Polygon3Y = (int)DrawingPolygonCache.Attribute.Points[2].Y;
                        PoiConfig.Polygon4X = (int)DrawingPolygonCache.Attribute.Points[3].X;
                        PoiConfig.Polygon4Y = (int)DrawingPolygonCache.Attribute.Points[3].Y;

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
            if (ImageViewModel.SelectDrawingVisuals != null)
            {
                foreach (var item in ImageViewModel.SelectDrawingVisuals)
                {
                    if (item is IDrawingVisual id)
                    {
                        id.Pen.Brush = Brushes.Red;
                        id.Render();
                    }
                }
                ImageViewModel.SelectDrawingVisuals = null;
            }
        }

        private void ImageShow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                if (ImageViewModel.Gridline.IsShow == true)
                    return;
                if (ImageViewModel.ConcentricCircle == true)
                    return;

                MouseDownP = e.GetPosition(drawCanvas);
                IsMouseDown = true;
                drawCanvas.CaptureMouse();

                Brush brush = PoiConfig.IsUserDraw ? Brushes.Blue : Brushes.Red;

                if (PoiConfig.IsUserDraw)
                {
                    if (PoiConfig.IsAreaCircle)
                        ImageViewModel.DrawCircle = true;
                    if (PoiConfig.IsAreaRect)
                        ImageViewModel.DrawRect = true;
                    if (PoiConfig.IsAreaPolygon)
                        ImageViewModel.DrawPolygon = true;
                    if (PoiConfig.IsAreaMask)
                        ImageViewModel.DrawPolygon = true;
                }


                if (ImageViewModel.EraseVisual)
                {
                    ImageViewModel.DrawSelectRect(SelectRect, new System.Windows.Rect(MouseDownP, MouseDownP)); ;
                    drawCanvas.AddVisual(SelectRect);

                    SelectDrawingVisualsClear();
                    ImageViewModel.SelectDrawingVisual = null;
                    return;
                }

                if (ImageViewModel.DrawCircle)
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
                    ImageViewModel.SelectDrawingVisual = null;
                    return;
                }
                if (ImageViewModel.DrawRect)
                {
                    No++;

                    DrawingRectangleCache = new DVRectangleText() { AutoAttributeChanged = false };
                    DrawingRectangleCache.Attribute.Id = No;
                    DrawingRectangleCache.Attribute.Rect = new System.Windows.Rect(MouseDownP, new Point(MouseDownP.X + PoiConfig.DefalutWidth, MouseDownP.Y + PoiConfig.DefalutHeight));
                    DrawingRectangleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawingRectangleCache.Attribute.Text = "Point_" + No.ToString();

                    drawCanvas.AddVisual(DrawingRectangleCache);


                    SelectDrawingVisualsClear();
                    ImageViewModel.SelectDrawingVisual = null;
                    return;
                }
                if (ImageViewModel.DrawPolygon)
                {
                    if (DrawingPolygonCache == null)
                    {
                        DrawingPolygonCache = new DVPolygon();
                        DrawingPolygonCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                        drawCanvas.AddVisual(DrawingPolygonCache);
                    }


                    SelectDrawingVisualsClear();
                    ImageViewModel.SelectDrawingVisual = null;
                    return;
                }


                var MouseVisual = drawCanvas.GetVisual(MouseDownP);
                if (MouseVisual == ImageViewModel.SelectEditorVisual)
                    return;
                if (MouseVisual is IDrawingVisual drawingVisual)
                {
                    PropertyChangedEventHandler @event = (s, e) => PropertyGrid2.Refresh();
                    if (PropertyGrid2.SelectedObject is BaseProperties viewModelBase)
                        viewModelBase.PropertyChanged -= @event;
                    PropertyGrid2.SelectedObject = drawingVisual.BaseAttribute;
                    drawingVisual.BaseAttribute.PropertyChanged += @event;

                    if (ImageViewModel.ImageEditMode == true)
                    {
                        if (ImageViewModel.SelectDrawingVisuals != null && drawingVisual is DrawingVisual visual1 && ImageViewModel.SelectDrawingVisuals.Contains(visual1))
                            return;

                        if (drawingVisual is DrawingVisual visual)
                        {
                            ImageViewModel.SelectDrawingVisual = visual;
                            if (!ImageViewModel.SelectEditorVisual.GetContainingRect(MouseDownP))
                                Zoombox1.Cursor = Cursors.Cross;
                        }

                        if (ImageViewModel.SelectDrawingVisuals != null)
                        {
                            foreach (var item in ImageViewModel.SelectDrawingVisuals)
                            {
                                if (item is IDrawingVisual id)
                                {
                                    id.Pen.Brush = Brushes.Red;
                                    id.Render();
                                }
                            }
                            ImageViewModel.SelectDrawingVisuals = null;
                        }
                    }
                    return;
                }

                SelectDrawingVisualsClear();
                ImageViewModel.SelectDrawingVisual = null;

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

                if (ImageViewModel.DrawPolygon)
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

                    if (ImageViewModel.DrawCircle && DrawCircleCache !=null)
                    {
                        double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                        DrawCircleCache.Attribute.Radius = Radius;
                        DrawCircleCache.Render();
                    }
                    else if (ImageViewModel.DrawRect && DrawingRectangleCache!=null)
                    {
                        DrawingRectangleCache.Attribute.Rect = new System.Windows.Rect(MouseDownP, point);
                        DrawingRectangleCache.Render();
                    }
                    else if (ImageViewModel.DrawPolygon)
                    {

                    }
                    if (ImageViewModel.SelectEditorVisual.SelectVisual != null)
                    {
                        if (Zoombox1.Cursor == Cursors.SizeAll)
                        {
                            var oldRect = ImageViewModel.SelectEditorVisual.Rect;
                            var deltaX = point.X - LastMouseMove.X;
                            var deltaY = point.Y - LastMouseMove.Y;
                            // 移动选择的区域
                            ImageViewModel.SelectEditorVisual.Rect = new System.Windows.Rect(
                                oldRect.X + deltaX,
                                oldRect.Y + deltaY,
                                oldRect.Width,
                                oldRect.Height
                            );
                        }
                        else if (Zoombox1.Cursor == Cursors.SizeNWSE || Zoombox1.Cursor == Cursors.SizeNESW)
                        {
                            Point point1 = ImageViewModel.SelectEditorVisual.OldRect.TopLeft;
                            ImageViewModel.SelectEditorVisual.Rect = new System.Windows.Rect(ImageViewModel.SelectEditorVisual.FixedPoint, point);
                        }
                        else if (Zoombox1.Cursor == Cursors.SizeNS)
                        {
                            Point point1 = ImageViewModel.SelectEditorVisual.FixedPoint1;
                            point1.Y = point.Y;
                            ImageViewModel.SelectEditorVisual.Rect = new System.Windows.Rect(ImageViewModel.SelectEditorVisual.FixedPoint, point1);
                        }
                        else if (Zoombox1.Cursor == Cursors.SizeWE)
                        {
                            Point point1 = ImageViewModel.SelectEditorVisual.FixedPoint1;
                            point1.X = point.X;
                            ImageViewModel.SelectEditorVisual.Rect = new System.Windows.Rect(ImageViewModel.SelectEditorVisual.FixedPoint, point1);
                        }
                        ImageViewModel.SelectEditorVisual.SetRect();
                    }

                    if (ImageViewModel.SelectDrawingVisuals != null)
                    {
                        foreach (var item in ImageViewModel.SelectDrawingVisuals)
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
                else
                {
                    if (!(drawCanvas.GetVisual(MouseDownP) == ImageViewModel.SelectEditorVisual && ImageViewModel.SelectEditorVisual.GetContainingRect(point)))
                        Zoombox1.Cursor = Cursors.Cross;
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

                    if (PoiConfig.IsUserDraw)
                    {
                        if (PoiConfig.IsAreaCircle && ImageViewModel.DrawCircle)
                        {
                            PoiConfig.CenterX = (int)DrawCircleCache.Attribute.Center.X;
                            PoiConfig.CenterY = (int)DrawCircleCache.Attribute.Center.Y;
                            PoiConfig.AreaCircleRadius = (int)DrawCircleCache.Attribute.Radius;
                            drawCanvas.RemoveVisual(DrawCircleCache);
                        }

                        if (PoiConfig.IsAreaRect && ImageViewModel.DrawRect)
                        {
                            PoiConfig.CenterX = (int)(DrawingRectangleCache.Attribute.Rect.Width / 2 + DrawingRectangleCache.Attribute.Rect.X);
                            PoiConfig.CenterY = (int)(DrawingRectangleCache.Attribute.Rect.Height / 2 + DrawingRectangleCache.Attribute.Rect.Y);
                            PoiConfig.AreaRectWidth = (int)DrawingRectangleCache.Attribute.Rect.Width;
                            PoiConfig.AreaRectHeight = (int)DrawingRectangleCache.Attribute.Rect.Height;
                            drawCanvas.RemoveVisual(DrawingRectangleCache);
                        }
                        RenderPoiConfig();
                    }

                    if (drawCanvas.GetVisual(MouseUpP) is not DrawingVisual dv || ImageViewModel.SelectDrawingVisuals == null || !ImageViewModel.SelectDrawingVisuals.Contains(dv))
                        SelectDrawingVisualsClear();

                    if (drawCanvas.ContainsVisual(SelectRect))
                    {
                        if (ImageViewModel.EraseVisual)
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
                            ImageViewModel.SelectDrawingVisuals = drawCanvas.GetVisuals(new RectangleGeometry(new System.Windows.Rect(MouseDownP, MouseUpP)));
                            foreach (var item in ImageViewModel.SelectDrawingVisuals)
                            {
                                if (item is IDrawingVisual drawingVisual)
                                {
                                    drawingVisual.Pen.Brush = Brushes.Yellow;
                                    drawingVisual.Render();
                                }
                            }

                            if (ImageViewModel.SelectDrawingVisuals.Count == 0)
                                ImageViewModel.SelectDrawingVisuals = null;
                        }

                        drawCanvas.RemoveVisual(SelectRect,false);
                    }


                    if (ImageViewModel.DrawPolygon && DrawingPolygonCache != null)
                    {
                        DrawingPolygonCache.Points.Add(MouseUpP);
                        DrawingPolygonCache.MovePoints = null;
                        DrawingPolygonCache.Render();
                    }
                    else if (ImageViewModel.DrawCircle && DrawCircleCache != null)
                    {
                        DrawCircleCache.Render();
                        PropertyGrid2.SelectedObject = DrawCircleCache.BaseAttribute;

                        DrawCircleCache.AutoAttributeChanged = true;

                        ListView1.ScrollIntoView(DrawCircleCache);
                        ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawCircleCache);

                        PoiConfig.DefalutRadius = DrawCircleCache.Attribute.Radius;

                    }
                    else if (ImageViewModel.DrawRect)
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
                    if (ImageViewModel.SelectDrawingVisual != null)
                    {
                        if (ImageViewModel.SelectDrawingVisual is IRectangle rectangle)
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
                        else if (ImageViewModel.SelectDrawingVisual is ICircle Circl)
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
