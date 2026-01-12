#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using ColorVision.Solution.Editor.AvalonEditor;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
 

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


        public override void Load(ViewResultContext view, ViewResultAlg result)
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
         
        public override void Handle(ViewResultContext view, ViewResultAlg result)
        {
            var AlgResultPoiCieFileModel = result.ViewResults.OfType<AlgResultPoiCieFileModel>().Where(a => a.FileName.Contains("Y.tif")).FirstOrDefault();
            if (AlgResultPoiCieFileModel != null)
            {
                if (File.Exists(AlgResultPoiCieFileModel.FileUrl))
                    view.ImageView.OpenImage(AlgResultPoiCieFileModel.FileUrl);



            }
            else
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);

            }

            List<string> header = new() { "FileUrl", "FileName"};
            if (view.ListView.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                foreach (var h in header)
                    gridView.Columns.Add(new GridViewColumn() { Header = h, DisplayMemberBinding = new Binding(h) });

                view.ListView.ItemsSource = result.ViewResults.OfType<AlgResultPoiCieFileModel>().ToList();
            }

        }



    }
}
