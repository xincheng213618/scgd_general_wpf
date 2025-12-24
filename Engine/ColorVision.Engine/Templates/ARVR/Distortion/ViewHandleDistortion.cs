using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.ImageEditor.Draw;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.Distortion
{
    public class ViewHandleDistortion : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Distortion };

        public override bool CanHandle1(ViewResultAlg result)
        {
            return base.CanHandle1(result);
        }

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<ViewResultDistortion>();

            List<string> bdHeader = new() { "DisTypeDesc", "SlopeTypeDesc", "LayoutTypeDesc", "CornerTypeDesc", "MaxRatio" };

            var csvBuilder = new StringBuilder();
            List<string> header = new() { "Type", "Slope", "Layout", "Corner", "DistortionRatio" };

            csvBuilder.AppendLine(string.Join(",", header));

            // Collect Data for basic information
            List<List<string>> basicData = new List<List<string>>();
            foreach (var item in ViewResults)
            {
                List<string> strings = new List<string>()
                {
                    item.DisTypeDesc.ToString(),
                    item.SlopeTypeDesc.ToString(),
                    item.LayoutTypeDesc.ToString(),
                    item.CornerTypeDesc.ToString(),
                    item.MaxRatio.ToString()
                };
                csvBuilder.AppendLine(string.Join(",", strings));
            }
            csvBuilder.AppendLine();
            foreach (var item in ViewResults)
            {
                foreach (var point in item.FinalPoints)
                {
                    List<string> strings = new List<string>()
                    {
                        point.X.ToString(),
                        point.Y.ToString()
                    };
                    csvBuilder.AppendLine(string.Join(",", strings));
                }
            }
            File.WriteAllText(fileName, csvBuilder.ToString(), Encoding.UTF8);
        }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                foreach (var item in AlgResultDistortionDao.Instance.GetAllByPid(result.Id))
                    result.ViewResults.Add(new ViewResultDistortion(item));
                result.ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmDistortion), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            int id = 0;
            foreach (var item in result.ViewResults.ToSpecificViewResults<ViewResultDistortion>())
            {
                foreach (var point in item.FinalPoints)
                {
                    id++;
                    CircleProperties circleProperties = new CircleProperties();
                    circleProperties.Center = new Point(point.X, point.Y);
                    circleProperties.Radius = 20 / ctx.ImageView.Zoombox1.ContentMatrix.M11;
                    circleProperties.Brush = Brushes.Transparent;
                    circleProperties.Pen = new Pen(Brushes.Red, 1 / ctx.ImageView.Zoombox1.ContentMatrix.M11);
                    circleProperties.Id = -1;

                    DVCircle Circle = new DVCircle(circleProperties);
                    Circle.Render();
                    ctx.ImageView.AddVisual(Circle);

                }
            }

            List<string> header = new() { "Type", "Slope", "Layout", "Corner", "DistortionRatio" };
            List<string> bdHeader = new() { "DisTypeDesc", "SlopeTypeDesc", "LayoutTypeDesc", "CornerTypeDesc", "MaxRatio" };

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
