using ColorVision.Common.MVVM;
using ColorVision.Themes;
using cvColorVision;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class CameraModelSelectionItem : ViewModelBase
    {
        public CameraModel CameraModel { get; set; }
        public string Name => CameraModel.ToString();

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; OnPropertyChanged(); } }
        private bool _IsSelected;
    }

    public partial class CameraSearchTypeWindow : Window
    {
        public ObservableCollection<CameraModelSelectionItem> CameraModels { get; } = new ObservableCollection<CameraModelSelectionItem>();

        public CameraSearchTypeWindow()
        {
            InitializeComponent();
            this.ApplyCaption();

            foreach (CameraModel cameraModel in Enum.GetValues<CameraModel>().Cast<CameraModel>())
            {
                CameraModels.Add(new CameraModelSelectionItem
                {
                    CameraModel = cameraModel,
                    IsSelected = cvCameraCSLib.DefaultFastSearchCameraModels.Contains(cameraModel)
                });
            }

            DataContext = this;
        }

        public CameraModel[] SelectedCameraModels => CameraModels.Where(a => a.IsSelected).Select(a => a.CameraModel).ToArray();

        private void Default_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in CameraModels)
            {
                item.IsSelected = cvCameraCSLib.DefaultFastSearchCameraModels.Contains(item.CameraModel);
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in CameraModels)
            {
                item.IsSelected = true;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in CameraModels)
            {
                item.IsSelected = false;
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCameraModels.Length == 0)
            {
                MessageBox.Show(this, Properties.Resources.SelectCameraType, Properties.Resources.Search, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}