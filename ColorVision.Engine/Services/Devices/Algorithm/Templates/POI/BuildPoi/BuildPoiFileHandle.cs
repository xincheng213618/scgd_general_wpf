using ColorVision.Engine.Media;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Net;
using CsvHelper;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.BuildPoi
{
    public class BuildPoiFileHandle : IResultHandle
    {
        public AlgorithmResultType ResultType => AlgorithmResultType.BuildPOI_File;
        private static POIPointInfo ReadPOIPointFromCSV(string fileName)
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
        public void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();
            List<string> header = new();
            List<string> bdHeader = new();

            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiCieFileModel> models = PoiCieFileDao.Instance.GetAllByPid(result.Id);
                foreach (var item in models)
                {
                    result.ViewResults.Add(item);
                }
                if (models.Count > 0)
                {
                    POIPointInfo pointinfo = ReadPOIPointFromCSV(models[0].FileUrl);
                    int[] ints = new int[pointinfo.Positions.Count*2];
                    for (int i = 0; i < pointinfo.Positions.Count; i++)
                    {
                        ints[2*i] = pointinfo.Positions[i].PixelX;
                        ints[2*i+1] = pointinfo.Positions[i].PixelY;
                    }
                    if (File.Exists(result.FilePath))
                    {
                        HImage hImage;
                        if (CVFileUtil.IsCIEFile(result.FilePath))
                        {
                            CVCIEFile cVCIEFile = new NetFileUtil().OpenLocalCVFile(result.FilePath);
                            hImage = cVCIEFile.ToWriteabl  eBitmap().ToHImage();

                        }
                        else
                        {
                            BitmapImage bitmapImage = new BitmapImage(new Uri(result.FilePath));
                            hImage = bitmapImage.ToHImage();
                        }
                        int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, pointinfo.HeaderInfo.Height, ints, ints.Length);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (ret == 0)
                            {
                                var image = hImageProcessed.ToWriteableBitmap();

                                OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                hImageProcessed.pData = IntPtr.Zero;
                                view.ImageView.PseudoImage = image;
                                view.ImageView.ImageShow.Source = view.ImageView.PseudoImage;
                            }
                        });

                    }


                }


                header = new List<string> { "id", "file_name", "file_url", "fileType" };
                bdHeader = new List<string> { "Id", "FileName", "FileUrl", "file_type" };
            }
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
