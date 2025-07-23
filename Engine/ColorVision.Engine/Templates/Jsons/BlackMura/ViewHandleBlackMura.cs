#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{

    public class BlackMuraConfig : ViewModelBase, IConfig,IConfigSettingProvider
    {

        public static BlackMuraConfig Instance =>  ConfigService.Instance.GetRequiredService<BlackMuraConfig>();

        public double WLvMaxScale { get => _WLvMaxScale; set { _WLvMaxScale = value; NotifyPropertyChanged(); } }
        private double _WLvMaxScale = 1;

        public double WLvMinScale { get => _WLvMinScale; set { _WLvMinScale = value; NotifyPropertyChanged(); } }
        private double _WLvMinScale = 1;

        public double WZaRelMaxScale { get => _WZaRelMaxScale; set { _WZaRelMaxScale = value; NotifyPropertyChanged(); } }
        private double _WZaRelMaxScale = 1;


        public double BLvMaxScale { get => _BLvMaxScale; set { _BLvMaxScale = value; NotifyPropertyChanged(); } }
        private double _BLvMaxScale = 1;

        public double BLvMinScale { get => _BLvMinScale; set { _BLvMinScale = value; NotifyPropertyChanged(); } }
        private double _BLvMinScale = 1;

        public double BZaRelMaxScale { get => _BZaRelMaxScale; set { _BZaRelMaxScale = value; NotifyPropertyChanged(); } }
        private double _BZaRelMaxScale = 1;



        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                new ConfigSettingMetadata
                {
                    Name = "BlackMura",
                    Description = "BlackMura",
                    Order = 8,
                    Type = ConfigSettingType.Class,
                    BindingName =nameof(WLvMaxScale),
                    Source = Instance,
                },
            };
        }
    }


    public class FloatPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

         public Point ToPoint()
        {
            return new Point(X, Y);
        }
    }



    public class ViewHandleBlackMura : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleBlackMura));

        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.BlackMura_Calc};

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',' ) || field.Contains('"') || field.Contains('\n'))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }
            return field;
        }

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            var blackMuraViews = result.ViewResults.ToSpecificViewResults<BlackMuraView>();
            var csvBuilder = new StringBuilder();

            List<string> header = new List<string>();
            var properties = typeof(BlackMuraView).GetProperties();

            // 递归构建头部
            foreach (var prop in properties)
            {
                var columnName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    var nestedProperties = prop.PropertyType.GetProperties();
                    foreach (var nestedProp in nestedProperties)
                    {
                        var nestedColumnName = $"{nestedProp.Name}";
                        header.Add(nestedColumnName);
                    }
                }
                else
                {
                    header.Add(columnName);
                }
            }

            string filePath = selectedPath + "//" + result.ResultType + ".csv";

            // 检查文件是否存在
            if (File.Exists(filePath))
            {
                // 读取文件末尾的几行，检查是否包含头信息
                var lines = File.ReadLines(filePath).ToList();
                if (!lines.Any(line => line == string.Join(",", header)))
                {
                    // 如果文件存在但不含头信息，则追加头信息
                    File.AppendAllText(filePath, "\n" + string.Join(",", header) + "\n", Encoding.UTF8);
                }
            }
            else
            {
                // 文件不存在，创建新文件并写入头信息
                File.WriteAllText(filePath, string.Join(",", header) + "\n", Encoding.UTF8);
            }

            // 追加内容
            foreach (var item in blackMuraViews)
            {
                List<string> content = new List<string>();
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item);
                    if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                    {
                        var nestedProperties = prop.PropertyType.GetProperties();
                        foreach (var nestedProp in nestedProperties)
                        {
                            var nestedValue = nestedProp.GetValue(value);
                            content.Add(EscapeCsvField(nestedValue?.ToString() ?? string.Empty));
                        }
                    }
                    else
                    {
                        content.Add(EscapeCsvField(value?.ToString() ?? string.Empty));
                    }
                }
                csvBuilder.AppendLine(string.Join(",", content));
            }

            // 追加内容到文件
            File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);

        }


        public override void Load(AlgorithmView view, AlgorithmResult result)
        {
            if (result.ViewResults == null)
            {
                void OpenSource()
                {
                    view.ImageView.ImageShow.Clear();
                    foreach (var item in result.ViewResults)
                    {
                        if (item is BlackMuraView blackMuraModel)
                        {
                            if (File.Exists(result.FilePath))
                                view.ImageView.OpenImage(result.FilePath);
                            log.Info(result.FilePath);

                            if (!string.IsNullOrEmpty(blackMuraModel.AreaJsonVal))
                            {
                                List<FloatPoint> floatPoints = JsonConvert.DeserializeObject<List<FloatPoint>>(blackMuraModel.AreaJsonVal);
                                log.Info(floatPoints.Count);
                                DVPolygon dVPolygon = new DVPolygon();
                                dVPolygon.Attribute.Brush = Brushes.Transparent;
                                dVPolygon.Attribute.Pen = new Pen(Brushes.Red, 1);
                                foreach (var item1 in floatPoints)
                                {
                                    dVPolygon.Points.Add(item1.ToPoint());
                                }
                                dVPolygon.IsComple = true;
                                view.ImageView.AddVisual(dVPolygon);
                                log.Info(dVPolygon);
                            }
                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                view.ImageView.Zoombox1.ZoomUniform();
                            });
                        }
                    }
                }


                void OpenAA()
                {
                    view.ImageView.ImageShow.Clear();
                    foreach (var item in result.ViewResults)
                    {
                        if (item is BlackMuraView blackMuraModel)
                        {
                            Outputfile outputfile = blackMuraModel.Outputfile;
                            if (File.Exists(outputfile.AAPath))
                                view.ImageView.OpenImage(outputfile.AAPath);


                            ResultJson lvDetails = blackMuraModel.ResultJson;
                            DVCircleText maxcirle = new();
                            maxcirle.Attribute.Center = new System.Windows.Point(lvDetails.MaxPtX, lvDetails.MaxPtY);
                            maxcirle.Attribute.Radius = lvDetails.Nle;
                            maxcirle.Attribute.Brush = Brushes.Transparent;
                            maxcirle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            maxcirle.Attribute.Id = -1;
                            maxcirle.Attribute.Text = string.Empty;
                            maxcirle.Attribute.Msg = $"Max({lvDetails.MaxPtX},{lvDetails.MaxPtY}) Lv:{lvDetails.LvMax}";
                            maxcirle.Render();
                            view.ImageView.AddVisual(maxcirle);

                            DVCircleText mincirle = new();    
                            mincirle.Attribute.Center = new System.Windows.Point(lvDetails.MinPtX, lvDetails.MinPtY);
                            mincirle.Attribute.Radius = lvDetails.Nle;
                            mincirle.Attribute.Brush = Brushes.Transparent;
                            mincirle.Attribute.Pen = new Pen(Brushes.Yellow, 1);
                            mincirle.Attribute.Id = -1;
                            mincirle.Attribute.Text = string.Empty;
                            mincirle.Attribute.Msg = $"Min({lvDetails.MinPtX},{lvDetails.MinPtY}) Lv:{lvDetails.LvMin}";
                            mincirle.Render();
                            view.ImageView.AddVisual(mincirle);
                        }
                    }

                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        view.ImageView.Zoombox1.ZoomUniform();
                    });
                }

                result.ViewResults = new ObservableCollection<IViewResult>();
                List<BlackMuraModel> AlgResultModels = BlackMuraDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultModels)
                {
                    BlackMuraView blackMuraView = new BlackMuraView(item);
                    blackMuraView.ResultJson.LvMax = blackMuraView.ResultJson.LvMax * BlackMuraConfig.Instance.WLvMaxScale;
                    blackMuraView.ResultJson.LvMin = blackMuraView.ResultJson.LvMin * BlackMuraConfig.Instance.WLvMinScale;
                    blackMuraView.ResultJson.ZaRelMax = blackMuraView.ResultJson.ZaRelMax * BlackMuraConfig.Instance.WZaRelMaxScale;
                    blackMuraView.ResultJson.Uniformity =  blackMuraView.ResultJson.LvMin / blackMuraView.ResultJson.LvMax * 100;
                    result.ViewResults.Add(blackMuraView);
                }


                var ContextMenu = result.ContextMenu;
                RelayCommand relayCommand = new RelayCommand(a => OpenSource());
                ContextMenu.Items.Add(new MenuItem() { Header = "切分示意图", Command = relayCommand });
                RelayCommand relayCommand1 = new RelayCommand(a => OpenAA());
                ContextMenu.Items.Add(new MenuItem() { Header = "AA区", Command = relayCommand1 });

                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmBlackMura), ImageFilePath = result.FilePath })) });

            }
        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            void OpenSource()
            {
                view.ImageView.ImageShow.Clear();
                foreach (var item in result.ViewResults)
                {
                    if (item is BlackMuraView blackMuraModel)
                    {
                        if (File.Exists(result.FilePath))
                            view.ImageView.OpenImage(result.FilePath);
                        log.Info(result.FilePath);

                        if (!string.IsNullOrEmpty(blackMuraModel.AreaJsonVal))
                        {
                            List<FloatPoint> floatPoints = JsonConvert.DeserializeObject<List<FloatPoint>>(blackMuraModel.AreaJsonVal);
                            log.Info(floatPoints.Count);
                            DVPolygon dVPolygon = new DVPolygon();
                            dVPolygon.Attribute.Brush = Brushes.Transparent;
                            dVPolygon.Attribute.Pen = new Pen(Brushes.Red, 1);
                            foreach (var item1 in floatPoints)
                            {
                                dVPolygon.Points.Add(item1.ToPoint());
                            }
                            dVPolygon.IsComple = true;
                            view.ImageView.AddVisual(dVPolygon);
                            log.Info(dVPolygon);
                        }
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            view.ImageView.Zoombox1.ZoomUniform();
                        });
                    }
                }
            }
            OpenSource();

            List<string> header = new() { "LvAvg", "LvMax", "LvMin", "Uniformity(%)", "ZaRelMax", "AreaJsonVal" };
            List<string> bdHeader = new() { "ResultJson.LvAvg", "ResultJson.LvMax", "ResultJson.LvMin", "ResultJson.Uniformity", "ResultJson.ZaRelMax", "AreaJsonVal" };

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
