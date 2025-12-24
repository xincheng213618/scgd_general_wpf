
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Rasterized;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Ghost
{
    public class  ViewHandleGhost : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Ghost};

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            var ViewResults = result.ViewResults.ToSpecificViewResults<AlgResultGhostModel>();
            var csvBuilder = new StringBuilder();
            List<string> headers = new List<string>();
            headers.Add("id");
            headers.Add("质心坐标");
            headers.Add("光斑灰度");
            headers.Add("鬼影灰度");
            csvBuilder.AppendLine(string.Join(",", headers));

            foreach (var item in ViewResults)
            {
                List<string> content = new List<string>();
                content.Add(EscapeCsvField(item.Id.ToString()));
                content.Add(EscapeCsvField(item.LEDCenters));
                content.Add(EscapeCsvField(item.LEDBlobGray));
                content.Add(EscapeCsvField(item.GhostAverageGray));
                csvBuilder.AppendLine(string.Join(",", content));
            }
            csvBuilder.AppendLine();
            csvBuilder.AppendLine();
            File.AppendAllText(selectedPath, csvBuilder.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',' ) || field.Contains('"') || field.Contains('\n'))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }
            return field;
        }
        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<AlgResultGhostModel> AlgResultGhostModels = AlgResultGhostDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultGhostModels)
                {
                    result.ViewResults.Add(item);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmGhost), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);



            if (result.ViewResults.Count != 0 && result.ViewResults[0] is AlgResultGhostModel viewResultGhost)
            {
                try
                {
                    List<Point1> generatedPointsGhostPixel = new List<Point1>();
                    if (viewResultGhost.GhostPixel !=null)
                        foreach (var item in viewResultGhost.GhostPixel)
                            foreach (var item1 in item)
                                generatedPointsGhostPixel.Add(item1);
                    Draw(generatedPointsGhostPixel);
                    List<Point1> generatedPointsLedPixel = new List<Point1>();
                    if (viewResultGhost.LedPixel !=null)
                        foreach (var item in viewResultGhost.LedPixel)
                            foreach (var item1 in item)
                                generatedPointsLedPixel.Add(item1);
                    Draw(generatedPointsLedPixel);
                    void Draw(List<Point1> generatedPoints)
                    {
                        // 2. 获取全局画布尺寸（假设 DrawCanvas.ActualWidth/ActualHeight）
                        int canvasWidth = (int)Math.Ceiling(ctx.ImageView.ActualWidth);
                        int canvasHeight = (int)Math.Ceiling(ctx.ImageView.ActualHeight);
                        if (canvasWidth == 0 || canvasHeight == 0) return;
                        var fullRect = new Rect(0, 0, canvasWidth, canvasHeight);
                        // 3. 新建全局大图
                        var rtb = new RenderTargetBitmap(canvasWidth, canvasHeight, 144, 144, PixelFormats.Pbgra32);

                        // 4. 渲染所有选中的Visual到全局
                        var dv = new DrawingVisual();
                        using (var dc = dv.RenderOpen())
                        {
                            for (int i = 0; i < generatedPoints.Count; i++)
                            {
                                var point = generatedPoints[i];
                                RectangleProperties rectangleTextProperties = new RectangleProperties();
                                rectangleTextProperties.Rect = new Rect(point.X, point.Y, 1, 1);
                                rectangleTextProperties.Brush = Brushes.Transparent;
                                rectangleTextProperties.Pen = new Pen(Brushes.Red, 1);
                                rectangleTextProperties.Id = i;
                                rectangleTextProperties.Name = i.ToString();
                                DVRectangle Rectangle = new DVRectangle(rectangleTextProperties);
                                Rectangle.Render();
                                dc.DrawDrawing(Rectangle.Drawing);
                            }
                        }

                        rtb.Render(dv);
                        var rasterVisual = new RasterizedSelectVisual(rtb, fullRect);
                        rasterVisual.Attribute.Tag = generatedPoints;
                        ctx.ImageView.ImageShow.AddVisualCommand(rasterVisual);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            List<string> header = new() { "质心坐标", "光斑灰度", "鬼影灰度" };
            List<string> bdHeader = new() { "LEDCenters", "LEDBlobGray", "GhostAverageGray" };

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }



    }
}
