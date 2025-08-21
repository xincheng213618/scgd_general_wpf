using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using CVCommCore.CVAlgorithm;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{

    public class ColorInformation
    {
        public double KeyLv { get; set; }
        public int HaloLv { get; set; }
        public CieColor KeyCIE { get; set; }
        public CieColor HaloCIE { get; set; }
    }

    public class CieColor
    {
        public double U { get; set; }
        public double V { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double CCT { get; set; }
        public double Wave { get; set; }
    }





    public class ViewHandleRealPOI : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleRealPOI));
        public override List<AlgorithmResultType> CanHandle { get;  } = new List<AlgorithmResultType>() { AlgorithmResultType.RealPOI, AlgorithmResultType.POI_XYZ_V2, AlgorithmResultType.POI_Y_V2 , AlgorithmResultType.KB_Output_Lv, AlgorithmResultType.KB_Output_CIE };

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            var csvBuilder = new StringBuilder();

            if (result.ResultType == AlgorithmResultType.KB_Output_CIE)
            {
                List<string> properties = new List<string>() { "PoiName", "KeyLv", "HaloLv", "KeyCIE_U", "KeyCIE_V", "KeyCIE_X", "KeyCIE_Y", "KeyCIE_CCT", "KeyCIE_Wave", "HaloCIE_U", "HaloCIE_V", "HaloCIE_X", "HaloCIE_Y", "HaloCIE_CCT", "HaloCIE_Wave" };
                csvBuilder.AppendLine(string.Join(",", properties));
                foreach (var item in result.ViewResults.OfType<PoiPointResultModel>())
                {
                    var colorInfo = JsonConvert.DeserializeObject<ColorInformation>(item.Value) ?? new ColorInformation();

                    // 将ColorInformation对象的属性转换为字符串
                    var values = new List<string>
                    {
                        item.PoiName.ToString(),
                        colorInfo.KeyLv.ToString(),
                        colorInfo.HaloLv.ToString(),
                        colorInfo.KeyCIE.U.ToString(),
                        colorInfo.KeyCIE.V.ToString(),
                        colorInfo.KeyCIE.X.ToString(),
                        colorInfo.KeyCIE.Y.ToString(),
                        colorInfo.KeyCIE.CCT.ToString(),
                        colorInfo.KeyCIE.Wave.ToString(),
                        colorInfo.HaloCIE.U.ToString(),
                        colorInfo.HaloCIE.V.ToString(),
                        colorInfo.HaloCIE.X.ToString(),
                        colorInfo.HaloCIE.Y.ToString(),
                        colorInfo.HaloCIE.CCT.ToString(),
                        colorInfo.HaloCIE.Wave.ToString()
                    };

                    csvBuilder.AppendLine(string.Join(",", values));
                }
            }
            else
            {
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
            }
           
            File.WriteAllText(selectedPath +"//" + result.Batch + ".csv", csvBuilder.ToString(), Encoding.UTF8);
        }
        public override void Load(AlgorithmView view, AlgorithmResult result)
        {
            if (result.ViewResults ==null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>(PoiPointResultDao.Instance.GetAllByPid(result.Id));
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmPoi), ImageFilePath = result.FilePath })) });
            }

        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.Count < 1000)
            {
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
            }
            else
            {
                log.Info($"点阵信息太多{result.ViewResults.Count}");
            }


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
