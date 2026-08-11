#pragma warning disable CA1725

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services;
using ColorVision.Database;
using ColorVision.ImageEditor.Draw;
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

namespace ColorVision.Engine.Templates.Ghost
{
    public class  ViewHandleGhost : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Ghost};

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            var ViewResults = result.ViewResults.ToSpecificViewResults<AlgResultGhostModel>();
            var csvBuilder = new StringBuilder();
            List<string> headers = new List<string>();
            headers.Add("id");
            headers.Add(Properties.Resources.CentroidCoordinates);
            headers.Add(Properties.Resources.SpotGrayscale);
            headers.Add(Properties.Resources.GhostGrayscale);
            csvBuilder.AppendLine(string.Join(",", headers));

            foreach (var item in ViewResults)
            {
                List<string> content = new List<string>();
                content.Add(EscapeCsvField(item.Id.ToString()));
                content.Add(EscapeCsvField(item.LEDCenters));
                content.Add(EscapeCsvField(item.LEDBlobGray));
                content.Add(EscapeCsvField(item.GhostAverageGray));
                csvBuilder.AppendLine(string.Join(",", content));
            }
            csvBuilder.AppendLine();
            csvBuilder.AppendLine();
            File.AppendAllText(selectedPath, csvBuilder.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',' ) || field.Contains('"') || field.Contains('\n'))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }
            return field;
        }
        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<AlgResultGhostModel> AlgResultGhostModels = AlgResultGhostDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultGhostModels)
                {
                    result.ViewResults.Add(item);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmGhost), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);



            if (result.ViewResults.Count != 0 && result.ViewResults[0] is AlgResultGhostModel viewResultGhost)
            {
                try
                {
                    List<Point1> generatedPoints = new();
                    if (viewResultGhost.GhostPixel !=null)
                        foreach (var item in viewResultGhost.GhostPixel)
                            foreach (var item1 in item)
                                generatedPoints.Add(item1);
                    if (viewResultGhost.LedPixel !=null)
                        foreach (var item in viewResultGhost.LedPixel)
                            foreach (var item1 in item)
                                generatedPoints.Add(item1);

                    VectorizedSelectVisual? visual = CreatePixelVisual(generatedPoints, ctx.ImageView.ImageShow.Source);
                    if (visual != null)
                        ctx.ImageView.ImageShow.AddVisualCommand(visual);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            List<string> header = new() { Properties.Resources.CentroidCoordinates, Properties.Resources.SpotGrayscale, Properties.Resources.GhostGrayscale };
            List<string> bdHeader = new() { "LEDCenters", "LEDBlobGray", "GhostAverageGray" };

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }

        internal static VectorizedSelectVisual? CreatePixelVisual(IReadOnlyList<Point1> points, ImageSource? imageSource)
        {
            ArgumentNullException.ThrowIfNull(points);
            if (points.Count == 0)
                return null;

            const double markerSize = 1;
            StreamGeometry geometry = new();
            using (StreamGeometryContext geometryContext = geometry.Open())
            {
                foreach (Point1 point in points)
                {
                    geometryContext.BeginFigure(new Point(point.X, point.Y), isFilled: true, isClosed: true);
                    geometryContext.LineTo(new Point(point.X + markerSize, point.Y), isStroked: true, isSmoothJoin: false);
                    geometryContext.LineTo(new Point(point.X + markerSize, point.Y + markerSize), isStroked: true, isSmoothJoin: false);
                    geometryContext.LineTo(new Point(point.X, point.Y + markerSize), isStroked: true, isSmoothJoin: false);
                }
            }
            geometry.Freeze();

            GeometryDrawing drawing = new(Brushes.Transparent, new Pen(Brushes.Red, 1), geometry);
            drawing.Freeze();

            VectorizedSelectVisual visual = new(drawing, GetPixelVisualBounds(points, imageSource, markerSize));
            visual.Attribute.Tag = points;
            return visual;
        }

        private static Rect GetPixelVisualBounds(IReadOnlyList<Point1> points, ImageSource? imageSource, double markerSize)
        {
            if (ImageUtils.TryGetImageSize(imageSource, out int width, out int height))
                return new Rect(0, 0, width, height);

            int minX = points.Min(point => point.X);
            int minY = points.Min(point => point.Y);
            int maxX = points.Max(point => point.X);
            int maxY = points.Max(point => point.Y);
            return new Rect(minX, minY, maxX - minX + markerSize, maxY - minY + markerSize);
        }



    }
}
