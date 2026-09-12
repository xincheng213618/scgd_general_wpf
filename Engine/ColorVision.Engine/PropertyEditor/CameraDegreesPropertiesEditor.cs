using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.UI;
using ColorVision.UI.Extension;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.PropertyEditor
{
    public enum CameraFovReferenceDirection
    {
        [Description("水平")]
        Horizontal,

        [Description("垂直")]
        Vertical,

        [Description("对角")]
        Diagonal
    }

    [DisplayName("相机视场角计算")]
    public sealed class CameraDegreesCalculatorOptions : ViewModelBase
    {
        private double? sensorWidthMillimeters;
        private double? sensorHeightMillimeters;
        private double? effectiveFocalLengthMillimeters;
        private CameraFovReferenceDirection referenceDirection;
        private double currentCameraDegrees = 74.2;
        private double fovDist = 9410;

        public CameraDegreesCalculatorOptions()
        {
        }

        public CameraDegreesCalculatorOptions(double currentCameraDegrees, double fovDist)
        {
            this.currentCameraDegrees = currentCameraDegrees;
            this.fovDist = fovDist;
        }

        [Category("输入")]
        [DisplayName("传感器宽度 (mm)")]
        [Description("传感器当前有效成像区域的水平尺寸；使用相机 ROI 或裁切时应填写有效尺寸。")]
        public double? SensorWidthMillimeters
        {
            get => sensorWidthMillimeters;
            set
            {
                if (sensorWidthMillimeters == value) return;
                sensorWidthMillimeters = value;
                OnPropertyChanged();
                RaiseCalculatedProperties();
            }
        }

        [Category("输入")]
        [DisplayName("传感器高度 (mm)")]
        [Description("传感器当前有效成像区域的垂直尺寸；只计算水平 FOV 时可以不填。")]
        public double? SensorHeightMillimeters
        {
            get => sensorHeightMillimeters;
            set
            {
                if (sensorHeightMillimeters == value) return;
                sensorHeightMillimeters = value;
                OnPropertyChanged();
                RaiseCalculatedProperties();
            }
        }

        [Category("输入")]
        [DisplayName("镜头有效焦距 (mm)")]
        [Description("填写当前镜头和对焦状态下的有效焦距，而不是镜头型号名称。")]
        public double? EffectiveFocalLengthMillimeters
        {
            get => effectiveFocalLengthMillimeters;
            set
            {
                if (effectiveFocalLengthMillimeters == value) return;
                effectiveFocalLengthMillimeters = value;
                OnPropertyChanged();
                RaiseCalculatedProperties();
            }
        }

        [Category("应用")]
        [DisplayName("参考方向")]
        [Description("选择写回 cameraDegrees 的方向；必须与 FovDist 所代表的参考像素跨度保持一致。")]
        public CameraFovReferenceDirection ReferenceDirection
        {
            get => referenceDirection;
            set
            {
                if (referenceDirection == value) return;
                referenceDirection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCameraDegrees));
                OnPropertyChanged(nameof(EquivalentFocalLengthPixels));
            }
        }

        [Category("当前参数")]
        [DisplayName("当前 cameraDegrees")]
        [ReadOnly(true)]
        public double CurrentCameraDegrees
        {
            get => currentCameraDegrees;
            set
            {
                if (currentCameraDegrees == value) return;
                currentCameraDegrees = value;
                OnPropertyChanged();
            }
        }

        [Category("当前参数")]
        [DisplayName("FovDist (px)")]
        [Description("仅用于核对当前配套参数；计算窗口不会修改 FovDist。")]
        [ReadOnly(true)]
        public double FovDist
        {
            get => fovDist;
            set
            {
                if (fovDist == value) return;
                fovDist = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EquivalentFocalLengthPixels));
            }
        }

        [Category("计算结果")]
        [DisplayName("水平 FOV (°)")]
        [ReadOnly(true)]
        public double? HorizontalFovDegrees => Calculate(SensorWidthMillimeters);

        [Category("计算结果")]
        [DisplayName("垂直 FOV (°)")]
        [ReadOnly(true)]
        public double? VerticalFovDegrees => Calculate(SensorHeightMillimeters);

        [Category("计算结果")]
        [DisplayName("对角 FOV (°)")]
        [ReadOnly(true)]
        public double? DiagonalFovDegrees => IsPositiveFinite(SensorWidthMillimeters) && IsPositiveFinite(SensorHeightMillimeters)
            ? Calculate(Math.Sqrt(SensorWidthMillimeters!.Value * SensorWidthMillimeters.Value
                + SensorHeightMillimeters!.Value * SensorHeightMillimeters.Value))
            : null;

        [Category("应用")]
        [DisplayName("将写入 cameraDegrees (°)")]
        [ReadOnly(true)]
        public double? SelectedCameraDegrees => ReferenceDirection switch
        {
            CameraFovReferenceDirection.Horizontal => HorizontalFovDegrees,
            CameraFovReferenceDirection.Vertical => VerticalFovDegrees,
            CameraFovReferenceDirection.Diagonal => DiagonalFovDegrees,
            _ => null
        };

        [Category("应用")]
        [DisplayName("对应等效像素焦距 (px)")]
        [Description("由当前 FovDist 与待写入角度换算，仅用于检查两项参数是否配套。")]
        [ReadOnly(true)]
        public double? EquivalentFocalLengthPixels => SelectedCameraDegrees is double degrees
            && double.IsFinite(FovDist) && FovDist > 0
            ? FovCalculator.EquivalentFocalLengthPixels(FovDist, degrees)
            : null;

        public bool TryGetSelectedCameraDegrees(out double cameraDegrees, out string error)
        {
            if (!IsPositiveFinite(EffectiveFocalLengthMillimeters))
            {
                cameraDegrees = 0;
                error = "请输入大于 0 的镜头有效焦距。";
                return false;
            }

            double? selected = SelectedCameraDegrees;
            if (!selected.HasValue)
            {
                cameraDegrees = 0;
                error = ReferenceDirection switch
                {
                    CameraFovReferenceDirection.Horizontal => "请输入大于 0 的传感器宽度。",
                    CameraFovReferenceDirection.Vertical => "请输入大于 0 的传感器高度。",
                    CameraFovReferenceDirection.Diagonal => "计算对角 FOV 需要同时填写大于 0 的传感器宽度和高度。",
                    _ => "请选择有效的参考方向。"
                };
                return false;
            }

            cameraDegrees = selected.Value;
            error = string.Empty;
            return true;
        }

        private double? Calculate(double? sensorLengthMillimeters) =>
            IsPositiveFinite(sensorLengthMillimeters) && IsPositiveFinite(EffectiveFocalLengthMillimeters)
                ? FovCalculator.SensorLengthToDegrees(sensorLengthMillimeters!.Value, EffectiveFocalLengthMillimeters!.Value)
                : null;

        private static bool IsPositiveFinite(double? value) =>
            value.HasValue && double.IsFinite(value.Value) && value.Value > 0;

        private void RaiseCalculatedProperties()
        {
            OnPropertyChanged(nameof(HorizontalFovDegrees));
            OnPropertyChanged(nameof(VerticalFovDegrees));
            OnPropertyChanged(nameof(DiagonalFovDegrees));
            OnPropertyChanged(nameof(SelectedCameraDegrees));
            OnPropertyChanged(nameof(EquivalentFocalLengthPixels));
        }
    }

    public sealed class CameraDegreesPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            if (property.PropertyType != typeof(double))
                return new TextboxPropertiesEditor().GenProperties(property, obj);

            var dockPanel = new DockPanel();
            dockPanel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));

            Binding binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property, UpdateSourceTrigger.PropertyChanged);
            binding.StringFormat = "0.0################";
            TextBox textBox = PropertyEditorHelper.CreateSmallTextBox(binding);
            textBox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;

            var glyph = new TextBlock
            {
                Text = "\uE70F",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            var calculateButton = new Button
            {
                Width = 24,
                Padding = new Thickness(2),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(5, 0, 0, 0),
                Content = glyph,
                ToolTip = "根据传感器尺寸和镜头有效焦距计算 cameraDegrees"
            };
            AutomationProperties.SetName(calculateButton, "计算 cameraDegrees");
            DockPanel.SetDock(calculateButton, Dock.Right);
            calculateButton.Click += (_, _) => ShowCalculator(property, obj, textBox, calculateButton);

            dockPanel.Children.Add(calculateButton);
            dockPanel.Children.Add(textBox);
            return dockPanel;
        }

        private static void ShowCalculator(PropertyInfo property, object obj, TextBox textBox, Button calculateButton)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            double currentCameraDegrees = ReadDouble(property, obj, 74.2);
            double fovDist = ReadDouble(obj.GetType().GetProperty("FovDist", BindingFlags.Public | BindingFlags.Instance), obj, 9410);
            var options = new CameraDegreesCalculatorOptions(currentCameraDegrees, fovDist);
            Window? owner = Window.GetWindow(calculateButton);
            if (owner == null && Application.Current != null)
                owner = Application.Current.GetActiveWindow();
            var window = new PropertyEditorWindow(options, PropertyEditorEditMode.Transactional)
            {
                Title = "相机视场角计算",
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            };
            window.Submitted += (_, _) =>
            {
                if (!options.TryGetSelectedCameraDegrees(out double result, out string error))
                {
                    MessageBox.Show(owner, error, "相机视场角计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                property.SetValue(obj, result);
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            };
            window.ShowDialog();
        }

        private static double ReadDouble(PropertyInfo? property, object obj, double fallback)
        {
            if (property?.GetValue(obj) is double value && double.IsFinite(value)) return value;
            try
            {
                object? raw = property?.GetValue(obj);
                double converted = raw == null ? fallback : Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return double.IsFinite(converted) ? converted : fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
