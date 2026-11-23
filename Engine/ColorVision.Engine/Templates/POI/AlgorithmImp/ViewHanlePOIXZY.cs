#pragma warning disable CA1725
using ColorVision.Engine.Media;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class ViewHanlePOIXZYFile : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.POI_XYZ_File, ViewResultAlgType.POI_Y_File, ViewResultAlgType.POI_CIE_File };

        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
            }
        }
        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            OpenSourceImage(ctx, result);
        }
    }


    public class ViewHanlePOIXZY : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleRealPOI));
        private readonly Func<int, IReadOnlyList<PoiPointResultModel>?> _readDetails;

        public ViewHanlePOIXZY() : this(PoiHistoryDetails.Read) { }

        internal ViewHanlePOIXZY(Func<int, IReadOnlyList<PoiPointResultModel>?> readDetails) => _readDetails = readDetails;

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.POI_XYZ};
        
        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            var PoiResultCIExyuvDatas = PoiHistoryDetails.RequireForExport(result).ToSpecificViewResults<PoiResultCIExyuvData>();
            AlgorithmSideDataFiles.Write(result, selectedPath, $"{result.ResultType}_{result.Batch}.csv",
                path => PoiResultCIExyuvDatas.SaveCsv(path));
        }
        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            PoiHistoryDetails.TryLoad(result, _readDetails, rows => rows.Select((item, id) => new PoiResultCIExyuvData(item) { Id = id }));
        }
        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {

            OpenSourceImage(ctx, result);
            var details = PoiHistoryDetails.GetForDisplay(ctx, result);
            if (details == null) return;

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();


            List<string> header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape, Properties.Resources.Size, "CCT", "Wave", "X", "Y", "Z", "u'", "v", "x", "y" };
            if (result.ResultType == ViewResultAlgType.LEDStripDetection)
            {
                header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape };
            }

            List<string> bdHeader = new List<string> { "Id", "Name", "PixelPos", "Shapes", "PixelSize", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y" };


            if (details.Count <= 4000)
            {
                foreach (PoiResultCIExyuvData poiResult in details.ToSpecificViewResults<PoiResultCIExyuvData>())
                    PoiOverlayRenderer.Add(ctx.ImageView, poiResult.Point, CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResult));
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
