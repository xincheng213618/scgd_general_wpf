using ColorVision.Core;
using ColorVision.UI;
using Conoscope.Presentation.Formatters;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope.Core
{
    public partial class ConoscopeConfigWindow : Window
    {
        private readonly ConoscopeConfig config;
        private readonly ConoscopeConfig workingConfig;
        private static readonly DisplayMetadataProvider MetadataProvider = new DisplayMetadataProvider();

        public bool CurrentModelGeometryChanged { get; private set; }
        public bool CurrentModelViewSettingsChanged { get; private set; }

        public ConoscopeConfigWindow(ConoscopeConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            workingConfig = new ConoscopeConfig();
            CopyEditableSettings(config, workingConfig);

            InitializeComponent();
            InitializeLocalizedText();
            InitializeOptions();

            basicSettingsPanel.DataContext = workingConfig;
            PreprocessSettingsHost.Content = new ConoscopePreprocessSettingsControl(workingConfig);
            cbCurrentModel.SelectedItem = workingConfig.CurrentModel;
            RefreshModelEditors();
        }

        public void SelectPreprocessTab()
        {
            tabPreprocess.IsSelected = true;
        }

        private void InitializeLocalizedText()
        {
            tabBasic.Header = UiText("Ui_BasicSettings", "常用设置");
            tabAdvanced.Header = UiText("Ui_AdvancedSettings", "高级设置");
            groupDisplayExport.Header = UiText("Ui_DisplayAndExport", "显示与导出");
            groupModelBasic.Header = UiText("Ui_ModelAndCamera", "型号与观察相机");
            btnRestoreDefaults.Content = UiText("Ui_RestoreDefaults", "恢复默认");
            btnRestoreDefaults.ToolTip = UiText("Ui_RestoreDefaultsHint", "恢复本窗口中的显示、预处理、导出和当前型号参数；取消可放弃。 ");
            btnApply.Content = UiText("Ui_ApplyAndSave", "应用并保存");
            tbPendingHint.Text = UiText("Ui_SettingsPendingHint", "修改仅保留在本窗口中，单击“应用并保存”后才会生效。");
            txtFullScalePixels.ToolTip = UiText("Con_Model_Pixels_Description", "0 表示自动使用图像短边的一半。");

            AutomationProperties.SetName(tabBasic, tabBasic.Header?.ToString() ?? string.Empty);
            AutomationProperties.SetName(tabAdvanced, tabAdvanced.Header?.ToString() ?? string.Empty);
            AutomationProperties.SetName(btnRestoreDefaults, btnRestoreDefaults.Content?.ToString() ?? string.Empty);
            AutomationProperties.SetName(btnApply, btnApply.Content?.ToString() ?? string.Empty);
        }

        private void InitializeOptions()
        {
            cbCurrentModel.ItemsSource = Enum.GetValues<ConoscopeModelType>();
            cbDisplayChannel.ItemsSource = Enum.GetValues<ExportChannel>()
                .Select(value => new NamedOption<ExportChannel>(GetChannelName(value), value))
                .ToArray();
            cbPseudoColorMap.ItemsSource = Enum.GetValues<ColormapTypes>()
                .Select(value => new NamedOption<ColormapTypes>(ColormapNameFormatter.Format(value), value))
                .ToArray();
        }

        private void cbCurrentModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbCurrentModel.SelectedItem is not ConoscopeModelType modelType || workingConfig == null)
            {
                return;
            }

            workingConfig.CurrentModel = modelType;
            RefreshModelEditors();
        }

        private void RefreshModelEditors()
        {
            ConoscopeModelProfile profile = workingConfig.CurrentModelProfile;
            modelBasicPanel.DataContext = profile;
            AdvancedProfileHost.Content = PropertyEditorHelper.GenPropertyEditorControl(
                profile,
                Properties.Resources.ResourceManager,
                showCategoryHeader: true,
                metadataProvider: MetadataProvider);
            CoordinateAxisHost.Content = PropertyEditorHelper.GenPropertyEditorControl(
                profile.CoordinateAxisParam,
                Properties.Resources.ResourceManager,
                showCategoryHeader: true,
                metadataProvider: MetadataProvider);
        }

        private void btnRestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            ConoscopeModelType currentModel = workingConfig.CurrentModel;
            ConoscopeConfig defaults = new ConoscopeConfig();
            CopyGeneralSettings(defaults, workingConfig);
            CopyProfile(ConoscopeModelProfile.CreateDefault(currentModel), workingConfig.CurrentModelProfile);
            workingConfig.CurrentModel = currentModel;

            basicSettingsPanel.DataContext = null;
            basicSettingsPanel.DataContext = workingConfig;
            RefreshModelEditors();
            tbSettingsStatus.Text = UiText("Ui_DefaultsLoaded", "已载入默认值；应用并保存后才会生效。");
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            UpdateBindingSources(this);
            if (HasValidationError(this))
            {
                MessageBox.Show(
                    UiText("Ui_InvalidSettings", "部分输入不是有效数值，请检查红色标记的输入框。"),
                    Properties.Resources.TitleHint,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ConoscopeConfig backup = new ConoscopeConfig();
            CopyEditableSettings(config, backup);
            try
            {
                UpdateCurrentModelChangeFlags();
                CopyEditableSettings(workingConfig, config);
                ConfigService.Instance.Save<ConoscopeConfig>();
                DialogResult = true;
            }
            catch (Exception ex)
            {
                CopyEditableSettings(backup, config);
                MessageBox.Show(
                    CompositeFormatCache.Format(Properties.Resources.MsgSaveConfigFailedDetail, ex.Message),
                    Properties.Resources.TitleError,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateCurrentModelChangeFlags()
        {
            ConoscopeModelProfile source = workingConfig.CurrentModelProfile;
            ConoscopeModelProfile? target = config.ModelProfiles.FirstOrDefault(item => item.ModelType == source.ModelType);
            if (target == null)
            {
                CurrentModelGeometryChanged = true;
                CurrentModelViewSettingsChanged = true;
                return;
            }

            CurrentModelGeometryChanged = source.MaxAngle != target.MaxAngle
                || source.CalculationDiameterPixels != target.CalculationDiameterPixels
                || source.ManualConoscopeCoefficient != target.ManualConoscopeCoefficient;
            CurrentModelViewSettingsChanged = CurrentModelGeometryChanged
                || !HaveEquivalentCoordinateAxis(source.CoordinateAxisParam, target.CoordinateAxisParam);
        }

        private static bool HaveEquivalentCoordinateAxis(ConoscopeCoordinateAxisParam left, ConoscopeCoordinateAxisParam right)
        {
            return left.IsInteractionEnabled == right.IsInteractionEnabled
                && left.MaxAngle == right.MaxAngle
                && left.ConoscopeCoefficient == right.ConoscopeCoefficient
                && left.CenterX == right.CenterX
                && left.CenterY == right.CenterY
                && left.AxisRadius == right.AxisRadius
                && left.AzimuthStep == right.AzimuthStep
                && left.PolarStep == right.PolarStep
                && left.LineWidth == right.LineWidth
                && HaveEquivalentBrush(left.AxisBrush, right.AxisBrush)
                && left.ReferenceMode == right.ReferenceMode
                && left.ReferenceAngle == right.ReferenceAngle
                && left.ReferenceRadiusAngle == right.ReferenceRadiusAngle
                && left.ReferenceLineWidth == right.ReferenceLineWidth
                && HaveEquivalentBrush(left.ReferenceBrush, right.ReferenceBrush)
                && left.IsMaskVisible == right.IsMaskVisible
                && left.MaskOpacity == right.MaskOpacity
                && left.MaskColor == right.MaskColor
                && left.IsTextVisible == right.IsTextVisible
                && left.FontSize == right.FontSize
                && HaveEquivalentBrush(left.TextBrush, right.TextBrush);
        }

        private static bool HaveEquivalentBrush(Brush left, Brush right)
        {
            return string.Equals(
                left.ToString(CultureInfo.InvariantCulture),
                right.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static void CopyEditableSettings(ConoscopeConfig source, ConoscopeConfig target)
        {
            CopyGeneralSettings(source, target);
            foreach (ConoscopeModelProfile sourceProfile in source.ModelProfiles)
            {
                ConoscopeModelProfile? targetProfile = target.ModelProfiles.FirstOrDefault(item => item.ModelType == sourceProfile.ModelType);
                if (targetProfile == null)
                {
                    targetProfile = ConoscopeModelProfile.CreateDefault(sourceProfile.ModelType);
                    target.ModelProfiles.Add(targetProfile);
                }

                CopyProfile(sourceProfile, targetProfile);
            }

            target.CurrentModel = source.CurrentModel;
        }

        private static void CopyGeneralSettings(ConoscopeConfig source, ConoscopeConfig target)
        {
            target.DisplayChannel = source.DisplayChannel;
            target.PseudoColorMap = source.PseudoColorMap;
            target.UsePseudoColor = source.UsePseudoColor;
            target.UsePseudoColorRangeLimit = source.UsePseudoColorRangeLimit;

            target.ApplyFilterOnOpen = source.ApplyFilterOnOpen;
            target.ClampNonPositiveXyzOnLoad = source.ClampNonPositiveXyzOnLoad;
            target.FilterType = source.FilterType;
            target.FilterKernelSize = source.FilterKernelSize;
            target.FilterSigma = source.FilterSigma;
            target.FilterD = source.FilterD;
            target.FilterSigmaColor = source.FilterSigmaColor;
            target.FilterSigmaSpace = source.FilterSigmaSpace;
            target.DustRemovalEnabled = source.DustRemovalEnabled;
            target.DustRemovalMode = source.DustRemovalMode;
            target.DustThresholdPercent = source.DustThresholdPercent;
            target.DustMinArea = source.DustMinArea;
            target.DustMaxArea = source.DustMaxArea;
            target.DustRepairRadius = source.DustRepairRadius;

            target.CurrentCurveExportStepDegrees = source.CurrentCurveExportStepDegrees;
            target.CurrentCurveExportIncludeMetadata = source.CurrentCurveExportIncludeMetadata;
            target.ExportDecimalPlaces = source.ExportDecimalPlaces;
        }

        private static void CopyProfile(ConoscopeModelProfile source, ConoscopeModelProfile target)
        {
            target.ModelType = source.ModelType;
            target.DisplayName = source.DisplayName;
            target.MaxAngle = source.MaxAngle;
            target.CalculationDiameterPixels = source.CalculationDiameterPixels;
            target.ManualConoscopeCoefficient = source.ManualConoscopeCoefficient;
            target.HasObservationCamera = source.HasObservationCamera;
            target.ObservationCameraScaleCoefficient = source.ObservationCameraScaleCoefficient;
            target.ObservationCameraCenterX = source.ObservationCameraCenterX;
            target.ObservationCameraCenterY = source.ObservationCameraCenterY;
            CopyCoordinateAxis(source.CoordinateAxisParam, target.CoordinateAxisParam);
        }

        private static void CopyCoordinateAxis(ConoscopeCoordinateAxisParam source, ConoscopeCoordinateAxisParam target)
        {
            target.IsInteractionEnabled = source.IsInteractionEnabled;
            target.MaxAngle = source.MaxAngle;
            target.ConoscopeCoefficient = source.ConoscopeCoefficient;
            target.CenterX = source.CenterX;
            target.CenterY = source.CenterY;
            target.AxisRadius = source.AxisRadius;
            target.AzimuthStep = source.AzimuthStep;
            target.PolarStep = source.PolarStep;
            target.LineWidth = source.LineWidth;
            target.AxisBrush = source.AxisBrush.CloneCurrentValue();
            target.ReferenceMode = source.ReferenceMode;
            target.ReferenceAngle = source.ReferenceAngle;
            target.ReferenceRadiusAngle = source.ReferenceRadiusAngle;
            target.ReferenceLineWidth = source.ReferenceLineWidth;
            target.ReferenceBrush = source.ReferenceBrush.CloneCurrentValue();
            target.IsMaskVisible = source.IsMaskVisible;
            target.MaskOpacity = source.MaskOpacity;
            target.MaskColor = source.MaskColor;
            target.IsTextVisible = source.IsTextVisible;
            target.FontSize = source.FontSize;
            target.TextBrush = source.TextBrush.CloneCurrentValue();
        }

        private static void UpdateBindingSources(DependencyObject root)
        {
            if (root is TextBox textBox)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < childCount; index++)
            {
                UpdateBindingSources(VisualTreeHelper.GetChild(root, index));
            }
        }

        private static bool HasValidationError(DependencyObject root)
        {
            if (Validation.GetHasError(root))
            {
                return true;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < childCount; index++)
            {
                if (HasValidationError(VisualTreeHelper.GetChild(root, index)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetChannelName(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => Properties.Resources.ChannelX,
                ExportChannel.Y => Properties.Resources.ChannelY,
                ExportChannel.Z => Properties.Resources.ChannelZ,
                ExportChannel.CieX => Properties.Resources.ChannelCieX,
                ExportChannel.CieY => Properties.Resources.ChannelCieY,
                ExportChannel.CieU => Properties.Resources.ChannelCieU,
                ExportChannel.CieV => Properties.Resources.ChannelCieV,
                ExportChannel.ColorDifference => Properties.Resources.ChannelDeltaUV,
                ExportChannel.Contrast => Properties.Resources.ChannelContrast,
                _ => ConoscopeColorimetry.GetChannelLabel(channel)
            };
        }

        private static string UiText(string key, string fallback)
        {
            return Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        }

        private sealed record NamedOption<T>(string Name, T Value);

        private sealed class DisplayMetadataProvider : IPropertyEditorMetadataProvider
        {
            public bool IsPropertyManaged(PropertyInfo propertyInfo) =>
                propertyInfo.GetCustomAttribute<DisplayAttribute>() != null
                || propertyInfo.GetCustomAttribute<DisplayNameAttribute>() != null;

            public bool IsBrowsable(PropertyInfo propertyInfo) => propertyInfo.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true;

            public Type? GetEditorType(PropertyInfo propertyInfo) => null;

            public string? GetDisplayName(PropertyInfo propertyInfo) =>
                propertyInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                ?? propertyInfo.GetCustomAttribute<DisplayAttribute>()?.Name;

            public string? GetDescription(PropertyInfo propertyInfo) =>
                propertyInfo.GetCustomAttribute<DescriptionAttribute>()?.Description
                ?? propertyInfo.GetCustomAttribute<DisplayAttribute>()?.Description;

            public string? GetCategory(PropertyInfo propertyInfo) =>
                propertyInfo.GetCustomAttribute<CategoryAttribute>()?.Category
                ?? propertyInfo.GetCustomAttribute<DisplayAttribute>()?.GroupName;
        }
    }
}
