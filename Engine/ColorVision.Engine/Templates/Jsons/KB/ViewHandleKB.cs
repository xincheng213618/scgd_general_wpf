#pragma warning disable CS8604,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class ViewHandleKB : IResultHandleBase
    {
        private static ILog log = LogManager.GetLogger(nameof(ViewHandleKB));
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.KB , ViewResultAlgType.KB_Raw};

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            if (!File.Exists(result.ResultImagFile)) return;
            try
            {
                // 获取文件名
                string fileName = Path.GetFileName(result.ResultImagFile);

                // 组合目标路径
                string destinationFilePath = Path.Combine(selectedPath, fileName);

                // 复制文件
                File.Copy(result.ResultImagFile, destinationFilePath, true);

                var csvBuilder = new StringBuilder();
                List<string> properties = new() { "PoiName", "Value" };

                // 写入列头
                csvBuilder.AppendLine(string.Join(",", properties));

                // 写入数据行
                foreach (var item in result.ViewResults.OfType<PoiPointResultModel>())
                {
                    List<string> values = new()
                {
                    item.PoiName,
                    item.Value
                };

                    csvBuilder.AppendLine(string.Join(",", values));
                }

                File.WriteAllText(selectedPath + "//" + result.Batch + ".csv", csvBuilder.ToString(), Encoding.UTF8);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults ??= new ObservableCollection<IViewResult>(PoiPointResultDao.Instance.GetAllByPid(result.Id));
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmKB), ImageFilePath = result.FilePath })) });
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

                    ctx.ImageView.Config.AddProperties("FilePath", result.ResultImagFile);
                    ctx.ImageView.OpenImage(writeableBitmap); // 你的方法如果支持这样调用
                    ctx.ImageView.UpdateZoomAndScale();
                }
			}
            else
            {
                if (File.Exists(result.FilePath))
                    ctx.ImageView.OpenImage(result.FilePath);
            }

            List<POIPoint> DrawPoiPoint = new();
            foreach (var item in result.ViewResults)
            {
                if (item is PoiPointResultModel poiPointResultModel)
                {
                    POIPoint pOIPoint = new POIPoint(poiPointResultModel.PoiId ?? -1, -1, poiPointResultModel.PoiName, poiPointResultModel.PoiType, (int)poiPointResultModel.PoiX, (int)poiPointResultModel.PoiY, poiPointResultModel.PoiWidth ?? 0, poiPointResultModel.PoiHeight ?? 0);
                    DrawPoiPoint.Add(pOIPoint);
                }

            }
            AddPOIPoint(ctx.ImageView, DrawPoiPoint);

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
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }
    }

}
