using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.DataLoad;
using ColorVision.Engine.Templates.ImageCropping;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using ColorVision.Engine.Templates.Jsons.CaliAngleShift;
using ColorVision.Engine.Templates.Jsons.CompoundImg;
using ColorVision.Engine.Templates.Jsons.ImageROI;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Templates.Jsons.OLEDAOI;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIGenCali;
using ColorVision.Engine.Templates.POI.POIOutput;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.UI;
using ColorVision.UI.Extension;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.Engine.PropertyEditor
{
    internal static class FlowNodePropertyEditorRegistration
    {
        private static int _registered;

        public static void EnsureRegistered()
        {
            if (Interlocked.Exchange(ref _registered, 1) == 1)
                return;

            FlowPropertyEditorRegistry.Register<FlowDeviceNameEditor>((property, obj) => new DeviceNameEditor().GenProperties(property, obj));
            FlowPropertyEditorRegistry.Register<FlowCalibrationTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, CreateCalibrationTemplate(obj)));
            FlowPropertyEditorRegistry.Register<FlowCameraRunTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateCameraRunParam()));
            FlowPropertyEditorRegistry.Register<FlowAutoFocusTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateAutoFocus()));
            FlowPropertyEditorRegistry.Register<FlowPoiTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplatePoi()));
            FlowPropertyEditorRegistry.Register<FlowPoiFilterTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplatePoiFilterParam()));
            FlowPropertyEditorRegistry.Register<FlowPoiReviseTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplatePoiReviseParam()));
            FlowPropertyEditorRegistry.Register<FlowPoiOutputTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplatePoiOutputParam()));
            FlowPropertyEditorRegistry.Register<FlowPoiGenCaliTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplatePoiGenCalParam()));
            FlowPropertyEditorRegistry.Register<FlowSmuTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateSMUParam()));
            FlowPropertyEditorRegistry.Register<FlowSensorTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, CreateSensorTemplate(obj)));
            FlowPropertyEditorRegistry.Register<FlowDataLoadTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateDataLoad()));
            FlowPropertyEditorRegistry.Register<FlowCaliAngleShiftJsonTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateCaliAngleShift()));
            FlowPropertyEditorRegistry.Register<FlowBlackMuraJsonTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateBlackMura()));
            FlowPropertyEditorRegistry.Register<FlowImageRoiJsonTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateImageROI()));
            FlowPropertyEditorRegistry.Register<FlowPoiAnalysisJsonTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplatePoiAnalysis()));
            FlowPropertyEditorRegistry.Register<FlowImageCroppingTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateImageCropping()));
            FlowPropertyEditorRegistry.Register<FlowCompoundImgJsonTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateCompoundImg()));
            FlowPropertyEditorRegistry.Register<FlowLedCheck2JsonTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateLedCheck2()));
            FlowPropertyEditorRegistry.Register<FlowOledAoiJsonTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateOLEDAOI()));
            FlowPropertyEditorRegistry.Register<FlowKbJsonTemplateEditor>((property, obj) => CreateTemplateEditor(property, obj, new TemplateKB()));
        }

        private static DockPanel CreateTemplateEditor(PropertyInfo property, object obj, ITemplate? template)
        {
            if (template == null)
                return new TextboxPropertiesEditor().GenProperties(property, obj);

            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (HasDirectTemplateEditor(template))
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var combo = new HandyControl.Controls.ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 0,
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                DisplayMemberPath = "Key",
                SelectedValuePath = "Value",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);

            bool isRefreshing = false;
            void RefreshItems()
            {
                isRefreshing = true;
                combo.ItemsSource = null;
                combo.ItemsSource = template.ItemsSource;
                SelectTemplate(combo, template.ItemsSource, property.GetValue(obj)?.ToString());
                isRefreshing = false;
            }

            RefreshItems();

            combo.SelectionChanged += (_, _) =>
            {
                if (isRefreshing)
                    return;

                string selectedName = combo.SelectedItem is TemplateBase templateModel ? templateModel.Key : string.Empty;
                SetValueAndNotify(property, obj, selectedName);
            };

            int buttonColumn = 2;
            if (HasDirectTemplateEditor(template))
            {
                var openButton = CreateOpenTemplateButton();
                openButton.Click += (_, _) =>
                {
                    int selectedIndex = GetSelectedTemplateIndex(template, property.GetValue(obj)?.ToString(), combo.SelectedIndex);
                    if (selectedIndex >= 0)
                    {
                        template.PreviewMouseDoubleClick(selectedIndex);
                        RefreshItems();
                    }
                };
                Grid.SetColumn(openButton, buttonColumn++);
                grid.Children.Add(openButton);
            }

            var editButton = CreateEditButton();
            editButton.Click += (_, _) =>
            {
                int defaultIndex = GetTemplateIndex(template, property.GetValue(obj)?.ToString(), combo.SelectedIndex);
                new TemplateEditorWindow(template, defaultIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                RefreshItems();
            };

            Grid.SetColumn(textBlock, 0);
            Grid.SetColumn(combo, 1);
            Grid.SetColumn(editButton, buttonColumn);
            grid.Children.Add(textBlock);
            grid.Children.Add(combo);
            grid.Children.Add(editButton);
            dockPanel.Children.Add(grid);
            return dockPanel;
        }

        private static Button CreateEditButton()
        {
            var textBlock = new TextBlock
            {
                Text = "\uE713",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            };

            return new Button
            {
                Width = 24,
                Padding = new Thickness(2),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(5, 0, 0, 0),
                Content = textBlock
            };
        }

        private static Button CreateOpenTemplateButton()
        {
            var image = new Image
            {
                Source = (ImageSource)Application.Current.Resources["DrawingImageEdit"],
                Width = 12,
                Margin = new Thickness(0)
            };

            return new Button
            {
                Width = 24,
                Padding = new Thickness(2),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(5, 0, 0, 0),
                Content = image
            };
        }

        private static void SelectTemplate(Selector combo, IEnumerable itemsSource, string? templateName)
        {
            combo.SelectedItem = itemsSource.Cast<object>().FirstOrDefault(item => item is TemplateBase templateModel && string.Equals(templateModel.Key, templateName, StringComparison.Ordinal));
        }

        private static bool HasDirectTemplateEditor(ITemplate template)
        {
            var method = template.GetType().GetMethod(nameof(ITemplate.PreviewMouseDoubleClick), BindingFlags.Instance | BindingFlags.Public);
            return method?.DeclaringType != typeof(ITemplate);
        }

        private static int GetSelectedTemplateIndex(ITemplate template, string? templateName, int selectedIndex)
        {
            if (!string.IsNullOrWhiteSpace(templateName))
            {
                try
                {
                    int index = template.GetTemplateIndex(templateName);
                    if (index >= 0)
                        return index;
                }
                catch
                {
                }
            }
            return selectedIndex >= 0 ? selectedIndex : -1;
        }

        private static int GetTemplateIndex(ITemplate template, string? templateName, int selectedIndex)
        {
            if (!string.IsNullOrWhiteSpace(templateName))
            {
                try
                {
                    int index = template.GetTemplateIndex(templateName);
                    if (index >= 0)
                        return index;
                }
                catch
                {
                }
            }
            return selectedIndex >= 0 ? selectedIndex : 0;
        }

        private static TemplateCalibrationParam? CreateCalibrationTemplate(object obj)
        {
            string deviceCode = GetStringProperty(obj, nameof(CVCommonNode.DeviceCode));
            if (string.IsNullOrWhiteSpace(deviceCode))
                return null;

            var services = ServiceManager.GetInstance().DeviceServices;
            var nodeType = GetStringProperty(obj, nameof(CVCommonNode.NodeType));

            if (string.Equals(nodeType, "Calibration", StringComparison.OrdinalIgnoreCase))
            {
                var calibration = services.OfType<DeviceCalibration>().FirstOrDefault(device => device.Code == deviceCode);
                return calibration?.PhyCamera == null ? null : new TemplateCalibrationParam(calibration.PhyCamera);
            }

            var camera = services.OfType<DeviceCamera>().FirstOrDefault(device => device.Code == deviceCode);
            return camera?.PhyCamera == null ? null : new TemplateCalibrationParam(camera.PhyCamera);
        }

        private static TemplateSensor CreateSensorTemplate(object obj)
        {
            string deviceCode = GetStringProperty(obj, nameof(CVCommonNode.DeviceCode));
            string? category = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSensor>().FirstOrDefault(device => device.Code == deviceCode)?.Config?.Category;
            if (string.IsNullOrWhiteSpace(category))
                category = TemplateSensor.Params.Keys.FirstOrDefault() ?? "Sensor.Default";

            return new TemplateSensor(category);
        }

        private static string GetStringProperty(object obj, string propertyName)
        {
            return obj.GetType().GetProperty(propertyName)?.GetValue(obj)?.ToString() ?? string.Empty;
        }

        private static void SetValueAndNotify(PropertyInfo property, object obj, string value)
        {
            string oldValue = property.GetValue(obj)?.ToString() ?? string.Empty;
            if (oldValue == value)
                return;

            property.SetValue(obj, value);
            if (obj is CVCommonNode node)
                node.nodeEvent?.Invoke(node, new FlowEngineNodeEventArgs());
        }
    }
}
