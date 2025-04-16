#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using CVCommCore.CVAlgorithm;
using log4net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class ViewHanlePOIXZY : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleRealPOI));
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.POI_XYZ ,AlgorithmResultType.LEDStripDetection };
        
        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            string fileName = Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var PoiResultCIExyuvDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>();
            PoiResultCIExyuvData.SaveCsv(PoiResultCIExyuvDatas, fileName);
        }
        public override void Load(AlgorithmResult result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                int id = 0;
                foreach (var item in POIPointResultModels)
                {
                    PoiResultCIExyuvData poiResultCIExyuvData = new(item) { Id = id++ };
                    result.ViewResults.Add(poiResultCIExyuvData);
                }
                ;
            }
        }
        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            if (result.ResultCode != 0)
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
                return;
            }

            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            Load(result);

            view.ImageView.ImageShow.Clear();


            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();


            List<string> header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape, Properties.Resources.Size, "CCT", "Wave", "X", "Y", "Z", "u'", "v", "x", "y", "Validate" };
            if (result.ResultType == AlgorithmResultType.LEDStripDetection)
            {
                header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape };
            }

            List<string> bdHeader = new List<string> { "Id", "Name", "PixelPos", "Shapes", "PixelSize", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y", "POIPointResultModel.ValidateResult" };


            if (result.ViewResults.Count <= 4000)
            {
                List<POIPoint> DrawPoiPoint = new();
                foreach (var item in result.ViewResults)
                {
                    if (item is PoiResultCIExyuvData poiResultData)
                    {
                        DrawPoiPoint.Add(poiResultData.Point);
                    }
                }
                view.AddPOIPoint(DrawPoiPoint);
            }
            else
            {
                log.Info($"result.ViewResults.Count:{result.ViewResults.Count}");
            }


            if (view.listViewSide.View is GridView gridView)
            {
                view.listViewSide.ItemsSource = null;
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }
        }
    }
}
