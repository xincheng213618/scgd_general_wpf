using ColorVision.Common.MVVM;
using ColorVision.Database;
using CVCommCore.CVAlgorithm;
using log4net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class ViewHanlePOIY : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleRealPOI));
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.POI, ViewResultAlgType.POI_Y};
        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var PoiResultCIEYDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIEYData>();
            PoiResultCIEYData.SaveCsv(PoiResultCIEYDatas, fileName);
        }

        public override void Load(ViewResultContext ctx, ViewResultAlg result)
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
        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();

            List<string> header = new() { "名称", "位置", "大小", "形状", "Y" };
            List<string> bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes", "Y" };

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
                AddPOIPoint(ctx.ImageView, DrawPoiPoint);
            }
            else
            {
                log.Info($"result.ViewResults.Count:{result.ViewResults.Count}");
            }

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.ListView.ItemsSource = null;
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }
    }
}
