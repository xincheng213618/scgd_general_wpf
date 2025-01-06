using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public interface ISearch
    {

    }

    public class SearchViewModel<T>:ViewModelBase , ISearch where T: IPKModel,new()
    {
        public BaseTableDao<T> TableDao { get; set; }

        public SearchViewModel(BaseTableDao<T> tableDao) 
        {
            TableDao = tableDao;
        }

        private List<T> ConditionalQuery(Dictionary<string, object> param, int limit = -1)
        {
            return TableDao.ConditionalQuery(param, limit);
        }
    }

    public class AdvanceSearchConfig : ViewModelBase,IConfig
    {
        public static AdvanceSearchConfig Instance => ConfigService.Instance.GetRequiredService<AdvanceSearchConfig>();

        public int Limit { get => _Limit; set { _Limit = value; NotifyPropertyChanged(); } }
        private int _Limit = 100;

        public DateTime SearchTimeStart { get => _SearchTimeStart; set { _SearchTimeStart = value; NotifyPropertyChanged(); } }
        private DateTime _SearchTimeStart = DateTime.MinValue;

        [JsonIgnore]
        public DateTime SearchTimeEnd { get => _SearchTimeEnd; set { _SearchTimeEnd = value; NotifyPropertyChanged(); } }
        private DateTime _SearchTimeEnd = DateTime.Now;
    }

    /// <summary>
    /// AdvanceSearch.xaml 的交互逻辑
    /// </summary>
    public partial class AdvanceSearch : Window
    {
        MeasureImgResultDao MeasureImgResultDao { get; set; }
        public AdvanceSearch(MeasureImgResultDao searchViewModel)
        {
            InitializeComponent();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = AdvanceSearchConfig.Instance;
            SearchTimeStart.DisplayDateTime = DateTime.MinValue;
            SearchTimeEnd.DisplayDateTime = DateTime.Now;

        }
        public List<MeasureImgResultModel> SearchResults { get; set; } = new List<MeasureImgResultModel>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (DateTimeAll.IsChecked ==true)
            {
                SearchTimeStart.DisplayDateTime = DateTime.MinValue; 
                SearchTimeEnd.DisplayDateTime = DateTime.Now;
            }
            if (DateTime1.IsChecked == true)
            {
                SearchTimeStart.DisplayDateTime = DateTime.Now.AddDays(-1);
                SearchTimeEnd.DisplayDateTime = DateTime.Now;
            }
            if (DateTime7.IsChecked == true)
            {
                SearchTimeStart.DisplayDateTime = DateTime.Now.AddDays(-7);
                SearchTimeEnd.DisplayDateTime = DateTime.Now;
            }
            if (DateTime30.IsChecked == true)
            {
                SearchTimeStart.DisplayDateTime = DateTime.Now.AddDays(-30);
                SearchTimeEnd.DisplayDateTime = DateTime.Now;
            }
            if (DateTimeAuto.IsChecked == true)
            {

            }
            int limit = -1;
            if(limitall.IsChecked == true)
                limit = -1;
            if (limitl0.IsChecked == true)
                limit = 10;
            if (limit20.IsChecked == true)
                limit = 20;
            if (limit50.IsChecked == true)
                limit = 50;
            if (limitAuto.IsChecked == true)
                limit = AdvanceSearchConfig.Instance.Limit;

            SearchResults = MeasureImgResultDao.Instance.ConditionalQuery(TextBoxId.Text, TextBoxFile.Text, TbDeviceCode.Text, SearchTimeStart.DisplayDateTime, SearchTimeEnd.DisplayDateTime, limit);
            this.Close();
        }


    }
}
