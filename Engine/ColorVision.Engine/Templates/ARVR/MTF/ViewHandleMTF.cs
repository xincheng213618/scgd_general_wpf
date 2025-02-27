using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Text;
using System.Globalization;

namespace ColorVision.Engine.Templates.MTF
{
    public class ViewHandleMTF : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.MTF};


        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();
            if (result.ResultCode != 0)
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
                return;
            }

            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultMTFModels)
                {
                    ViewResultMTF mTFResultData = new(item);
                    result.ViewResults.Add(mTFResultData);
                }
            }

            List<POIPoint> DrawPoiPoint = new();

            foreach (var item in result.ViewResults)
            {
                if (item is PoiResultData poiResultData)
                {
                    DrawPoiPoint.Add(poiResultData.Point);
                }
            }
            view.AddPOIPoint(DrawPoiPoint);

            List<string> header;
            List<string> bdHeader;
            header = new() { "Name", "位置", "大小", "形状", "MTF", "Value" };
            bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes", "Articulation", "AlgResultMTFModel.ValidateResult" };


            if (view.listViewSide.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }
        }

        public static void SaveCsv(ObservableCollection<ViewResultMTF> viewResultMTFs, string FileName)
        {
            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Name", "位置", "大小", "形状", "Articulation", };
            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));
            // 写入数据行
            foreach (var item in viewResultMTFs)
            {
                List<string> values = new()
                {
                    item.Point.Id?.ToString(CultureInfo.InvariantCulture),
                    item.Name,
                    $"{item.Point.PixelX}|{item.Point.PixelY}" ,
                    $"{item.Point.Width}|{item.Point.Height}",
                    item.Shapes.ToString(),
                    item.Articulation.ToString(),
                };
                csvBuilder.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
        }
    }
}
