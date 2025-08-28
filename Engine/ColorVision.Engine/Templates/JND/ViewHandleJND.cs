using ColorVision.Common.MVVM;
using ColorVision.Database;
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
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.JND
{
    public class ViewHandleJND : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Compliance_Math_JND, ViewResultAlgType.OLED_JND_CalVas};

        public override void SideSave(ViewResultAlg result, string selectedPath)
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

        public IViewImageA AlgorithmView { get; set; }

        public override void Load(IViewImageA view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                foreach (var item in PoiPointResultDao.Instance.GetAllByPid(result.Id))
                    result.ViewResults.Add(new ViewRsultJND(item));
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmJND), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(IViewImageA view, ViewResultAlg result)
        {
            AlgorithmView = view;


            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();
            List<string> header = new() { "Name", "位置", "大小", "形状", "h_jnd", "v_jnd" };
            List<string> bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes", "JND.h_jnd", "JND.v_jnd" };


            if (view.ListView.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.ListView.ItemsSource = result.ViewResults;
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
