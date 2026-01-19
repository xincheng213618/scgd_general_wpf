#pragma warning disable CS8602

using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using log4net;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using SqlSugar;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
    public class Compliance_Math: IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Compliance_Math));
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Compliance_Math };
        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                //result.ViewResults = new ObservableCollection<IViewResult>();
                //using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                //var list = db.Queryable<AlgResultPoiCieFileModel>().Where(it => it.Pid == result.Id).ToList();

                //foreach (var item in list)
                //{
                //    result.ViewResults.Add(item);
                //}
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.ResultImagFile))
                ctx.ImageView.OpenImage(result.ResultImagFile);

        }
    }



    public class ResultOLED_RebuildPixelsMem : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ResultOLED_RebuildPixelsMem));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.OLED_FindDotsArrayMem_File, ViewResultAlgType.OLED_FindDotsArrayMem, ViewResultAlgType.OLED_RebuildPixelsMem , ViewResultAlgType.OLED_FindDotsArrayByCornerPts_File, ViewResultAlgType.OLED_FindDotsArrayOutFile };

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string filePath = Path.Combine(selectedPath, $"{result.Batch}{result.ResultType}_LEDStripV2.csv");

            //var viewResults = result.ViewResults.ToSpecificViewResults<LEDStripDetailViewResult>();
            //var csvBuilder = new StringBuilder();
            //csvBuilder.AppendLine("name,physicalLength,pixLength,x1,y1,x2,y2");
            //if (viewResults.Count == 1)
            //{
            //    var items = viewResults[0].LEDStripResult?.Result;
            //    if (items != null)
            //    {
            //        foreach (var item in items)
            //        {
            //            csvBuilder.AppendLine($"{item.Name},{item.PhysicalLength},{item.PixLength},{item.EndPoints[0].X},{item.EndPoints[0].Y},{item.EndPoints[1].X},{item.EndPoints[1].Y}");
            //        }
            //    }
            //    File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
            //}
        }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var list  = db.Queryable<AlgResultPoiCieFileModel>().Where(it => it.Pid == result.Id).ToList();

                foreach (var item in list)
                {
                    result.ViewResults.Add(item);
                }

            }
        }
         
        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if(result.ResultType == ViewResultAlgType.OLED_FindDotsArrayOutFile)
            {
                string filename = "D:\\CvMainwindows\\CVWindowsService\\debug\\position.tif";
                if (File.Exists(filename))
                {
                    ctx.ImageView.OpenImage(filename);
                }
            }
            else
            {
                var AlgResultPoiCieFileModel = result.ViewResults.OfType<AlgResultPoiCieFileModel>().FirstOrDefault();
                if (AlgResultPoiCieFileModel != null && AlgResultPoiCieFileModel.FileUrl != null)
                {
                    string originalPath = AlgResultPoiCieFileModel.FileUrl;

                    if (originalPath.Contains("Y.tif"))
                    {
                        // 1. 读取图像 (使用 AnyDepth 确保能读取 16位 TIF, 使用 Grayscale 确保按灰度读取)
                        using (Mat src = Cv2.ImRead(originalPath, ImreadModes.Unchanged))
                        using (Mat dst8Bit = new Mat())
                        {
                            if (!src.Empty())
                            {
                                if (src.Depth() == MatType.CV_32F || src.Depth() == MatType.CV_16U || src.Depth() == MatType.CV_16S)
                                {
                                    src.ConvertTo(dst8Bit, MatType.CV_8U, 1.0 / 256.0);
                                }
                                else
                                {
                                    src.ConvertTo(dst8Bit, MatType.CV_8U);
                                }
                                ctx.ImageView.Config.AddProperties("FilePath", originalPath);
                                ctx.ImageView.SetImageSource(dst8Bit.ToWriteableBitmap());
                                ctx.ImageView.UpdateZoomAndScale();
                            }
                        }
                    }
                    else
                    {
                        ctx.ImageView.OpenImage(originalPath);
                        result.FilePath = originalPath;
                    }
                }
                else
                {
                    if (File.Exists(result.FilePath))
                        ctx.ImageView.OpenImage(result.FilePath);
                }
            }




            List<string> header = new() { "FileUrl", "FileName"};
            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                foreach (var h in header)
                    gridView.Columns.Add(new GridViewColumn() { Header = h, DisplayMemberBinding = new Binding(h) });

                ctx.ListView.ItemsSource = result.ViewResults.OfType<AlgResultPoiCieFileModel>().ToList();
            }

        }



    }
}
