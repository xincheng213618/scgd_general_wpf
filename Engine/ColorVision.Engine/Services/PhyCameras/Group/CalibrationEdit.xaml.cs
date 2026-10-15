using LocalizedText = global::ColorVision.Engine.EngineLocalization;
#pragma warning disable CA1863
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Calibration;
using ColorVision.Engine.Services.PhyCameras.Calibration.Creation;
using ColorVision.Engine.Services.PhyCameras.Calibration.Editing;
using ColorVision.Engine.Services.Types;
using ColorVision.UI.Authorizations;
using ColorVision.Themes;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;


namespace ColorVision.Engine.Services.PhyCameras.Group
{
    public sealed class CalibrationEditRow : ViewModelBase
    {
        private readonly CalibrationSlotDefinition slot;

        internal CalibrationEditRow(CalibrationSlotDefinition slot, GroupResource group, ObservableCollection<CalibrationResource> resources)
        {
            this.slot = slot;
            Group = group;
            Resources = resources;
            Title = CalibrationSlotPresentation.GetTitle(slot.Key);
        }

        public string SlotKey => slot.Key;
        public ServiceTypes ServiceType => slot.ServiceType;
        public GroupResource Group { get; }
        public ObservableCollection<CalibrationResource> Resources { get; }
        public string Title { get; }
        public string FileStatus => SelectedResource == null ? "未配置" : SelectedResource.IsValid ? "本机存在" : "本机缺失";
        public bool CanCorrect => slot.ServiceType == ServiceTypes.LumFourColor;

