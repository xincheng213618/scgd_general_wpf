using ColorVision.Common.Algorithms;
using ColorVision.Database;
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
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.FindLightArea
{
    [SugarTable("t_scgd_algorithm_result_detail_light_area")]
    public class AlgResultLightAreaModel : EntityBase, IViewResult
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
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.LightArea, ViewResultAlgType.FindLightArea };

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<AlgResultLightAreaModel>();
            var csvBuilder = new StringBuilder();
            File.WriteAllText(fileName, csvBuilder.ToString(), Encoding.UTF8);
        }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            result.ViewResults ??= new ObservableCollection<IViewResult>(AlgResultLightAreaDao.Instance.GetAllByPid(result.Id));
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            ctx.ImageView.ImageShow.Clear();


            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            Load(ctx, result);

            ctx.ImageView.ImageShow.Clear();
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
            ctx.ImageView.AddVisual(polygon);


            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();
            List<string> header = new List<string> { "PosX", "PosY" };
            List<string> bdHeader = new List<string> { "PosX", "PosY" };


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
