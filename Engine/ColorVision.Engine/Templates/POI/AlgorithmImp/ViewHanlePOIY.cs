#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Database;
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
    public class ViewHanlePOIY : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleRealPOI));
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.POI_Y};
        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            string fileName = Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var PoiResultCIEYDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIEYData>();
            PoiResultCIEYData.SaveCsv(PoiResultCIEYDatas, fileName);
        }

        public override void Load(AlgorithmView view, AlgorithmResult result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                foreach (var item in POIPointResultModels)
                {
                    PoiResultCIEYData poiResultCIExyuvData = new(item);
                    result.ViewResults.Add(poiResultCIExyuvData);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmPoi), ImageFilePath = result.FilePath })) });
            }
        }
        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();

            List<string> header = new() { "名称", "位置", "大小", "形状", "Y", "Validate" };
            List<string> bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes", "Y", "POIPointResultModel.ValidateResult" };

            if (result.ViewResults.Count <= 4000)
            {
                List<POIPoint> DrawPoiPoint = new();
                foreach (var item in result.ViewResults)
                {
                    if (item is PoiResultData poiResultData)
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
