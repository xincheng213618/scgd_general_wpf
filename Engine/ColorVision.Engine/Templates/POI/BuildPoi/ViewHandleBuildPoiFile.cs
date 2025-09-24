#pragma warning disable CS8604,CS8603
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Rasterized;
using CsvHelper;
using CVCommCore.CVAlgorithm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class ViewHandleBuildPoiFile : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.BuildPOI_File};

        public static void CovertPoiParam(PoiParam poiParam ,string fileName)
        {
            var poiInfo = ReadPOIPointFromCSV(fileName);
            poiParam.PoiPoints.Clear();
            foreach (var item in poiInfo.Positions)
            {
                poiParam.PoiPoints.Add(new PoiPoint() { PixX = item.PixelX, PixY = item.PixelY ,PointType = (GraphicTypes)poiInfo.HeaderInfo.PointType ,PixWidth = poiInfo .HeaderInfo.Width, PixHeight = poiInfo.HeaderInfo.Height });
            }
            poiParam.PoiConfig.AreaRectRow = poiInfo.HeaderInfo.Rows;
            poiParam.PoiConfig.AreaRectCol = poiInfo.HeaderInfo.Cols;
        }
        public static void CoverFile(PoiParam poiParam, string fileName)
        {
            POIPointInfo poiInfo = new POIPointInfo();
            poiInfo.Positions = new List<POIPointPosition>();
            if (poiParam.PoiPoints.Count <= 0)
            {
                poiInfo.HeaderInfo = new POIHeaderInfo() { Height = (int)poiParam.PoiPoints[0].PixHeight, Width = (int)poiParam.PoiPoints[0].PixWidth, PointType = (POIPointTypes)poiParam.PoiPoints[0].PointType };
            }
            else
            {
                poiInfo.HeaderInfo = new POIHeaderInfo();
            }
            if (poiParam.PoiConfig.IsAreaRect)
            {
                poiInfo.HeaderInfo.Rows = poiParam.PoiConfig.AreaRectRow;
                poiInfo.HeaderInfo.Cols = poiParam.PoiConfig.AreaRectCol;
            }
            if (poiParam.PoiConfig.IsAreaMask)
            {
                poiInfo.HeaderInfo.Rows = poiParam.PoiConfig.AreaPolygonRow;
                poiInfo.HeaderInfo.Cols = poiParam.PoiConfig.AreaPolygonCol;
            }

            foreach (var item in poiParam.PoiPoints)
            {
                poiInfo.Positions.Add(new POIPointPosition() { PixelX = (int)item.PixX, PixelY = (int)item.PixY });
            }
            POIPointToCSV(fileName, poiInfo);
        }

        public static void POIPointToCSV(string fileName, POIPointInfo poiInfo)
        {
            using (var writer = new StreamWriter(fileName))
            {
                using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csvWriter.WriteHeader<POIHeaderInfo>();
                    csvWriter.NextRecord();
                    csvWriter.WriteRecord(poiInfo.HeaderInfo);
                    csvWriter.NextRecord();
                    csvWriter.WriteHeader<POIPointPosition>();
                    csvWriter.NextRecord();
                    csvWriter.WriteRecords(poiInfo.Positions);
                }
            }
        }

        public static POIPointInfo ReadPOIPointFromCSV(string fileName)
        {
            POIPointInfo poiInfo = null;
            using (var reader = new StreamReader(fileName))
            {
                using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    POIHeaderInfo info = null;
                    if (csvReader.Read())
                    {
                        info = csvReader.GetRecord<POIHeaderInfo>();
                        if (csvReader.Read() && csvReader.ReadHeader())
                        {
                            var pois = csvReader.GetRecords<POIPointPosition>().ToList();
                            if (pois != null && pois.Count > 0)
                            {
                                poiInfo = new POIPointInfo() { HeaderInfo = info, Positions = pois };
                            }
                        }
                    }
                }
            }
            return poiInfo;
        }

        public override void Load(IViewImageA view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiCieFileModel> models = PoiCieFileDao.Instance.GetAllByPid(result.Id);
                foreach (var item in models)
                {
                    result.ViewResults.Add(item);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmBuildPoi), ImageFilePath = result.FilePath })) });

            }
        }


        public override void Handle(IViewImageA view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.Count > 0 && result.ViewResults[0] is PoiCieFileModel model)
            {
                POIPointInfo pointinfo = ReadPOIPointFromCSV(model.FileUrl);

                if (File.Exists(result.FilePath))
                {
                    // 2. 获取全局画布尺寸（假设 DrawCanvas.ActualWidth/ActualHeight）
                    int canvasWidth = (int)Math.Ceiling(view.ImageView.ActualWidth);
                    int canvasHeight = (int)Math.Ceiling(view.ImageView.ActualHeight);
                    if (canvasWidth == 0 || canvasHeight == 0) return;
                    var fullRect = new Rect(0, 0, canvasWidth, canvasHeight);
                    // 3. 新建全局大图
                    var rtb = new RenderTargetBitmap(canvasWidth, canvasHeight, 144, 144, PixelFormats.Pbgra32);

                    // 4. 渲染所有选中的Visual到全局
                    var dv = new DrawingVisual();
                    using (var dc = dv.RenderOpen())
                    {
                        for (int i = 0; i < pointinfo.Positions.Count; i++)
                        {
                            var point = pointinfo.Positions[i];
                            RectangleProperties rectangleTextProperties = new RectangleProperties();
                            rectangleTextProperties.Rect = new Rect(point.PixelX, point.PixelY, pointinfo.HeaderInfo.Width, pointinfo.HeaderInfo.Height);
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
                    rasterVisual.Attribute.Tag = pointinfo.Positions;
                    view.ImageView.ImageShow.AddVisualCommand(rasterVisual);
                }

            }

            var header = new List<string> { "id", "file_name", "file_url", "fileType" };
            var bdHeader = new List<string> { "Id", "FileName", "FileUrl", "file_type" };
            if (view.ListView.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.ListView.ItemsSource = result.ViewResults;
            }

        }
    }
}