        public CalibrationResource? SelectedResource
        {
            get => slot.GroupGetter(Group);
            set
            {
                if (ReferenceEquals(slot.GroupGetter(Group), value))
                    return;

                slot.GroupSetter(Group, value!);
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileStatus));
            }
        }

    }

    public partial class CalibrationEdit : Window
    {
        public PhyCamera PhyCamera { get; set; }

        private int Index;
        private bool isExporting;

        public CalibrationEdit(PhyCamera calibrationService , int index = 0)
        {
            PhyCamera = calibrationService;
            Index = index;

            InitializeComponent();
            this.ApplyCaption();
        }
        public ObservableCollection<GroupResource> groupResources { get; set; } = new ObservableCollection<GroupResource>();

        public ObservableCollection<CalibrationResource> DarkNoiseList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> DefectPointList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> DSNUList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> UniformityList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> DistortionList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> ColorShiftList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LuminanceList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LumOneColorList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LumFourColorList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LumMultiColorList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> ColorDiffList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> AngleShiftList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LineArityList { get; set; } = new ObservableCollection<CalibrationResource>();

        public ObservableCollection<CalibrationEditRow> NormalRows { get; } = new();
        public ObservableCollection<CalibrationEditRow> PrimaryColorRows { get; } = new();
        public ObservableCollection<CalibrationEditRow> SecondaryColorRows { get; } = new();

        private IReadOnlyDictionary<ServiceTypes, ObservableCollection<CalibrationResource>> CalibrationListsByServiceType => _CalibrationListsByServiceType ??= new Dictionary<ServiceTypes, ObservableCollection<CalibrationResource>>
        {
            [ServiceTypes.DarkNoise] = DarkNoiseList,
            [ServiceTypes.DefectPoint] = DefectPointList,
            [ServiceTypes.DSNU] = DSNUList,
            [ServiceTypes.Uniformity] = UniformityList,
            [ServiceTypes.Distortion] = DistortionList,
            [ServiceTypes.ColorShift] = ColorShiftList,
            [ServiceTypes.Luminance] = LuminanceList,
            [ServiceTypes.LumOneColor] = LumOneColorList,
            [ServiceTypes.LumFourColor] = LumFourColorList,
            [ServiceTypes.LumMultiColor] = LumMultiColorList,
            [ServiceTypes.ColorDiff] = ColorDiffList,
            [ServiceTypes.AngleShift] = AngleShiftList,
            [ServiceTypes.LineArity] = LineArityList,
        };
        private IReadOnlyDictionary<ServiceTypes, ObservableCollection<CalibrationResource>> _CalibrationListsByServiceType;


        public void Init()
        {
            groupResources.Clear();
            foreach (var calibrationList in CalibrationListsByServiceType.Values)
            {
                calibrationList.Clear();
            }

            foreach (var item in PhyCamera.VisualChildren)
            {
                if (item is GroupResource groupResource)
                {
                    groupResource.SetCalibrationResource();
                    groupResources.Add(groupResource);
                }
                if (item is CalibrationResource calibrationResource
                    && CalibrationListsByServiceType.TryGetValue((ServiceTypes)calibrationResource.SysResourceModel.Type, out var calibrationList))
                {
                    calibrationList.Add(calibrationResource);
                }
            }

            ListView1.ItemsSource = groupResources;
            if (groupResources.Count > 0)
            {
                ListView1.SelectedIndex = Math.Clamp(Index, 0, groupResources.Count - 1);
                ShowGroup(groupResources[ListView1.SelectedIndex]);
            }
            else
            {
                ShowGroup(null);
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            Init();
            DataContext = PhyCamera;
            PhyCamera.VisualChildren.CollectionChanged += CameraChildrenChanged;
        }

        private void CameraChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e) => Init();

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Index = ListView1.SelectedIndex;
            ShowGroup(ListView1.SelectedItem as GroupResource);
        }

        private void ShowGroup(GroupResource? group)
        {
            StackPanelCab.DataContext = group;
            NormalRows.Clear();
            PrimaryColorRows.Clear();
            SecondaryColorRows.Clear();

            if (group == null)
                return;

            foreach (var slot in CalibrationSlotDefinitions.NormalSlots)
            {
                NormalRows.Add(CreateRow(slot, group));
            }

            foreach (var slot in CalibrationSlotDefinitions.ColorSlots)
            {
                ObservableCollection<CalibrationEditRow> target = slot.ServiceType is ServiceTypes.Luminance or ServiceTypes.LumFourColor
                    ? PrimaryColorRows
                    : SecondaryColorRows;
                target.Add(CreateRow(slot, group));
            }
        }

        private CalibrationEditRow CreateRow(CalibrationSlotDefinition slot, GroupResource group)
        {
            return new CalibrationEditRow(slot, group, CalibrationListsByServiceType[slot.ServiceType]);
        }

        private void GroupManagement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu is ContextMenu menu)
            {
                menu.PlacementTarget = button;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void OpenCalibration_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CalibrationEditRow row)
                row.SelectedResource?.Open();
        }

        private void EditCalibration_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CalibrationEditRow row)
                row.SelectedResource?.Edit();
        }

        private void CreateCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || !AccessControl.Check(PermissionMode.Administrator)) return;
            if (ListView1.SelectedItem is not GroupResource group)
            {
                MessageBox.Show(this, "请先选择或添加校正组。", Properties.Resources.CalibrationFileManagement);
                return;
            }
            var menu = new ContextMenu();
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                var item = new MenuItem { Header = CalibrationSlotPresentation.GetTitle(slot.Key) };
                AddCreationChoices(item.Items, slot, group);
                menu.Items.Add(item);
            }
            menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
        }

        private void CreateCalibrationRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CalibrationEditRow row || !AccessControl.Check(PermissionMode.Administrator)) return;
            var menu = new ContextMenu();
            AddCreationChoices(menu.Items, CalibrationSlotDefinitions.ByKey[row.SlotKey], row.Group);
            menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
        }

        private void AddCreationChoices(ItemCollection items, CalibrationSlotDefinition slot, GroupResource group)
        {
            if (CalibrationCreationWindow.SupportsType(slot.ServiceType.ToCalibrationType()))
            {
                var measured = new MenuItem { Header = "从原始图像生成…" };
                measured.Click += (_, _) => CreateCalibration(slot, group, measured: true);
                items.Add(measured);
            }
            if (CalibrationResource.SupportsTextEditing(slot.ServiceType))
            {
                var manual = new MenuItem { Header = "手工填写参数…" };
                manual.Click += (_, _) => CreateCalibration(slot, group, measured: false);
                items.Add(manual);
            }
            if (items.Count == 0)
            {
                items.Add(new MenuItem { Header = "需先取得实测响应表；可导入后编辑", IsEnabled = false });
                items.Add(new MenuItem { Header = "导入已有文件…", Command = group.UploadCalibrationItemCommand, CommandParameter = slot.Key });
            }
        }

        private void CreateCalibration(CalibrationSlotDefinition slot, GroupResource group, bool measured)
        {
            try
            {
                string? createdPath;
                ServiceTypes createdType = slot.ServiceType;
                if (measured)
                {
                    var wizard = new CalibrationCreationWindow(slot.ServiceType.ToCalibrationType()) { Owner = this };
                    if (wizard.ShowDialog() != true || wizard.CreatedFilePath == null) return;
                    createdPath = wizard.CreatedFilePath;
                    slot = CalibrationSlotDefinitions.AllSlots.First(item => item.ServiceType.ToCalibrationType() == wizard.CreatedType);
                    createdType = slot.ServiceType;
                }
                else
                {
                    var editor = new CalibrationJsonEditorWindow(createdType) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                    editor.ShowDialog();
                    createdPath = editor.SavedFilePath;
                }
                if (createdPath == null) return;
                if (group.ImportCalibrationFile(slot.Key, createdPath) == null) return;
                ShowGroup(group);
                MessageBox.Show(this, $"已创建并选用于校正组「{group.Name}」的{CalibrationSlotPresentation.GetTitle(slot.Key)}。", Properties.Resources.CalibrationFileManagement, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "创建或登记校正失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CorrectFourColor_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not CalibrationEditRow row)
                return;

            if (row.SelectedResource?.TryGetFilePath(out string filePath) == true)
                LumFourColorCalibrationWorkflowWindow.ShowWindow(filePath);
            else
                LumFourColorCalibrationWorkflowWindow.ShowWindow();
        }

        private void LaunchFourColorCorrection_Click(object sender, RoutedEventArgs e)
        {
            LumFourColorCalibrationWorkflowWindow.ShowWindow();
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            string calue = NewCreateFileName("title");
            var group = GroupResource.AddGroupResource(PhyCamera, calue);
            if (group == null)
            {
                MessageBox.Show(Properties.Resources.CreateGroupFailed);
            }

        }

        public string NewCreateFileName(string FileName)
        {
            var list = groupResources.Select(g => g.Name).Distinct().ToList();
            for (int i = 1; i < 9999; i++)
            {
                if (!list.Contains($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedItems.Count > 0)
            {
                // Create a SysDictionaryModDetaiModels to hold the items to be removed
                List<GroupResource> itemsToRemove = new List<GroupResource>();
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                foreach (var selectedItem in ListView1.SelectedItems)
                {
                    GroupResource groupResource = selectedItem as GroupResource;
                    if (groupResource != null)
                    {
                        Db.Deleteable<SysResourceModel>().Where(it => it.Id == groupResource.SysResourceModel.Id).ExecuteCommand();
                        itemsToRemove.Add(groupResource);
                    }
                }

                // Remove the items from the original SysDictionaryModDetaiModels and the visual children
                foreach (var item in itemsToRemove)
                {
                    groupResources.Remove(item);
                    PhyCamera.VisualChildren.Remove(item);
                }

                MessageBox.Show(Properties.Resources.DeleteSucceeded);
            }
            else
            {
                MessageBox.Show(Properties.Resources.SelectItemsToDelete);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            PhyCamera.VisualChildren.CollectionChanged -= CameraChildrenChanged;
            foreach (var item in groupResources)
            {
                item.Save();
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (isExporting)
                e.Cancel = true;
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is GroupResource groupResource)
            {
                groupResource.IsEditMode = false;
            }
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            if (isExporting)
                return;

            string zipFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "output.zip");
            using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
            {
                saveFileDialog.Filter = "ZIP files (*.zip)|*.zip";
                saveFileDialog.DefaultExt = "zip";
                saveFileDialog.AddExtension = true;
                saveFileDialog.FileName =  $"{PhyCamera.Code}.zip";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                 zipFilePath = saveFileDialog.FileName;
            }

            SetExporting(true, "正在准备导出…");
            await Dispatcher.Yield(DispatcherPriority.Background);

            try
            {
                CalibrationExportPlan plan = CalibrationArchivePlanBuilder.Create(PhyCamera);
                ExportStatusText.Text = LocalizedText.Format($"正在导出 0%（{plan.EntryCount} 个文件）…");
                Progress<CalibrationExportProgress> progress = new(value =>
                {
                    ExportProgressBar.Value = value.Percent;
                    string fileName = Path.GetFileName(value.EntryPath);
                    ExportStatusText.Text = string.IsNullOrEmpty(fileName)
                        ? LocalizedText.Format($"正在导出 {value.Percent}%…")
                        : LocalizedText.Format($"正在导出 {value.Percent}%：{fileName}");
                });

                await Task.Run(() => CalibrationExportArchive.CreateOrReplace(zipFilePath, plan, progress));
                MessageBox.Show(this, string.Format(Properties.Resources.ExportCalibrationSuccess, PhyCamera.Code));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(Properties.Resources.ExportFailedMessage, ex.Message),
                    Properties.Resources.Failure, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetExporting(false, string.Empty);
            }
        }

        private void SetExporting(bool value, string status)
        {
            isExporting = value;
            EditorContent.IsEnabled = !value;
            ExportBusyOverlay.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            ExportProgressBar.Value = 0;
            ExportStatusText.Text = status;
            Cursor = value ? Cursors.Wait : null;
        }

    }
}
