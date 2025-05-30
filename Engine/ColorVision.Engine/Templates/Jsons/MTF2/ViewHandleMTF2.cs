#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.Jsons.Ghost2;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Extension;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ColorVision.Engine.Templates.Jsons.MTF2
{
    public class MTFItem
    {
        public string name { get; set; }
        public double mtfValue { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public int w { get; set; }
        public int h { get; set; }

    }

    public class MTFResult
    {
        public List<MTFItem> result { get; set; }
    }


    public class MTFDetailViewReslut : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public MTFDetailViewReslut(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;

            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (File.Exists(ResultFileName))
            {
                MTFResult = JsonConvert.DeserializeObject<MTFResult>(File.ReadAllText(ResultFileName));
            }

        }
        [Column("id")]
        public int Id { get; set; }
        [Column("pid")]
        public int PId { get; set; }
        public string? ResultFileName { get; set; }

        public MTFResult? MTFResult { get; set; }
    }


    public class ViewHandleMTF2 : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleMTF2));

        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.MTF};
        public override bool CanHandle1(AlgorithmResult result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }


        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            //var blackMuraViews = mtfresult.ViewResults.ToSpecificViewResults<GhostView>();
            //var csvBuilder = new StringBuilder();
            //if (blackMuraViews.Count == 1)
            //{
            //    string filePath = selectedPath + "//" + mtfresult.Batch + mtfresult.ResultType + ".json";
            //    File.AppendAllText(filePath, blackMuraViews[0].Result, Encoding.UTF8);
            //}
        }


        public override void Load(AlgorithmView view, AlgorithmResult result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                if (detailCommonModels.Count == 1)
                {
                    MTFDetailViewReslut mtfresult = new MTFDetailViewReslut(detailCommonModels[0]);
                    result.ViewResults.Add(mtfresult);

                    RelayCommand SelectrelayCommand = new RelayCommand(a =>
                    {
                        PlatformHelper.OpenFolderAndSelectFile(mtfresult.ResultFileName);

                    }, a => File.Exists(mtfresult.ResultFileName));

                    RelayCommand OpenrelayCommand = new RelayCommand(a =>
                    {
                        AvalonEditWindow avalonEditWindow = new AvalonEditWindow(mtfresult.ResultFileName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        avalonEditWindow.ShowDialog();
                    }, a => File.Exists(mtfresult.ResultFileName));


                    result.ContextMenu.Items.Add(new MenuItem() { Header = "选中2.0结果集", Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = "打开2.0结果集", Command = OpenrelayCommand });
                }


            }
        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();

            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            Load(view, result);



            if (result.ViewResults.Count == 1)
            {
                if (result.ViewResults[0] is MTFDetailViewReslut mTFDetailViewReslut)
                {
                    int id = 0;
                    if (mTFDetailViewReslut.MTFResult.result.Count != 0)
                    {
                        foreach (var item in mTFDetailViewReslut.MTFResult.result)
                        {
                            id++;
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.x,item.y,item.w,item.h);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Rectangle.Attribute.Id = id;
                            Rectangle.Attribute.Text = item.name;
                            Rectangle.Attribute.Msg = item.mtfValue.ToString();
                            Rectangle.Render();
                            view.ImageView.AddVisual(Rectangle);
                        }
                    }



                    List<string> header = new() { "name", "x","y","w","h","mtfvalue" };
                    List<string> bdHeader = new() { "name", "x", "y", "w", "h", "mtfValue" };

                    if (view.listViewSide.View is GridView gridView)
                    {
                        view.LeftGridViewColumnVisibilitys.Clear();
                        gridView.Columns.Clear();
                        for (int i = 0; i < header.Count; i++)
                            gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                        view.listViewSide.ItemsSource = mTFDetailViewReslut?.MTFResult?.result;
                    }
                }
            }
            else
            {

            }



        }



    }
}
