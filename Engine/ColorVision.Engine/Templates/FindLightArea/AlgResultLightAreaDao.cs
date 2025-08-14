using ColorVision.Common.Algorithms;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor.Draw;
using SqlSugar;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.FindLightArea
{
    [SugarTable("t_scgd_algorithm_result_detail_light_area")]
    public class AlgResultLightAreaModel : PKModel, IViewResult
    {
        [SugarColumn(ColumnName ="pid")]
        public int Pid { get; set; }

        [SugarColumn(ColumnName ="pos_x")]
        public float PosX { get; set; }

        [SugarColumn(ColumnName ="pos_y")]
        public float PosY { get; set; }

    }
    public class AlgResultLightAreaDao : BaseTableDao<AlgResultLightAreaModel>
    {
        public static AlgResultLightAreaDao Instance { get; set; } = new AlgResultLightAreaDao();

    }

    public class ViewHandleFindLightArea : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.LightArea, AlgorithmResultType.FindLightArea };

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<AlgResultLightAreaModel>();
            var csvBuilder = new StringBuilder();
            File.WriteAllText(fileName, csvBuilder.ToString(), Encoding.UTF8);
        }


        public override void Load(AlgorithmView view, AlgorithmResult result)
        {
            result.ViewResults ??= new ObservableCollection<IViewResult>(AlgResultLightAreaDao.Instance.GetAllByPid(result.Id));
        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();


            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            Load(view, result);

            view.ImageView.ImageShow.Clear();
            DVPolygon polygon = new DVPolygon();
            List<System.Windows.Point> point1s = new List<System.Windows.Point>();
            foreach (var item in result.ViewResults.ToSpecificViewResults<AlgResultLightAreaModel>())
            {
                point1s.Add(new System.Windows.Point((int)item.PosX, (int)item.PosY));
            }
            foreach (var item in GrahamScan.ComputeConvexHull(point1s))
            {
                polygon.Attribute.Points.Add(new Point(item.X, item.Y));
            }
            polygon.Attribute.Brush = Brushes.Transparent;
            polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
            polygon.Attribute.Id = -1;
            polygon.IsComple = true;
            polygon.Render();
            view.ImageView.AddVisual(polygon);


            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();
            List<string> header = new List<string> { "PosX", "PosY" };
            List<string> bdHeader = new List<string> { "PosX", "PosY" };


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
