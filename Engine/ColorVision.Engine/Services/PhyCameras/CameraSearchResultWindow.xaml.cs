using ColorVision.Common.MVVM;
using ColorVision.Themes;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class CameraSearchResultViewModel : ViewModelBase
    {
        public ObservableCollection<CameraSearchModelViewModel> Models { get; } = new ObservableCollection<CameraSearchModelViewModel>();
        public ObservableCollection<CameraSearchCameraViewModel> Cameras { get; } = new ObservableCollection<CameraSearchCameraViewModel>();

        public string SummaryText { get; }
        public string ElapsedText { get; }

        public CameraSearchResultViewModel()
        {
            SummaryText = string.Empty;
            ElapsedText = string.Empty;
        }

        public CameraSearchResultViewModel(cvCameraCSLib.CameraDiscoverySummary summary, PhyCameraManager phyCameraManager)
        {
            var existingCodes = new HashSet<string>(phyCameraManager.PhyCameras.Select(a => a.Code).Where(a => !string.IsNullOrWhiteSpace(a)), StringComparer.OrdinalIgnoreCase);

            foreach (var model in summary.Models)
            {
                Models.Add(new CameraSearchModelViewModel(model));
            }

            foreach (var camera in summary.Cameras.OrderBy(a => a.CameraModel).ThenBy(a => a.CameraId))
            {
                Cameras.Add(new CameraSearchCameraViewModel(camera, phyCameraManager, existingCodes.Contains(camera.MD5Id)));
            }

            SummaryText = string.Format(Properties.Resources.FoundCamerasCount, Cameras.Count);
            ElapsedText = string.Format(Properties.Resources.SearchTotalElapsed, FormatElapsed(summary.Elapsed));
        }

        public static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds >= 1)
            {
                return $"{elapsed.TotalSeconds:F2}s";
            }
            return $"{elapsed.TotalMilliseconds:F0}ms";
        }
    }

    public class CameraSearchModelViewModel
    {
        public CameraModel CameraModel { get; }
        public int CameraCount { get; }
        public string ElapsedText { get; }
        public string StatusText { get; }

        public CameraSearchModelViewModel(cvCameraCSLib.CameraDiscoveryModelResult modelResult)
        {
            CameraModel = modelResult.CameraModel;
            CameraCount = modelResult.CameraCount;
            ElapsedText = CameraSearchResultViewModel.FormatElapsed(modelResult.Elapsed);
            StatusText = modelResult.Success ? (modelResult.CameraCount > 0 ? Properties.Resources.CameraSearchSuccess : Properties.Resources.CameraSearchNotFound) : string.Format(Properties.Resources.CameraSearchFailed, modelResult.ErrorMessage);
        }
    }

    public class CameraSearchCameraViewModel : ViewModelBase
    {
        private readonly PhyCameraManager _phyCameraManager;

        public CameraModel CameraModel { get; }
        public string CameraId { get; }
        public string MD5Id { get; }
        public string SearchElapsedText { get; }
        public RelayCommand CreateCommand { get; }

        public bool IsCreated { get => _IsCreated; private set { _IsCreated = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanCreate)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(ActionText)); } }
        private bool _IsCreated;

        public bool CanCreate => !IsCreated;
        public string StatusText => IsCreated ? Properties.Resources.CameraStatusOnline : Properties.Resources.CameraStatusNotCreated;
        public string ActionText => IsCreated ? Properties.Resources.CameraActionCreated : Properties.Resources.CameraActionCreate;

        public CameraSearchCameraViewModel(cvCameraCSLib.CameraDiscoveryItem camera, PhyCameraManager phyCameraManager, bool isCreated)
        {
            _phyCameraManager = phyCameraManager;
            CameraModel = camera.CameraModel;
            CameraId = camera.CameraId;
            MD5Id = camera.MD5Id;
            SearchElapsedText = CameraSearchResultViewModel.FormatElapsed(camera.SearchElapsed);
            _IsCreated = isCreated;
            CreateCommand = new RelayCommand(a => Create(), a => CanCreate);
        }

        private void Create()
        {
            if (!CanCreate)
            {
                return;
            }

            var createWindow = new CreateWindow(_phyCameraManager, MD5Id, CameraId, CameraModel)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (createWindow.ShowDialog() == true)
            {
                _phyCameraManager.LoadPhyCamera();
                IsCreated = true;
            }
        }
    }

    public partial class CameraSearchResultWindow : Window
    {
        public CameraSearchResultWindow(CameraSearchResultViewModel viewModel)
        {
            InitializeComponent();
            this.ApplyCaption();
            DataContext = viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}