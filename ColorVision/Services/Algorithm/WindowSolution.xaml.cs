using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Services.Algorithm
{


    public static class MySQLHelper
    {
        private static BatchResultMasterDao BatchResultMasterDao { get; set; } = new BatchResultMasterDao();

        public static BatchResultMasterModel? GetBatch(int id) => id >= 0 ? BatchResultMasterDao.GetByID(id) : null;


        private static MeasureImgResultDao MeasureImgResultDao { get; set;  } = new MeasureImgResultDao();

        public static MeasureImgResultModel? GetMeasureResultImg(int id) => id >= 0 ? MeasureImgResultDao.GetByID(id) : null;


    }

    public class FOVResult : ViewModelBase
    {

        public AlgorithmFovResultModel Model { get; set; }
        public FOVResult(AlgorithmFovResultModel algorithmFovResultModel)
        {
            Model = algorithmFovResultModel;
            Batch = MySQLHelper.GetBatch(algorithmFovResultModel.BatchId);
            IMG = MySQLHelper.GetMeasureResultImg(algorithmFovResultModel.ImgId);
        }
        public BatchResultMasterModel? Batch { get; set; }

        public MeasureImgResultModel? IMG{ get; set; }

    }



    /// <summary>
    /// WindowSolution.xaml 的交互逻辑
    /// </summary>
    public partial class WindowSolution : Window
    {
        public WindowSolution()
        {
            InitializeComponent();
        }

        public ObservableCollection<PoiResult> PoiResults { get; set; } = new ObservableCollection<PoiResult>();

        public ObservableCollection<FOVResult> FOVResults { get; set; } = new ObservableCollection<FOVResult>();


        private void Window_Initialized(object sender, EventArgs e)
        {
            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号", "批次号", "模板", "图像数据文件", "测量时间", "执行结果" };
            List<string> bdheaders = new List<string> { "Id", "SerialNumber", "POITemplateName", "ImgFileName", "RecvTime", "ResultTypeDis" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            ListView1.View = gridView;
            ListView1.ItemsSource = PoiResults;

            BatchResultMasterDao batchDao = new BatchResultMasterDao();
            var batchlist = batchDao.GetAll(0);

            foreach (var item in batchlist)
            {
                PoiResult poiResult = new PoiResult();
                poiResult.Id = item.Id;
                poiResult.SerialNumber = item.Name;
                PoiResults.Add(poiResult);
            }
        }


        public void FOV()
        {
            AlgorithmFovResult algorithmFovResult = new AlgorithmFovResult();

            var algorithmFovResults = algorithmFovResult.GetAll();

            foreach (var item in algorithmFovResults)
            {
                FOVResults.Add(new FOVResult(item));
            }
            algorithmFovResults.Clear();


            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号", "批次号", "img_id", "fovDegrees", "coordinates1", "coordinates2", "coordinates3", "coordinates4", "执行结果" };
            List<string> bdheaders = new List<string> { "Model.Id", "Batch.Code", "Model.ImgId", "Model.FovDegrees", "Model.Coordinates1", "Model.Coordinates2", "Model.Coordinates3", "Model.Coordinates4", "Model.Result" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            ListView1.View = gridView;
            ListView1.ItemsSource = FOVResults;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                switch (button.Tag.ToString())
                {
                    case "Fov":
                        FOV();
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
