using System.Collections.Generic;
using System.Linq;
using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using ColorVision.Engine.Services.Types;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    public sealed class CalibrationControlRow : ViewModelBase
    {
        internal CalibrationControlRow(CalibrationSlotDefinition slot, CalibrationBase calibration, IEnumerable<CalibrationResource> resources)
        {
            SlotKey = slot.Key;
            Calibration = calibration;
            Resources = resources.Where(resource => resource.SysResourceModel.Type == (int)slot.ServiceType).ToList();
            Title = CalibrationSlotPresentation.GetTitle(slot.Key);
            RefreshStatus();
        }

        public string SlotKey { get; }
        public string Title { get; }
        public CalibrationBase Calibration { get; }
        public List<CalibrationResource> Resources { get; }
        public CalibrationResource? Resource => Resources.FirstOrDefault(resource => resource.Name == Calibration.FilePath);
        public string FileReference
        {
            get => Calibration.FilePath;
            set
            {
                if (Calibration.FilePath == value) return;
                Calibration.FilePath = value;
                Calibration.Id = Resource?.Id ?? 0;
                OnPropertyChanged();
                RefreshStatus();
            }
        }
        public string FileStatus => string.IsNullOrWhiteSpace(FileReference) ? "未配置" : Calibration.IsExitFile ? "本机存在" : "本机缺失";
        public void RefreshStatus()
        {
            Calibration.IsExitFile = Resource?.IsValid ?? false;
            OnPropertyChanged(nameof(Resource));
            OnPropertyChanged(nameof(FileStatus));
        }
    }

    /// <summary>
    /// 用于缓存校正参数的选中状态，在校正组切换时恢复上一次的选中状态
    /// </summary>
    sealed class TempCache
    {
        public string Cache { get; set; }
        private readonly Dictionary<string, bool> selectionStates = new();

        /// <summary>
        /// 从 CalibrationParam 中快照当前选中状态
        /// </summary>
        public void SaveFrom(CalibrationParam param)
        {
            Cache = param.CalibrationMode;
            selectionStates.Clear();
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                selectionStates[slot.Key] = slot.ParamGetter(param).IsSelected;
            }
        }

        /// <summary>
        /// 将缓存的选中状态恢复到 CalibrationParam
        /// </summary>
        public void RestoreTo(CalibrationParam param)
        {
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                if (selectionStates.TryGetValue(slot.Key, out var isSelected))
                {
                    slot.ParamGetter(param).IsSelected = isSelected;
                }
            }
        }

        /// <summary>
        /// 将所有选中状态重置为 false
        /// </summary>
        public static void ClearSelection(CalibrationParam param)
        {
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                slot.ParamGetter(param).IsSelected = false;
            }
        }
    }

    public partial class CalibrationControl : UserControl
    {
        public CalibrationParam CalibrationParam { get => _CalibrationParam; set { _CalibrationParam = value;} }
        private CalibrationParam _CalibrationParam;

        public PhyCamera PhyCamera { get; set; }

        public CalibrationControl(PhyCamera calibrationService)
        {
            PhyCamera = calibrationService;

            InitializeComponent();
            CalibrationParam = new CalibrationParam();
            DataContext = CalibrationParam;
        }

        public Dictionary<string, List<ZipCalibrationItem>> CalibrationModeList { get; set; }

        public CalibrationControl(PhyCamera calibrationService, CalibrationParam calibrationParam)
        {
            PhyCamera = calibrationService;
            InitializeComponent();
            CalibrationParam = calibrationParam;
            DataContext = CalibrationParam;
        }

        public ObservableCollection<GroupResource> groupResources { get; set; } = new ObservableCollection<GroupResource>();
        public ObservableCollection<CalibrationControlRow> NormalRows { get; } = new();
        public ObservableCollection<CalibrationControlRow> PrimaryColorRows { get; } = new();
        public ObservableCollection<CalibrationControlRow> SecondaryColorRows { get; } = new();
        TempCache TempCache { get; set; } = new TempCache();

        /// <summary>
        /// 从 GroupResource 同步校正文件的存在状态到 CalibrationParam
        /// </summary>
        private static void SyncFileExistence(CalibrationParam param, GroupResource groupResource)
        {
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                slot.ParamGetter(param).IsExitFile = slot.GroupGetter(groupResource)?.IsValid ?? false;
            }
        }

        /// <summary>
        /// 清空所有校正文件路径
        /// </summary>
        private static void ClearAllFilePaths(CalibrationParam param)
        {
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                slot.ParamGetter(param).FilePath = string.Empty;
            }
        }

        /// <summary>
        /// 从 GroupResource 同步校正文件路径和 ID 到 CalibrationParam
        /// </summary>
        private static void SyncFilePathsAndIds(CalibrationParam param, GroupResource groupResource)
        {
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                SetCalibrationFile(slot.ParamGetter(param), slot.GroupGetter(groupResource));
            }
        }

        /// <summary>
        /// 将单个 CalibrationResource 的文件名和 ID 赋值到 CalibrationBase
        /// </summary>
        private static void SetCalibrationFile(CalibrationBase calibBase, CalibrationResource resource)
        {
            calibBase.FilePath = resource?.Name ?? string.Empty;
            calibBase.Id = resource?.Id ?? 0;
        }

        public void Initializedsss(CalibrationParam calibrationParam)
        {
            ComboBoxList.SelectionChanged -= ComboBox_SelectionChanged;

            CalibrationParam = calibrationParam;
            DataContext = CalibrationParam;

            TempCache.SaveFrom(calibrationParam);

            ComboBoxList.Text = calibrationParam.CalibrationMode;
            ComboBoxList.SelectionChanged += ComboBox_SelectionChanged;

            if (string.IsNullOrWhiteSpace(calibrationParam.CalibrationMode) && groupResources.Count > 0)
            {
                ComboBoxList.SelectedIndex = 0;
            }

            if (ComboBoxList.SelectedValue is GroupResource groupResource)
            {
                SyncFileExistence(CalibrationParam, groupResource);
                RefreshRows(groupResource);
            }
        }


        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            uploadbutton.DataContext = PhyCamera;

            void UpdateDefaultStyle()   
            {
                string selectedName = CalibrationParam?.CalibrationMode;
                ComboBoxList.SelectionChanged -= ComboBox_SelectionChanged;
                groupResources.Clear();
                ComboBoxList.ItemsSource = groupResources;
                foreach (var item in PhyCamera.VisualChildren)
                {
                    if (item is GroupResource groupResource)
                    {
                        groupResource.SetCalibrationResource();
                        groupResources.Add(groupResource);
                    }
                }
                if (CalibrationParam != null)
                {
                    ComboBoxList.SelectedItem = groupResources.FirstOrDefault(group => group.Name == selectedName);
                    ComboBoxList.SelectionChanged += ComboBox_SelectionChanged;
                    RefreshRows(ComboBoxList.SelectedItem as GroupResource);
                }
            }

            PhyCamera.VisualChildren.CollectionChanged += (s, e) => UpdateDefaultStyle();
            UpdateDefaultStyle();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
            }
        }



        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                ClearAllFilePaths(CalibrationParam);

                if (comboBox.SelectedValue is GroupResource groupResource)
                {
                    if (groupResource.Name == TempCache.Cache)
                    {
                        TempCache.RestoreTo(CalibrationParam);
                    }
                    else
                    {
                        TempCache.ClearSelection(CalibrationParam);
                    }

                    SyncFileExistence(CalibrationParam, groupResource);
                    SyncFilePathsAndIds(CalibrationParam, groupResource);
                    RefreshRows(groupResource);
                }
                else
                {
                    RefreshRows(null);
                }
            }
        }

        private void RefreshRows(GroupResource? groupResource)
        {
            NormalRows.Clear();
            PrimaryColorRows.Clear();
            SecondaryColorRows.Clear();

            if (groupResource == null)
                return;

            foreach (var slot in CalibrationSlotDefinitions.NormalSlots)
            {
                if (string.IsNullOrWhiteSpace(slot.ParamGetter(CalibrationParam).FilePath))
                    continue;
                NormalRows.Add(new CalibrationControlRow(slot, slot.ParamGetter(CalibrationParam), PhyCamera.VisualChildren.OfType<CalibrationResource>()));
            }

            foreach (var slot in CalibrationSlotDefinitions.ColorSlots)
            {
                if (string.IsNullOrWhiteSpace(slot.ParamGetter(CalibrationParam).FilePath))
                    continue;

                ObservableCollection<CalibrationControlRow> target = slot.ServiceType is ServiceTypes.Luminance or ServiceTypes.LumFourColor
                    ? PrimaryColorRows
                    : SecondaryColorRows;
                target.Add(new CalibrationControlRow(slot, slot.ParamGetter(CalibrationParam), PhyCamera.VisualChildren.OfType<CalibrationResource>()));
            }
        }

        private void OpenCalibrationItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if ((sender as System.Windows.FrameworkElement)?.DataContext is CalibrationControlRow row)
                row.Resource?.Open();
        }

        private void EditCalibrationItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if ((sender as System.Windows.FrameworkElement)?.DataContext is CalibrationControlRow row)
                row.Resource?.Edit();
        }

        private void OpenCalibrationManager_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CalibrationEdit calibrationEdit = new(PhyCamera, ComboBoxList.SelectedIndex)
            {
                Owner = System.Windows.Window.GetWindow(this),
            };
            calibrationEdit.ShowDialog();
            RefreshRows(ComboBoxList.SelectedItem as GroupResource);
        }

    }


}
