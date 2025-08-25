#pragma warning disable CS8604,CS8603
using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.Media;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.FileIO;
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
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class ViewHandleBuildPoiFile : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.BuildPOI_File};

        public static void CovertPoiParam(PoiParam poiParam ,string fileName)
        {
            var poiInfo = ReadPOIPointFromCSV(fileName);
            poiParam.PoiPoints.Clear();
            foreach (var item in poiInfo.Positions)
            {
                poiParam.PoiPoints.Add(new PoiPoint() { PixX = item.PixelX, PixY = item.PixelY ,PointType = (RiPointTypes)poiInfo.HeaderInfo.PointType ,PixWidth = poiInfo .HeaderInfo.Width, PixHeight = poiInfo.HeaderInfo.Height });
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

        public override void Load(AlgorithmView view, AlgorithmResult result)
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


        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {

            if (result.ViewResults.Count > 0 && result.ViewResults[0] is PoiCieFileModel model)
            {
                POIPointInfo pointinfo = ReadPOIPointFromCSV(model.FileUrl);
                int[] ints = new int[pointinfo.Positions.Count * 2];
                for (int i = 0; i < pointinfo.Positions.Count; i++)
                {
                    ints[2 * i] = (int)pointinfo.Positions[i].PixelX;
                    ints[2 * i + 1] = (int)pointinfo.Positions[i].PixelY;
                }
                if (File.Exists(result.FilePath))
                {
                    HImage hImage;
                    if (CVFileUtil.IsCIEFile(result.FilePath))
                    {
                        CVCIEFile cVCIEFile = new NetFileUtil().OpenLocalCVFile(result.FilePath);
                        hImage = cVCIEFile.ToWriteableBitmap().ToHImage();

                    }
                    else
                    {
                        BitmapImage bitmapImage = new BitmapImage(new Uri(result.FilePath));
                        hImage = bitmapImage.ToHImage();
                    }
                    int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, (int)pointinfo.HeaderInfo.Height, ints, ints.Length, 1);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (ret == 0)
                        {
                            var image = hImageProcessed.ToWriteableBitmap();

                            OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                            hImageProcessed.pData = IntPtr.Zero;
                            view.ImageView.FunctionImage = image;
                            view.ImageView.ImageShow.Source = view.ImageView.FunctionImage;
                        }
                    });

                }


            }

            var header = new List<string> { "id", "file_name", "file_url", "fileType" };
            var bdHeader = new List<string> { "Id", "FileName", "FileUrl", "file_type" };
            if (view.listViewSide.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }

        }
    }
}
