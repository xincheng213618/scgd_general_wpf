#pragma warning disable CA1863
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    public sealed class CalibrationTemplateCloneSelectionItem : ViewModelBase
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string CalibrationMode { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _IsSelected;
            set
            {
                _IsSelected = value;
                OnPropertyChanged();
            }
        }
        private bool _IsSelected = true;
    }

    public partial class CalibrationTemplateCloneWindow : Window
    {
        public PhyCamera TargetCamera { get; }
        public ObservableCollection<PhyCamera> SourceCameras { get; } = new();
        public ObservableCollection<CalibrationTemplateCloneSelectionItem> Templates { get; } = new();

        public PhyCamera? SelectedSourceCamera
        {
            get => _SelectedSourceCamera;
            set
            {
                _SelectedSourceCamera = value;
                LoadTemplates(value);
            }
        }
        private PhyCamera? _SelectedSourceCamera;

        public CalibrationTemplateCloneWindow(PhyCamera targetCamera)
        {
            TargetCamera = targetCamera ?? throw new ArgumentNullException(nameof(targetCamera));

            foreach (PhyCamera camera in PhyCameraManager.GetInstance().PhyCameras.Where(camera => camera.Id != targetCamera.Id))
            {
                CalibrationParam.LoadResourceParams(camera.CalibrationParams, camera.Id);
                SourceCameras.Add(camera);
            }

            InitializeComponent();
            this.ApplyCaption();
            DataContext = this;

            SelectedSourceCamera = SourceCameras.FirstOrDefault(camera => camera.CalibrationParams.Count > 0)
                ?? SourceCameras.FirstOrDefault();
            SourceCameraComboBox.SelectedItem = SelectedSourceCamera;
        }

        private void SourceCameraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadTemplates(SourceCameraComboBox.SelectedItem as PhyCamera);
        }

        private void LoadTemplates(PhyCamera? sourceCamera)
        {
            Templates.Clear();
            if (sourceCamera == null)
                return;

            CalibrationParam.LoadResourceParams(sourceCamera.CalibrationParams, sourceCamera.Id);
            foreach (TemplateModel<CalibrationParam> template in sourceCamera.CalibrationParams)
            {
                Templates.Add(new CalibrationTemplateCloneSelectionItem
                {
                    Id = template.Id,
                    Name = template.Key,
                    CalibrationMode = template.Value.CalibrationMode,
                    IsSelected = true
                });
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (CalibrationTemplateCloneSelectionItem template in Templates)
                template.IsSelected = true;
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (CalibrationTemplateCloneSelectionItem template in Templates)
                template.IsSelected = false;
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            PhyCamera? sourceCamera = SourceCameraComboBox.SelectedItem as PhyCamera;
            if (sourceCamera == null)
            {
                MessageBox.Show(this, Properties.Resources.CloneCalibrationTemplatesNoSource, Properties.Resources.CloneCalibrationTemplates, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<int> selectedIds = Templates.Where(template => template.IsSelected).Select(template => template.Id).ToList();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show(this, Properties.Resources.CloneCalibrationTemplatesNoSelection, Properties.Resources.CloneCalibrationTemplates, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                CalibrationTemplateCloneResult result = CalibrationTemplateCloneService.Clone(sourceCamera, TargetCamera, selectedIds);
                List<string> messages = new()
                {
                    string.Format(Properties.Resources.CloneCalibrationTemplatesCompleted, result.ClonedCount, result.SkippedNames.Count)
                };

                if (result.NeedsConfigurationNames.Count > 0)
                {
                    messages.Add(string.Format(
                        Properties.Resources.CloneCalibrationTemplatesNeedsConfiguration,
                        string.Join(", ", result.NeedsConfigurationNames)));
                }

                MessageBox.Show(this, string.Join(Environment.NewLine + Environment.NewLine, messages), Properties.Resources.CloneCalibrationTemplates, MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(Properties.Resources.CloneCalibrationTemplatesFailure, ex.Message), Properties.Resources.CloneCalibrationTemplates, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
