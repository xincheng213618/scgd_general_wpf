#pragma warning disable CS8604,CS8602,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Templates.ARVR.SFR;
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
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.SFR
{
    public class ViewHandleSFR : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.SFR };

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<AlgResultSFRModel>();

            var csvBuilder = new StringBuilder();

            // Collect Data for basic information
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

            // Collect Data for Pdfrequency and PdomainSamplingData
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

            // Write Data Rows for Pdfrequency and PdomainSamplingData
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


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                var AlgResultSFRModels = AlgResultSFRDao.Instance.GetAllByPid(result.Id);

                foreach (var item in AlgResultSFRModels)
                {
                    var Pdfrequencys = JsonConvert.DeserializeObject<float[]>(item.Pdfrequency);
                    var PdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(item.PdomainSamplingData);
                }

                result.ViewResults = new ObservableCollection<IViewResult>(AlgResultSFRModels);


                RelayCommand relayCommand = new RelayCommand(a => new WindowSFR(AlgResultSFRModels).Show());

                result.ContextMenu.Items.Add(new MenuItem() { Header = "分析", Command = relayCommand });

                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmSFR), ImageFilePath = result.FilePath })) });

            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);


            foreach (var item in result.ViewResults.ToSpecificViewResults<AlgResultSFRModel>())
            {
                DVRectangleText Rectangle = new();
                Rectangle.Attribute.Rect = new Rect((double)item.RoiX, (double)item.RoiY, (double)item.RoiWidth, (double)item.RoiHeight);
                Rectangle.Attribute.Brush = Brushes.Transparent;
                Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                Rectangle.Attribute.Id = item.Id;
                Rectangle.Render();
                ctx.ImageView.AddVisual(Rectangle);
            }

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();
            List<string> header = new() { "RoiX", "RoiY", "RoiWidth", "RoiHeight", "Pdfrequency", "PdomainSamplingData" };
            List<string> bdHeader = new() { "RoiX", "RoiY", "RoiWidth", "RoiHeight", "Pdfrequency", "PdomainSamplingData" };

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
