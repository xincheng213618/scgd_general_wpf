#pragma warning disable CS8604,CS8602,CS8629
using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System;
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
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<AlgResultSFRModel>();

            var csvBuilder = new StringBuilder();

            // Collect data for basic information
            List<List<string>> basicData = new List<List<string>>();
            foreach (var item in ViewResults)
            {
                basicData.Add(new List<string>
    {
        item.Id.ToString(),
        item.RoiX.ToString(),
        item.RoiY.ToString(),
        item.RoiWidth.ToString(),
        item.RoiHeight.ToString()
    });
            }

            // Generate dynamic headers for basic information
            List<string> basicHeaders = new List<string>();
            for (int i = 0; i < basicData.Count; i++)
            {
                basicHeaders.Add($"{basicData[i][0]}");
                basicHeaders.Add(""); // Add empty columns for spacing
                basicHeaders.Add("");
            }
            csvBuilder.AppendLine(string.Join(",", basicHeaders));

            // Combine RoiX and RoiY, and RoiWidth and RoiHeight
            List<string> roiXYLine = new List<string>();
            List<string> roiWHLine = new List<string>();
            for (int i = 0; i < basicData.Count; i++)
            {
                roiXYLine.Add($"{basicData[i][1]},{basicData[i][2]}");
                roiXYLine.Add("");
                roiWHLine.Add($"{basicData[i][3]},{basicData[i][4]}");
                roiWHLine.Add("");
            }
            csvBuilder.AppendLine(string.Join(",", roiXYLine));
            csvBuilder.AppendLine(string.Join(",", roiWHLine));
            csvBuilder.AppendLine();

            // Collect data for Pdfrequency and PdomainSamplingData
            List<float[]> lists = new List<float[]>();
            int maxLength = 0;
            foreach (var item in ViewResults)
            {
                var Pdfrequencys = JsonConvert.DeserializeObject<float[]>(item.Pdfrequency);
                var PdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(item.PdomainSamplingData);
                lists.Add(Pdfrequencys);
                lists.Add(PdomainSamplingDatas);
                maxLength = Math.Max(maxLength, Math.Max(Pdfrequencys.Length, PdomainSamplingDatas.Length));
            }

            // Generate dynamic headers for Pdfrequency and PdomainSamplingData
            List<string> dynamicHeaders = new();
            for (int i = 0; i < lists.Count; i += 2)
            {
                dynamicHeaders.Add($"Pdfrequency_{i / 2 + 1}");
                dynamicHeaders.Add($"PdomainSamplingData_{i / 2 + 1}");
                dynamicHeaders.Add(""); // Add an empty header for the space column
            }
            csvBuilder.AppendLine(string.Join(", ", dynamicHeaders));

            // Write data rows for Pdfrequency and PdomainSamplingData
            for (int i = 0; i < maxLength; i++)
            {
                List<string> lineValues = new List<string>();
                for (int j = 0; j < lists.Count; j += 2)
                {
                    string pdfValue = i < lists[j].Length ? lists[j][i].ToString() : "";
                    string pdomainValue = i < lists[j + 1].Length ? lists[j + 1][i].ToString() : "";
                    lineValues.Add($"{pdfValue}, {pdomainValue},"); // Add an empty value for the space column
                }
                csvBuilder.AppendLine(string.Join(", ", lineValues));
            }

            File.WriteAllText(fileName, csvBuilder.ToString(), Encoding.UTF8);
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
                    Rectangle.Attribute.Rect = new Rect((double)poiResultData.RoiX, (double)poiResultData.RoiY, (double)poiResultData.RoiWidth, (double)poiResultData.RoiHeight);
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
