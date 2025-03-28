#pragma warning disable CS8604,CS8602
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.MTF;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.SFR
{
    public class ViewHandleSFR : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.SFR };

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            var ViewResults = result.ViewResults.ToSpecificViewResults<AlgResultSFRModel>();

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "id","RoiX", "RoiY", "RoiWidth", "RoiHeight", "Pdfrequency", "PdomainSamplingData" };
            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));
            // 写入数据行
            foreach (var item in ViewResults)
            {
                List<string> values = new()
                {
                    item.Id.ToString(),
                    item.RoiX.ToString(),
                    item.RoiY.ToString(),
                    item.RoiWidth.ToString(),
                    item.RoiHeight.ToString(),
                    item.Pdfrequency.ToString(),
                    item.PdomainSamplingData.ToString(),
                };

                csvBuilder.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(selectedPath, csvBuilder.ToString(), Encoding.UTF8);
        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();
            if (TemplateSFR.Params.Where(x => x.Key == result.POITemplateName).FirstOrDefault() is TemplateModel<SFRParam> templateModel)
            {
                var rect = templateModel.Value.RECT;
                DVRectangleText Rectangle = new();
                Rectangle.Attribute.Rect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
                Rectangle.Attribute.Brush = Brushes.Transparent;
                Rectangle.Attribute.Pen = new Pen(Brushes.Red, rect.Width / 30.0);
                Rectangle.Render();
                view.ImageView.AddVisual(Rectangle);
            }
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
                result.ViewResults = new ObservableCollection<IViewResult>(AlgResultSFRDao.Instance.GetAllByPid(result.Id));
            }
            view.ImageView.ImageShow.Clear();

            foreach (var item in result.ViewResults)
            {
                if (item is AlgResultSFRModel poiResultData)
                {
                    DVRectangleText Rectangle = new();
                    Rectangle.Attribute.Rect = new Rect((double)poiResultData.RoiX - (double)poiResultData.RoiWidth / 2, (double)poiResultData.RoiY - (double)poiResultData.RoiHeight / 2, (double)poiResultData.RoiWidth, (double)poiResultData.RoiHeight);
                    Rectangle.Attribute.Brush = Brushes.Transparent;
                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                    Rectangle.Attribute.Id = poiResultData.Id;
                    Rectangle.Render();
                    view.ImageView.AddVisual(Rectangle);
                }
            }

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();
            List<string> header = new() { "RoiX", "RoiY", "RoiWidth", "RoiHeight", "Pdfrequency", "PdomainSamplingData" };
            List<string> bdHeader = new() { "RoiX", "RoiY", "RoiWidth", "RoiHeight", "Pdfrequency", "PdomainSamplingData" };

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
