using ColorVision.Common.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor.Draw;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.Matching
{
    public class ViewHandleMatching : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.AOI};


        public override void Load(AlgorithmView view, ViewResultAlg result)
        {
           if (result.ViewResults != null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>(AlgResultAoiDao.Instance.GetAllByPid(result.Id));
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmMatching), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(AlgorithmView view, ViewResultAlg result)
        {

            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            foreach (var item in result.ViewResults.ToSpecificViewResults<AlgResultAoiModel>())
            {
                List<System.Windows.Point> point1s = new List<System.Windows.Point>();
                point1s.Add(new System.Windows.Point((int)item.LeftTopX, (int)item.LeftTopY));
                point1s.Add(new System.Windows.Point((int)item.RightTopX, (int)item.RightTopY));
                point1s.Add(new System.Windows.Point((int)item.RightBottomX, (int)item.RightBottomY));
                point1s.Add(new System.Windows.Point((int)item.LeftBottomX, (int)item.LeftBottomY));
                DVPolygon polygon = new DVPolygon();

                foreach (var point in GrahamScan.ComputeConvexHull(point1s))
                {
                    polygon.Attribute.Points.Add(point);
                }
                polygon.Attribute.Brush = Brushes.Transparent;
                polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
                polygon.Attribute.Id = -1;
                polygon.IsComple = true;
                polygon.Render();
                view.ImageView.AddVisual(polygon);

            }

            List<string> header =  new() { "分数", "角度", "中心点x" , "中心点y", "左上点x" , "左上点y" , "右上点x" , "右上点y" , "右下点x", "右下点y", "左下点x", "左下点x" };
            List<string> bdHeader = new() { "Score", "Angle", "CenterX", "CenterY", "LeftTopX", "LeftTopY" , "RightTopX", "RightTopY", "RightBottomX", "RightBottomY" , "LeftBottomX", "LeftBottomY" };

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
