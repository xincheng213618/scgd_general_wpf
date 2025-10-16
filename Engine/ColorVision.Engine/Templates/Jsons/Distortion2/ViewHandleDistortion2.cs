
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Extension;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{

    public class ViewHandleDistortion2 : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleDistortion2));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Distortion};
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }
        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            // 添加日期时间戳到文件名（只到天）
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");

            var sb = new StringBuilder();
            
            // 检查文件是否存在以及是否已有标题
            bool fileExists = File.Exists(fileName);
            bool needHeader = !fileExists;
            
            if (fileExists)
            {
                // 读取文件检查是否已有标题
                var lines = File.ReadAllLines(fileName, Encoding.UTF8);
                if (lines.Length == 0 || !lines[0].Contains("OpticDistortion_OpticRatio"))
                {
                    needHeader = true;
                }
            }
            
            // 只在需要时写入标题
            if (needHeader)
            {
                sb.AppendLine("WriteTime,OpticDistortion_OpticRatio,OpticDistortion_T,OpticDistortion_Message,OpticDistortion_MaxErrPoint_X,OpticDistortion_MaxErrPoint_Y,Point9Distortion_TopRatio,Point9Distortion_BottomRatio,Point9Distortion_LeftRatio,Point9Distortion_RightRatio,Point9Distortion_KeyStoneHoriRatio,Point9Distortion_KeyStoneVercRatio,Point9Distortion_Message,TVDistortion_HorizontalRatio,TVDistortion_VerticalRatio,TVDistortion_Message");
            }

            // 获取当前时间用于记录
            string writeTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var res in result.ViewResults.ToSpecificViewResults<Distortion2View>())
            {
                sb.AppendLine(string.Join(",",
                    writeTime,
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
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        public override void Load(IViewImageA view, ViewResultAlg result)
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

        public override void Handle(IViewImageA view, ViewResultAlg result)
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
                            CircleProperties circleProperties = new CircleProperties();
                            circleProperties.Center = new System.Windows.Point(points.X, points.Y);
                            circleProperties.Radius = 20 / view.ImageView.Zoombox1.ContentMatrix.M11;
                            circleProperties.Brush = Brushes.Transparent;
                            circleProperties.Pen = new Pen(Brushes.Red, 1 / view.ImageView.Zoombox1.ContentMatrix.M11);
                            circleProperties.Id = -1;

                            DVCircle Circle = new DVCircle(circleProperties);
                            Circle.Render();
                            view.ImageView.AddVisual(Circle);
                        }
                    }
                }
            }

            List<string> header = new() { "Point9Distortion", "TVDistortion", "OpticDistortion" };
            List<string> bdHeader = new() { "DistortionReslut.Point9Distortion", "DistortionReslut.TVDistortion", "DistortionReslut.OpticDistortion" };

            if (view.ListView.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.ListView.ItemsSource = result.ViewResults;
            }

            view.SideTextBox.Visibility = System.Windows.Visibility.Visible;
            view.SideTextBox.Text = result.ViewResults.ToSpecificViewResults<Distortion2View>()[0].DistortionReslut.ToJsonN();
        }



    }
}
