using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            var batchlist =  batchDao.GetAll(0);

            foreach (var item in batchlist)
            {
                PoiResult poiResult = new PoiResult();
                poiResult.Id = item.Id;
                poiResult.SerialNumber = item.Name;
                PoiResults.Add(poiResult);
            }
        }
    }
}
