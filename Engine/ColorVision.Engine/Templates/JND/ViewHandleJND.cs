using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.JND
{
    public class ViewHandleJND : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.Compliance_Math_JND, AlgorithmResultType.OLED_JND_CalVas};

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            var ViewResults = result.ViewResults.ToSpecificViewResults<ViewRsultJND>();

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Id", "名称", "位置", "大小", "形状", "h_jnd", "v_jnd" };
            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));
            // 写入数据行
            foreach (var item in ViewResults)
            {
                List<string> values = new()
                {
                    item.Point.Id?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    item.Name,
                    $"{item.Point.PixelX}|{item.Point.PixelY}" ,
                    $"{item.Point.Width}|{item.Point.Height}",
                    item.Shapes,
                    item.JND.h_jnd.ToString(),
                    item.JND.v_jnd.ToString(),
                };

                csvBuilder.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(selectedPath, csvBuilder.ToString(), Encoding.UTF8);

            string saveng = System.IO.Path.Combine(selectedPath, $"{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.png");
            AlgorithmView.ImageView.ImageViewModel.Save(saveng);
        }

        public AlgorithmView AlgorithmView { get; set; }

        public override void Load(AlgorithmView view, AlgorithmResult result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                foreach (var item in PoiPointResultDao.Instance.GetAllByPid(result.Id))
                    result.ViewResults.Add(new ViewRsultJND(item));
            }
        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            AlgorithmView = view;
            view.ImageView.ImageShow.Clear();
            if (result.ResultCode != 0)
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
                return;
            }

            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            Load(view,result);

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();
            List<string> header = new() { "Name", "位置", "大小", "形状", "h_jnd", "v_jnd" };
            List<string> bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes", "JND.h_jnd", "JND.v_jnd" };


            if (view.listViewSide.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
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
        }

    }

}
