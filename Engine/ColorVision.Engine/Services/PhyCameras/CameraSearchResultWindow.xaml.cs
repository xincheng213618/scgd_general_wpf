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

            foreach (var cameraGroup in GroupDiscoveredCameras(summary.Cameras))
            {
                Cameras.Add(new CameraSearchCameraViewModel(cameraGroup, phyCameraManager, cameraGroup.Any(camera => existingCodes.Contains(camera.MD5Id))));
            }

            SummaryText = string.Format(Properties.Resources.FoundCamerasCount, Cameras.Count);
            ElapsedText = string.Format(Properties.Resources.SearchTotalElapsed, FormatElapsed(summary.Elapsed));
        }

        private static IEnumerable<IReadOnlyList<cvCameraCSLib.CameraDiscoveryItem>> GroupDiscoveredCameras(IEnumerable<cvCameraCSLib.CameraDiscoveryItem> cameras)
        {
            return cameras
                .GroupBy(GetCameraGroupKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(item => item.CameraModel)
                    .ThenBy(item => item.CameraId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ToList())
                .OrderBy(group => group[0].CameraModel)
                .ThenBy(group => group[0].CameraId ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        }

        private static string GetCameraGroupKey(cvCameraCSLib.CameraDiscoveryItem camera)
        {
            if (!string.IsNullOrWhiteSpace(camera.MD5Id))
            {
                return $"md5:{camera.MD5Id}";
            }

            return $"id:{camera.CameraId}|model:{camera.CameraModel}";
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
        public string SupportedCameraModelsText { get; }
        public string CameraId { get; }
        public string MD5Id { get; }
        public string SearchElapsedText { get; }
        public RelayCommand CreateCommand { get; }

        public bool IsCreated { get => _IsCreated; private set { _IsCreated = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanCreate)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(ActionText)); } }
        private bool _IsCreated;

        public bool CanCreate => !IsCreated;
        public string StatusText => IsCreated ? Properties.Resources.CameraStatusOnline : Properties.Resources.CameraStatusNotCreated;
        public string ActionText => IsCreated ? Properties.Resources.CameraActionCreated : Properties.Resources.CameraActionCreate;

        public CameraSearchCameraViewModel(IReadOnlyList<cvCameraCSLib.CameraDiscoveryItem> cameras, PhyCameraManager phyCameraManager, bool isCreated)
        {
            if (cameras == null || cameras.Count == 0)
            {
                throw new ArgumentException("At least one discovered camera is required.", nameof(cameras));
            }

            var primaryCamera = cameras[0];

            _phyCameraManager = phyCameraManager;
            CameraModel = primaryCamera.CameraModel;
            SupportedCameraModelsText = string.Join(" / ", cameras.Select(camera => camera.CameraModel.ToString()).Distinct(StringComparer.Ordinal));
            CameraId = cameras.Select(camera => camera.CameraId).FirstOrDefault(cameraId => !string.IsNullOrWhiteSpace(cameraId)) ?? string.Empty;
            MD5Id = cameras.Select(camera => camera.MD5Id).FirstOrDefault(cameraId => !string.IsNullOrWhiteSpace(cameraId)) ?? string.Empty;
            SearchElapsedText = CameraSearchResultViewModel.FormatElapsed(cameras.Max(camera => camera.SearchElapsed));
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