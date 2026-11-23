#pragma warning disable CA1725
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class ViewHanlePOIY : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleRealPOI));
        private readonly Func<int, IReadOnlyList<PoiPointResultModel>?> _readDetails;

        public ViewHanlePOIY() : this(PoiHistoryDetails.Read) { }

        internal ViewHanlePOIY(Func<int, IReadOnlyList<PoiPointResultModel>?> readDetails) => _readDetails = readDetails;

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.POI, ViewResultAlgType.POI_Y};
        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            var PoiResultCIEYDatas = PoiHistoryDetails.RequireForExport(result).ToSpecificViewResults<PoiResultCIEYData>();
            AlgorithmSideDataFiles.Write(result, selectedPath, $"{result.ResultType}_{result.Batch}.csv",
                path => PoiResultCIEYData.SaveCsv(PoiResultCIEYDatas, path));
        }

        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            PoiHistoryDetails.TryLoad(result, _readDetails, rows => rows.Select(item => new PoiResultCIEYData(item)));
        }
        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            OpenSourceImage(ctx, result);
            var details = PoiHistoryDetails.GetForDisplay(ctx, result);
            if (details == null) return;

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();

            List<string> header = new() { Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Size, Properties.Resources.Shape, "Y" };
            List<string> bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes", "Y" };

            if (details.Count <= 4000)
            {
                List<PoiPoint> poiPoints = new();
                foreach (var item in details)
                {
                    if (item is PoiResultData poiResultData)
                    {
                        poiPoints.Add(poiResultData.Point);
                    }
                }
                PoiOverlayRenderer.AddRange(ctx.ImageView, poiPoints);
            }
            else
            {
                log.Info($"result.ViewResults.Count:{details.Count}");
            }

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.ListView.ItemsSource = null;
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = details;
            }
        }
    }
}
