#pragma warning disable CA1725
using ColorVision.Common.Algorithms;
using ColorVision.Engine.Services;
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.ImageEditor.Draw;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Matching
{
    public class ViewHandleMatching : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.AOI};


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
           if (result.ViewResults != null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>(AlgResultAoiDao.Instance.GetAllByPid(result.Id));
                result.ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmMatching), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {

            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

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
                ctx.ImageView.AddVisual(polygon);

            }

            List<string> header = new() { Properties.Resources.Score, Properties.Resources.Angle, Properties.Resources.CenterPointX, Properties.Resources.CenterPointY, Properties.Resources.TopLeftPointX, Properties.Resources.TopLeftPointY, Properties.Resources.TopRightPointX, Properties.Resources.TopRightPointY, Properties.Resources.BottomRightPointX, Properties.Resources.BottomRightPointY, Properties.Resources.BottomLeftPointX, Properties.Resources.BottomLeftPointY };
            List<string> bdHeader = new() { "Score", "Angle", "CenterX", "CenterY", "LeftTopX", "LeftTopY" , "RightTopX", "RightTopY", "RightBottomX", "RightBottomY" , "LeftBottomX", "LeftBottomY" };

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }
    }
}
