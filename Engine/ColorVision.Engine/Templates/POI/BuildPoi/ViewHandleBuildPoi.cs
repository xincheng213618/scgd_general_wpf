using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class ViewHandleBuildPoi : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get;  } = new List<ViewResultAlgType>() { ViewResultAlgType.BuildPOI};

        public override void Load(AlgorithmView view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultMTFModels)
                {
                    ViewResultBuildPoi mTFResultData = new(item);
                    result.ViewResults.Add(mTFResultData);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmBuildPoi), ImageFilePath = result.FilePath })) });
            }

        }

        public override void Handle(AlgorithmView view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);


            List<POIPoint> DrawPoiPoint = new();
            foreach (var item in result.ViewResults)
            {
                if (item is PoiResultData poiResultData)
                    DrawPoiPoint.Add(poiResultData.Point);
            }
            view.AddPOIPoint(DrawPoiPoint);

            List<string> header;
            List<string> bdHeader;
            header = new() { "Name", "位置", "大小", "形状" };
            bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes" };

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
