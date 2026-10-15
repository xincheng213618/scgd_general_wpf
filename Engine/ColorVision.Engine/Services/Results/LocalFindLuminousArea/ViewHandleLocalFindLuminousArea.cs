using ColorVision.Common.Algorithms;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results.LightArea;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Results.LocalFindLuminousArea
{
    public sealed class ViewHandleLocalFindLuminousArea : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = [ViewResultAlgType.FindLightArea];

        public override bool CanHandle1(ViewResultAlg result) =>
            base.CanHandle1(result) &&
            (string.Equals(result.Version, LocalLuminousAreaResultPersistence.ResultVersion, StringComparison.Ordinal) ||
             (string.IsNullOrEmpty(result.Version) && string.Equals(result.POITemplateName, "RobustV2", StringComparison.Ordinal)));

        public override void Load(ViewResultContext context, ViewResultAlg result) =>
            result.ViewResults ??= new ObservableCollection<IViewResult>(AlgResultLightAreaDao.Instance.GetAllByPid(result.Id));

        public override void Handle(ViewResultContext context, ViewResultAlg result)
        {
            OpenSourceImage(context, result);
            var corners = result.ViewResults.ToSpecificViewResults<AlgResultLightAreaModel>();
            if (corners.Count > 0)
            {
                DVPolygon polygon = new() { IsComple = true };
                List<Point> points = corners.Select(item => new Point((int)item.PosX, (int)item.PosY)).ToList();
                foreach (Point point in GrahamScan.ComputeConvexHull(points))
                    polygon.Attribute.Points.Add(point);
                polygon.Attribute.Brush = Brushes.Transparent;
                polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
                polygon.Attribute.Id = -1;
                polygon.Render();
                context.ImageView.AddVisual(polygon);
            }

            if (context.ListView.View is GridView gridView)
            {
                context.ListView.ItemsSource = null;
                context.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                gridView.Columns.Add(new GridViewColumn { Header = "PosX", DisplayMemberBinding = new Binding(nameof(AlgResultLightAreaModel.PosX)) });
                gridView.Columns.Add(new GridViewColumn { Header = "PosY", DisplayMemberBinding = new Binding(nameof(AlgResultLightAreaModel.PosY)) });
                context.ListView.ItemsSource = corners;
            }
        }
    }
}
