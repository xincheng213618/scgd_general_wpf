#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Extension;
using log4net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{

    public class ViewHandleDistortion2 : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleDistortion2));

        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.Distortion};
        public override bool CanHandle1(AlgorithmResult result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }
        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");

            var sb = new StringBuilder();
            // 标题
            sb.AppendLine("OpticDistortion_OpticRatio,OpticDistortion_T,OpticDistortion_Message,OpticDistortion_MaxErrPoint_X,OpticDistortion_MaxErrPoint_Y,Point9Distortion_TopRatio,Point9Distortion_BottomRatio,Point9Distortion_LeftRatio,Point9Distortion_RightRatio,Point9Distortion_KeyStoneHoriRatio,Point9Distortion_KeyStoneVercRatio,Point9Distortion_Message,TVDistortion_HorizontalRatio,TVDistortion_VerticalRatio,TVDistortion_Message");

            foreach (var res in result.ViewResults.ToSpecificViewResults<Distortion2View>())
            {
                sb.AppendLine(string.Join(",",
                    res.DistortionReslut.OpticDistortion?.OpticRatio.ToString() ?? "",
                    res.DistortionReslut.OpticDistortion?.T.ToString() ?? "",
                    EscapeCsv(res.DistortionReslut.OpticDistortion?.Message),
                    res.DistortionReslut.OpticDistortion?.MaxErrPoint?.X.ToString() ?? "",
                    res.DistortionReslut.OpticDistortion?.MaxErrPoint?.Y.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.TopRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.BottomRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.LeftRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.RightRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.KeyStoneHoriRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.KeyStoneVercRatio.ToString() ?? "",
                    EscapeCsv(res.DistortionReslut.Point9Distortion?.Message),
                    res.DistortionReslut.TVDistortion?.HorizontalRatio.ToString() ?? "",
                    res.DistortionReslut.TVDistortion?.VerticalRatio.ToString() ?? "",
                    EscapeCsv(res.DistortionReslut.TVDistortion?.Message)
                ));
            }
            File.AppendAllText(fileName, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        public override void Load(AlgorithmView view, AlgorithmResult result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultModels)
                {
                    Distortion2View blackMuraView = new Distortion2View(item);
                    result.ViewResults.Add(blackMuraView);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmDistortion2), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            void OpenSource()
            {
                view.ImageView.ImageShow.Clear();
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
                log.Info(result.FilePath);
            }

            OpenSource();

            foreach (var item in result.ViewResults.ToSpecificViewResults<Distortion2View>())
            {
                if (item.DistortionReslut !=null && item.DistortionReslut.TVDistortion !=null)
                {
                    if (item.DistortionReslut.TVDistortion.FinalPoints != null)
                    {
                        foreach (var points in item.DistortionReslut.TVDistortion.FinalPoints)
                        {
                            DVCircle Circle = new();
                            Circle.Attribute.Center = new System.Windows.Point(points.X, points.Y);
                            Circle.Attribute.Radius = 20 / view.ImageView.Zoombox1.ContentMatrix.M11;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / view.ImageView.Zoombox1.ContentMatrix.M11);
                            Circle.Attribute.Id = -1;
                            Circle.Render();
                            view.ImageView.AddVisual(Circle);
                        }
                    }
                }
            }

            List<string> header = new() { "Point9Distortion", "TVDistortion", "OpticDistortion" };
            List<string> bdHeader = new() { "DistortionReslut.Point9Distortion", "DistortionReslut.TVDistortion", "DistortionReslut.OpticDistortion" };

            if (view.listViewSide.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }

            view.SideTextBox.Visibility = System.Windows.Visibility.Visible;
            view.SideTextBox.Text = result.ViewResults.ToSpecificViewResults<Distortion2View>()[0].DistortionReslut.ToJsonN();
        }



    }
}
