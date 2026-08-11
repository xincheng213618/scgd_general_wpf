#pragma warning disable CA1725,CS8603,CS8604
using ColorVision.Common.MVVM;
using ImageUtils = ColorVision.Common.Utilities.ImageUtils;
using ColorVision.Engine.Services;
using ColorVision.Database;
using ColorVision.ImageEditor.Draw;
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
                poiParam.PoiPoints.Add(new PoiPoint() { PixX = item.PixelX, PixY = item.PixelY, PointType = poiInfo.HeaderInfo.PointType.ToPoiShape(), PixWidth = poiInfo.HeaderInfo.Width, PixHeight = poiInfo.HeaderInfo.Height });
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
                poiInfo.HeaderInfo = new POIHeaderInfo() { Height = (int)poiParam.PoiPoints[0].PixHeight, Width = (int)poiParam.PoiPoints[0].PixWidth, PointType = poiParam.PoiPoints[0].PointType.ToAlgorithmPoiShape() };
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

        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiCieFileModel> models = PoiCieFileDao.Instance.GetAllByPid(result.Id);
                foreach (var item in models)
                {
                    result.ViewResults.Add(item);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmBuildPoi), ImageFilePath = result.FilePath })) });

            }
        }


        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.Count > 0 && result.ViewResults[0] is PoiCieFileModel model)
            {
                POIPointInfo pointinfo = ReadPOIPointFromCSV(model.FileUrl);
                if (pointinfo?.HeaderInfo != null && pointinfo.Positions != null)
                {
                    VectorizedSelectVisual? visual = CreatePoiVisual(pointinfo.Positions, pointinfo.HeaderInfo, ctx.ImageView.ImageShow.Source);
                    if (visual != null)
                        ctx.ImageView.ImageShow.AddVisualCommand(visual);
                }

            }

            var header = new List<string> { "id", "file_name", "file_url", "fileType" };
            var bdHeader = new List<string> { "Id", "FileName", "FileUrl", "file_type" };
            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }

        }

        internal static VectorizedSelectVisual? CreatePoiVisual(
            IReadOnlyList<POIPointPosition> positions,
            POIHeaderInfo headerInfo,
            ImageSource? imageSource)
        {
            ArgumentNullException.ThrowIfNull(positions);
            ArgumentNullException.ThrowIfNull(headerInfo);
            if (positions.Count == 0)
                return null;

            double markerWidth = headerInfo.Width;
            double markerHeight = headerInfo.Height;
            StreamGeometry geometry = new();
            using (StreamGeometryContext geometryContext = geometry.Open())
            {
                foreach (POIPointPosition point in positions)
                {
                    geometryContext.BeginFigure(new Point(point.PixelX, point.PixelY), isFilled: true, isClosed: true);
                    geometryContext.LineTo(new Point(point.PixelX + markerWidth, point.PixelY), isStroked: true, isSmoothJoin: false);
                    geometryContext.LineTo(new Point(point.PixelX + markerWidth, point.PixelY + markerHeight), isStroked: true, isSmoothJoin: false);
                    geometryContext.LineTo(new Point(point.PixelX, point.PixelY + markerHeight), isStroked: true, isSmoothJoin: false);
                }
            }
            geometry.Freeze();

            GeometryDrawing drawing = new(Brushes.Transparent, new Pen(Brushes.Red, 1), geometry);
            drawing.Freeze();

            VectorizedSelectVisual visual = new(drawing, GetPoiVisualBounds(positions, imageSource, markerWidth, markerHeight));
            visual.Attribute.Tag = positions;
            return visual;
        }

        private static Rect GetPoiVisualBounds(
            IReadOnlyList<POIPointPosition> positions,
            ImageSource? imageSource,
            double markerWidth,
            double markerHeight)
        {
            if (ImageUtils.TryGetImageSize(imageSource, out int width, out int height))
                return new Rect(0, 0, width, height);

            double minX = positions.Min(point => Math.Min(point.PixelX, point.PixelX + markerWidth));
            double minY = positions.Min(point => Math.Min(point.PixelY, point.PixelY + markerHeight));
            double maxX = positions.Max(point => Math.Max(point.PixelX, point.PixelX + markerWidth));
            double maxY = positions.Max(point => Math.Max(point.PixelY, point.PixelY + markerHeight));
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
