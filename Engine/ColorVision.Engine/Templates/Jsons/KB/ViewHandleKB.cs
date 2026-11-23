#pragma warning disable CA1725,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class ViewHandleKB : IResultHandleBase
    {
        private readonly Func<int, IReadOnlyList<PoiPointResultModel>?> _readDetails;

        public ViewHandleKB() : this(PoiHistoryDetails.Read) { }

        internal ViewHandleKB(Func<int, IReadOnlyList<PoiPointResultModel>?> readDetails) => _readDetails = readDetails;

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.KB , ViewResultAlgType.KB_Raw};

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            var details = PoiHistoryDetails.RequireForExport(result);
            if (string.IsNullOrWhiteSpace(result.ResultImagFile))
                throw new FileNotFoundException($"Keyboard result {result.Id} has no annotated image.");
            var rows = details.OfType<PoiPointResultModel>().Select(item => new[] { item.PoiName, item.Value });
            AlgorithmSideDataFiles.WriteCsv(result, selectedPath, $"{result.Batch}.csv", ["PoiName", "Value"], rows, result.ResultImagFile);
        }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null && PoiHistoryDetails.TryLoad(result, _readDetails))
            {
                result.ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmKB), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.ResultImagFile))
            {
				using (var stream = File.OpenRead(result.ResultImagFile))
				{
					BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
					BitmapSource bitmapSource = decoder.Frames[0];
					WriteableBitmap writeableBitmap = new WriteableBitmap(bitmapSource);

                    ctx.ImageView.Config.FilePath = result.ResultImagFile;
                    ctx.ImageView.OpenImage(writeableBitmap); // 你的方法如果支持这样调用
                    ctx.ImageView.UpdateZoomAndScale();
                }
			}
            else
            {
                OpenSourceImage(ctx, result);
            }

            var details = PoiHistoryDetails.GetForDisplay(ctx, result);
            if (details == null) return;

            List<PoiPoint> poiPoints = new();
            foreach (var item in details)
            {
                if (item is PoiPointResultModel poiPointResultModel)
                {
                    poiPoints.Add(new PoiPoint(poiPointResultModel.PoiId, -1, poiPointResultModel.PoiName, poiPointResultModel.PoiType.ToPoiShape(), poiPointResultModel.PoiX ?? 0, poiPointResultModel.PoiY ?? 0, poiPointResultModel.PoiWidth ?? 0, poiPointResultModel.PoiHeight ?? 0));
                }

            }
            PoiOverlayRenderer.AddRange(ctx.ImageView, poiPoints);

            List<string> header;
            List<string> bdHeader;
            header = new() { "PoiName", "Value" };
            bdHeader = new() { "PoiName", "Value" };

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = details;
            }
        }
    }

}
