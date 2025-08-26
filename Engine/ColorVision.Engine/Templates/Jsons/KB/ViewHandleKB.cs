#pragma warning disable CS8604,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;

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


        public override void Load(AlgorithmView view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults ??= new ObservableCollection<IViewResult>(PoiPointResultDao.Instance.GetAllByPid(result.Id));
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmKB), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(AlgorithmView view, ViewResultAlg result)
        {
            if (File.Exists(result.ResultImagFile))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(result.ResultImagFile);
                        log.Warn($"fileInfo.Length{fileInfo.Length}");
                        using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            log.Warn("文件可以读取，没有被占用。");
                        }
                        if (fileInfo.Length > 0)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                view.ImageView.OpenImage(result.ResultImagFile);
                            });
                        }
                    }
                    catch
                    {
                        log.Warn("文件还在写入");
                        await Task.Delay(view.Config.ViewImageReadDelay);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            view.ImageView.OpenImage(result.ResultImagFile);
                        });
                    }
                });
                view.ImageView.OpenImage(result.ResultImagFile);
            }
            else
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
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
            view.AddPOIPoint(DrawPoiPoint);

            List<string> header;
            List<string> bdHeader;
            header = new() { "PoiName", "Value" };
            bdHeader = new() { "PoiName", "Value" };

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
