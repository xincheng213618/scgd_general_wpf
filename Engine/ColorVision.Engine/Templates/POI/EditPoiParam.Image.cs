#pragma warning disable CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static ColorVision.ImageEditor.Draw.SelectEditorVisual;

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
            var DrawingVisual = ImageShow.GetVisual<Visual>(Point);

            //if (DrawingVisual != null && ImageViewModel.SelectEditorVisual.SelectVisual != DrawingVisual && DrawingVisual is IDrawingVisual drawing)
            //{
            //    var ContextMenu = new ContextMenu();

            //    MenuItem menuItem = new() { Header = "隐藏(_H)" };
            //    menuItem.Click += (s, e) =>
            //    {
            //        drawing.BaseAttribute.IsShow = false;
            //    };
            //    MenuItem menuIte2 = new() { Header = "删除(_D)" };

            //    menuIte2.Click += (s, e) =>
            //    {
            //        ImageShow.RemoveVisual(DrawingVisual);
            //        PropertyGrid2.SelectedObject = null;
            //    };
            //    ContextMenu.Items.Add(menuItem);
            //    ContextMenu.Items.Add(menuIte2);
            //    ImageShow.ContextMenu = ContextMenu;
            //}
            //else
            //{
            //    ImageShow.ContextMenu = null;
            //}

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
                DrawingPolygonCache.Render();
                DrawingPolygonCache = null;
            }
            IsRightButtonDown = true;
        }


        public int CheckNo()
        {
            if (DrawingVisualLists.Count > 0 && DrawingVisualLists.Last() is DrawingVisualBase drawingVisual)
            {
                return drawingVisual.ID + 1;
            }
            else
            {
                return 1;
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

                if (ImageViewModel.SelectEditorVisual.GetContainingRect(MouseDownP))
                {
                    return;
                }
                else
                {
                    ImageViewModel.SelectEditorVisual.ClearRender();
                }

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
                    return;
                }

                if (ImageViewModel.DrawCircle)
                {
                    int id = CheckNo();

                    DrawCircleCache = new DVCircleText() { };
                    DrawCircleCache.Attribute.Id = id;
                    DrawCircleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawCircleCache.Attribute.Center = MouseDownP;
                    DrawCircleCache.Attribute.Radius = PoiConfig.DefalutRadius;
                    DrawCircleCache.Attribute.Text = "Point_" + id.ToString();

                    drawCanvas.AddVisual(DrawCircleCache);

                    ImageViewModel.SelectEditorVisual.ClearRender();
                    return;
                }
                if (ImageViewModel.DrawRect)
                {
                    int id = CheckNo();

                    DrawingRectangleCache = new DVRectangleText();
                    DrawingRectangleCache.Attribute.Id = id;

                    if (PoiConfig.UseCenter)
                    {
                        DrawingRectangleCache.Attribute.Rect = new System.Windows.Rect(new Point(MouseDownP.X + PoiConfig.DefalutWidth/2, MouseDownP.Y + PoiConfig.DefalutHeight/2), new Point(MouseDownP.X - PoiConfig.DefalutWidth/2, MouseDownP.Y - PoiConfig.DefalutHeight/2));
                    }
                    else
                    {
                        DrawingRectangleCache.Attribute.Rect = new System.Windows.Rect(MouseDownP, new Point(MouseDownP.X + PoiConfig.DefalutWidth, MouseDownP.Y + PoiConfig.DefalutHeight));
                    }

                    DrawingRectangleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawingRectangleCache.Attribute.Text = "Point_" + id.ToString();

                    drawCanvas.AddVisual(DrawingRectangleCache);
                    ImageViewModel.SelectEditorVisual.ClearRender();
                    return;
                }
                if (ImageViewModel.DrawPolygon)
                {
                    if (DrawingPolygonCache == null)
                    {
                        DrawingPolygonCache = new DVPolygon();
                        DrawingPolygonCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                        DrawingPolygonCache.Points.Add(MouseDownP);
                        DrawingPolygonCache.Points.Add(MouseDownP);
                        drawCanvas.AddVisual(DrawingPolygonCache);
                    }
                    ImageViewModel.SelectEditorVisual.ClearRender();
                    return;
                }


                var MouseVisual = drawCanvas.GetVisual<Visual>(MouseDownP);
                if (MouseVisual == ImageViewModel.SelectEditorVisual)
                    return;
                if (MouseVisual is IDrawingVisual drawingVisual)
                {
                    ListView1.SelectedValue = drawingVisual;
                    ListView1.ScrollIntoView(drawingVisual);
                    PropertyGrid2.SelectedObject = drawingVisual.BaseAttribute;
                    if (ImageViewModel.ImageEditMode == true)
                    {
                        if (drawingVisual is ISelectVisual visual)
                        {
                            if (ImageViewModel.SelectEditorVisual.SelectVisuals.Contains(visual))
                            {
                                return;
                            }
                            else
                            {
                                ImageViewModel.SelectEditorVisual.SetRender(visual);
                                if (!ImageViewModel.SelectEditorVisual.GetContainingRect(MouseDownP))
                                    Zoombox1.Cursor = Cursors.Cross;
                            }
                        }
                        else
                        {
                            ImageViewModel.SelectEditorVisual.ClearRender();
                        }
                    }
                    return;
                }
                ImageViewModel.SelectEditorVisual.ClearRender();
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
                        DrawingPolygonCache.Points.RemoveAt(DrawingPolygonCache.Points.Count - 1);
                        DrawingPolygonCache.Points.Add(point);
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

                    if (ImageViewModel.SelectEditorVisual.SelectVisuals.Count != 0)
                    {
                        if (Zoombox1.Cursor == Cursors.SizeAll)
                        {
                            foreach (var selectVisual in ImageViewModel.SelectEditorVisual.SelectVisuals)
                            {
                                var oldRect = selectVisual.GetRect(); ;
                                var deltaX = point.X - LastMouseMove.X;
                                var deltaY = point.Y - LastMouseMove.Y;

                                // 移动选择的区域
                                Rect rect = new System.Windows.Rect(
                                   oldRect.X + deltaX,
                                   oldRect.Y + deltaY,
                                   oldRect.Width,
                                   oldRect.Height
                               );
                                selectVisual.SetRect(rect);
                            }
                            ImageViewModel.SelectEditorVisual.Render();

                        }
                        //这里明天在优化
                        if (ImageViewModel.SelectEditorVisual.SelectVisuals.Count == 1)
                        {
                            if (Zoombox1.Cursor == Cursors.SizeNWSE || Zoombox1.Cursor == Cursors.SizeNESW)
                            {
                                foreach (var selectVisual in ImageViewModel.SelectEditorVisual.SelectVisuals)
                                {
                                    if (!ImageViewModel.SelectEditorVisual.Cache.TryGetValue(selectVisual, out CacheClass cache)) continue;

                                    var oldRect = selectVisual.GetRect(); ;
                                    Point point1 = oldRect.TopLeft;
                                    Point FixedPoint = ImageViewModel.SelectEditorVisual.Cache[selectVisual].FixedPoint;

                                    Rect rect = new System.Windows.Rect(FixedPoint, point);
                                    selectVisual.SetRect(rect);
                                }
                                ImageViewModel.SelectEditorVisual.Render(); ;
                            }
                            else if (Zoombox1.Cursor == Cursors.SizeNS)
                            {
                                foreach (var selectVisual in ImageViewModel.SelectEditorVisual.SelectVisuals)
                                {
                                    if (!ImageViewModel.SelectEditorVisual.Cache.TryGetValue(selectVisual, out CacheClass cache)) continue;

                                    var oldRect = selectVisual.GetRect();
                                    Point point1 = ImageViewModel.SelectEditorVisual.Cache[selectVisual].FixedPoint1;
                                    point1.Y = point.Y;

                                    Rect rect = new System.Windows.Rect(ImageViewModel.SelectEditorVisual.Cache[selectVisual].FixedPoint, point1);
                                    selectVisual.SetRect(rect);
                                }
                                ImageViewModel.SelectEditorVisual.Render();
                            }
                            else if (Zoombox1.Cursor == Cursors.SizeWE)
                            {
                                foreach (var selectVisual in ImageViewModel.SelectEditorVisual.SelectVisuals)
                                {
                                    if (!ImageViewModel.SelectEditorVisual.Cache.TryGetValue(selectVisual, out CacheClass cache)) continue;
                                    var oldRect = selectVisual.GetRect();


                                    Point point1 = ImageViewModel.SelectEditorVisual.Cache[selectVisual].FixedPoint1;
                                    point1.X = point.X;

                                    Rect rect = new System.Windows.Rect(ImageViewModel.SelectEditorVisual.Cache[selectVisual].FixedPoint, point1);
                                    selectVisual.SetRect(rect);
                                }
                                ImageViewModel.SelectEditorVisual.Render();
                            }
                        }

                    }
                }
                else
                {
                    if (!(drawCanvas.GetVisual<Visual>(MouseDownP) == ImageViewModel.SelectEditorVisual && ImageViewModel.SelectEditorVisual.GetContainingRect(point)))
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

                    if (drawCanvas.GetVisual<Visual>(MouseUpP) is not IDrawingVisual dv || !ImageViewModel.SelectEditorVisual.Contains(MouseUpP))
                        ImageViewModel.SelectEditorVisual.ClearRender();

                    if (drawCanvas.ContainsVisual(SelectRect))
                    {
                        var List = drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP)));

                        ImageViewModel.SelectEditorVisual.SetRenders(List.Cast<ISelectVisual>());

                        drawCanvas.RemoveVisual(SelectRect, false);
                    }


                    if (ImageViewModel.DrawPolygon && DrawingPolygonCache != null)
                    {
                        DrawingPolygonCache.Points.RemoveAt(DrawingPolygonCache.Points.Count - 1);
                        DrawingPolygonCache.Points.Add(MouseUpP);
                        DrawingPolygonCache.Render();
                    }
                    else if (ImageViewModel.DrawCircle && DrawCircleCache != null)
                    {
                        DrawCircleCache.Render();

                        ListView1.ScrollIntoView(DrawCircleCache);
                        ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawCircleCache);

                        PoiConfig.DefalutRadius = DrawCircleCache.Attribute.Radius;
                        ImageViewModel.SelectEditorVisual.SetRender(DrawCircleCache);

                    }
                    else if (ImageViewModel.DrawRect)
                    {
                        DrawingRectangleCache.Render();


                        ListView1.ScrollIntoView(DrawingRectangleCache);
                        ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawingRectangleCache);

                        PoiConfig.DefalutWidth = DrawingRectangleCache.Attribute.Rect.Width;
                        PoiConfig.DefalutHeight = DrawingRectangleCache.Attribute.Rect.Height;
                        ImageViewModel.SelectEditorVisual.SetRender(DrawingRectangleCache)  ;
                    }
                    drawCanvas.ReleaseMouseCapture();
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
