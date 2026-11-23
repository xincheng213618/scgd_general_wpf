using LocalizedText = global::ColorVision.Engine.EngineLocalization;
#pragma warning disable CS8601
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Media
{
    public class PoiImageViewComponent : IImageComponent
    {
        private readonly PoiTemplateStorage storage;
        public PoiImageViewComponent() : this(PoiTemplateStorage.Default) { }
        public PoiImageViewComponent(PoiTemplateStorage storage) => this.storage = storage;

        public const string SelectedTemplateRuntimeKey = "POI.SelectedTemplate";
        public const string IsTemplateSupportedRuntimeKey = "POI.IsTemplateSupported";
        public static readonly DependencyProperty IsTemplateSelectorEnabledProperty = DependencyProperty.RegisterAttached(
            "IsTemplateSelectorEnabled",
            typeof(bool),
            typeof(PoiImageViewComponent),
            new PropertyMetadata(true));

        public static bool GetIsTemplateSelectorEnabled(DependencyObject element)
        {
            return (bool)element.GetValue(IsTemplateSelectorEnabledProperty);
        }

        public static void SetIsTemplateSelectorEnabled(DependencyObject element, bool value)
        {
            element.SetValue(IsTemplateSelectorEnabledProperty, value);
        }

        public static bool TryGetSelectedTemplate(ImageView imageView, out PoiParam poiParam)
        {
            poiParam = imageView.Config.GetProperties<PoiParam>(SelectedTemplateRuntimeKey);


            return poiParam != null && poiParam.Id != -1;
        }

        public void Execute(ImageView imageView)
        {
            ComboBox? poiTemplateComboBox = null;
            int loadVersion = 0;
            bool populating = false;
            bool saving = false;

            imageView.Dispatcher.BeginInvoke(() =>
            {
                if (!GetIsTemplateSelectorEnabled(imageView))
                {
                    return;
                }

                EnsurePoiTemplateUi();
                LoadTemplates(++loadVersion);
            });

            void EnsurePoiTemplateUi()
            {
                if (poiTemplateComboBox != null)
                {
                    return;
                }

                poiTemplateComboBox = new ComboBox
                {
                    Width = 84,
                    Height = 22,
                    MinHeight = 0,
                    FontSize = 12,
                    Padding = new Thickness(5, 0, 4, 0),
                    Margin = new Thickness(4, 0, 5, 5),
                    Opacity = 0.9,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    DisplayMemberPath = "Key",
                    SelectedValuePath = "Value",
                    Style = Application.Current.TryFindResource("ComboBox.Small") as Style
                };

                poiTemplateComboBox.SelectionChanged += (_, _) =>
                {
                    if (populating) return;
                    try { ApplySelectedTemplate(imageView, poiTemplateComboBox, ClearDrawingVisuals); }
                    catch (Exception ex) { MessageBox.Show(Window.GetWindow(imageView), ex.Message, LocalizedText.Get("POI 模板"), MessageBoxButton.OK, MessageBoxImage.Warning); }
                };
                imageView.ToolBarAl.Items.Add(poiTemplateComboBox);
                poiTemplateComboBox.Name = "PoiTemplateSelector";
                var menu = new ContextMenu();
                var save = new MenuItem { Name = "PoiTemplateSave", Header = LocalizedText.Get("保存到模板") };
                var edit = new MenuItem { Name = "PoiTemplateEdit", Header = LocalizedText.Get("编辑当前 POI 模板…") };
                edit.Click += (_, _) =>
                {
                    if (poiTemplateComboBox.SelectedValue is not PoiParam value || value.Id == -1) return;
                    var editor = new EditPoiParam(value) { Owner = Window.GetWindow(imageView) };
                    editor.Closed += (_, _) => LoadTemplates(++loadVersion);
                    editor.Show();
                };
                var manage = new MenuItem { Name = "PoiTemplateManager", Header = LocalizedText.Get("管理 POI 模板…") };
                manage.Click += (_, _) => OpenManager();
                var refresh = new MenuItem { Name = "PoiTemplateRefresh", Header = LocalizedText.Get("刷新模板") };
                refresh.Click += (_, _) => LoadTemplates(++loadVersion);
                menu.Opened += (_, _) =>
                {
                    save.Visibility = CanSave() ? Visibility.Visible : Visibility.Collapsed;
                    edit.Visibility = poiTemplateComboBox.SelectedValue is PoiParam p && p.Id != -1 ? Visibility.Visible : Visibility.Collapsed;
                };
                menu.Items.Add(save);
                menu.Items.Add(edit);
                menu.Items.Add(manage);
                menu.Items.Add(refresh);
                poiTemplateComboBox.ContextMenu = menu;
                save.Click += async (_, _) =>
                {
                    if (!CanSave()) return;
                    saving = true;
                    poiTemplateComboBox.IsEnabled = false;
                    try
                    {
                        var selected = poiTemplateComboBox.SelectedValue as PoiParam;
                        if (selected?.Id == -1) selected = null;
                        var snapshot = PoiImageTemplateCapture.Capture(imageView, selected);
                        if (selected == null)
                        {
                            var template = new TemplatePoi(storage);
                            template.Load();
                            template.ImportTemp = snapshot;
                            template.ImportName = "POI";
                            var create = new TemplateCreate(template, true) { Owner = Window.GetWindow(imageView), Title = LocalizedText.Get("保存 POI 模板"), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                            if (create.ShowDialog() != true) return;
                            snapshot = (PoiParam)template.GetParamValue(template.GetTemplateIndex(create.CreateName!));
                        }
                        else
                        {
                            var destination = selected.Storage ?? storage;
                            await Task.Run(() => destination.Save(snapshot));
                        }
                        LoadTemplates(++loadVersion, snapshot.Id, false);
                        string status = PoiTemplateStorage.IsLocalId(snapshot.Id) ? LocalizedText.Get("已保存到本地 POI 模板库") : LocalizedText.Get("POI 模板已保存");
                        poiTemplateComboBox.ToolTip = $"{poiTemplateComboBox.ToolTip}{Environment.NewLine}{status}";
                    }
                    catch (Exception ex) { MessageBox.Show(Window.GetWindow(imageView), ex.Message, LocalizedText.Get("保存 POI 模板"), MessageBoxButton.OK, MessageBoxImage.Warning); }
                    finally
                    {
                        saving = false;
                        poiTemplateComboBox.IsEnabled = true;
                    }
                };
                imageView.ToolBarAl.Padding = new Thickness(0);

                bool CanSave() => !saving && imageView.ImageShow.IsEnabled && imageView.ImageShow.Source != null &&
                    imageView.EditorContext.DrawingVisualLists.Count > 0;

                void OpenManager()
                {
                    try
                    {
                        int selectedId = (poiTemplateComboBox.SelectedValue as PoiParam)?.Id ?? -1;
                        var template = new TemplatePoi(storage);
                        var manager = new PoiTemplateManagerWindow(template) { Owner = Window.GetWindow(imageView) };
                        if (selectedId != -1) manager.SelectTemplate(selectedId);
                        manager.Closed += (_, _) => LoadTemplates(++loadVersion);
                        manager.Show();
                    }
                    catch (Exception ex) { MessageBox.Show(Window.GetWindow(imageView), ex.Message, LocalizedText.Get("POI 模板"), MessageBoxButton.OK, MessageBoxImage.Warning); }
                }
            }

            void ClearDrawingVisuals()
            {
                imageView.ImageShow.ClearActionCommand();
                foreach (Visual visual in imageView.EditorContext.DrawingVisualLists.OfType<Visual>().ToList())
                {
                    imageView.ImageShow.RemoveVisual(visual);
                }
            }

            void LoadTemplates(int version, int? selectId = null, bool applySelection = true)
            {
                if (version != loadVersion || poiTemplateComboBox == null) return;
                int selectedId = selectId ?? (poiTemplateComboBox.SelectedValue as PoiParam)?.Id ?? -1;
                try
                {
                    var template = new TemplatePoi(storage);
                    template.Load();
                    populating = true;
                    // A snapshot avoids retaining each closed ImageView through the shared collection's event handlers.
                    var items = new ObservableCollection<TemplateModel<PoiParam>>(TemplatePoi.Params);
                    items.Insert(0, new TemplateModel<PoiParam>("Empty", new PoiParam { Id = -1 }));
                    poiTemplateComboBox.ItemsSource = items;
                    poiTemplateComboBox.SelectedValue = TemplatePoi.Params.FirstOrDefault(x => x.Id == selectedId)?.Value;
                    if (poiTemplateComboBox.SelectedIndex < 0) poiTemplateComboBox.SelectedIndex = 0;
                    poiTemplateComboBox.ToolTip = storage.IsLocal ? LocalizedText.Format($"本地模板 · {storage.Location}") : LocalizedText.Get("MySQL 模板");
                    populating = false;
                    // Do not clear manually drawn ROIs when initially loading an empty selector.
                    if (applySelection && selectedId != -1) ApplySelectedTemplate(imageView, poiTemplateComboBox, ClearDrawingVisuals);
                    else if (poiTemplateComboBox.SelectedValue is PoiParam current)
                        imageView.Config.SetViewState(SelectedTemplateRuntimeKey, current, nameof(PoiImageViewComponent), "当前选择的 POI 模板");
                }
                catch (Exception ex)
                {
                    poiTemplateComboBox.ToolTip = LocalizedText.Format($"POI 模板读取失败：{ex.Message}");
                    System.Diagnostics.Trace.TraceError(poiTemplateComboBox.ToolTip.ToString());
                }
                finally { populating = false; }
            }
        }

        private static void ApplySelectedTemplate(ImageView imageView, ComboBox comboBox, Action clearDrawingVisuals)
        {
            if (comboBox.SelectedValue is not PoiParam poiParams)
            {
                return;
            }

            if (poiParams.Id != -1) PoiParam.LoadPoiDetailFromDB(poiParams);
            clearDrawingVisuals();
            imageView.Config.SetViewState(SelectedTemplateRuntimeKey, poiParams, nameof(PoiImageViewComponent), "当前选择的 POI 模板");
            if (poiParams.Id == -1)
            {
                return;
            }

            foreach (PoiPoint item in poiParams.PoiPoints)
            {
                PoiOverlayRenderer.Add(imageView, item, style: new PoiOverlayStyle
                {
                    StrokeThickness = item.PixWidth / 30
                });
            }
        }
    }
}
