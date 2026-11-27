using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class LocalPoiTemplateStorageTests
{
    private static string NewPath() => Path.Combine(Path.GetTempPath(), "ColorVision-local-poi-tests", Guid.NewGuid().ToString("N"), "ColorVision.Local.db");
    private static PoiTemplateStorage Offline(string path) => new(new LocalTemplateStore(path), () => false, () => throw new Exception("Offline mode must never open MySQL."));
    private static PoiParam Sample() => new()
    {
        Id = -1, Name = "中心/边缘模板", Width = 640, Height = 480,
        PoiConfig = new PoiConfig { DefaultCircleRadius = 12, BackgroundFilePath = "" },
        PoiPoints = [new() { Name = "左上", PointType = PoiShape.Rect, PixX = 100.25, PixY = 80.75, PixWidth = 42.5, PixHeight = 31.25 },
                     new() { Name = "中心", PointType = PoiShape.Circle, PixX = 320, PixY = 240, PixWidth = 20, PixHeight = 20 }]
    };

    [Fact]
    public void DefaultDatabaseIsSharedLocalDatabaseInRoamingConfig()
    {
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Config", "ColorVision.Local.db"), LocalTemplateStore.DefaultDatabasePath);
    }

    [Fact]
    public void ReopenPreservesCompleteTemplateAndFractionalCoordinates()
    {
        string path = NewPath();
        var storage = Offline(path);
        var value = Sample();
        storage.Save(value);
        Assert.True(value.Id < -1);
        var reopened = Assert.Single(Offline(path).Load());
        Assert.Equal(value.Id, reopened.Id);
        Assert.Equal(value.Name, reopened.Name);
        Assert.Equal(640, reopened.Width);
        Assert.Equal(480, reopened.Height);
        Assert.Equal(12, reopened.PoiConfig.DefaultCircleRadius);
        Assert.Equal(100.25, reopened.PoiPoints[0].PixX);
        Assert.Equal(31.25, reopened.PoiPoints[0].PixHeight);
        Assert.Equal(PoiShape.Circle, reopened.PoiPoints[1].PointType);
        Assert.Equal(2, reopened.PoiPoints.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void RenameDoesNotErasePersistedPointsAndDeleteCannotBeUndoneByStaleSave()
    {
        var storage = Offline(NewPath());
        var value = Sample();
        storage.Save(value);
        var masterOnly = Assert.Single(storage.Load());
        masterOnly.PoiPoints.Clear();
        masterOnly.Name = "重命名";
        storage.SaveMetadata(masterOnly);
        var reopened = Assert.Single(storage.Load());
        Assert.Equal("重命名", reopened.Name);
        Assert.Equal(2, reopened.PoiPoints.Count);
        storage.Delete(value.Id);
        Assert.Empty(storage.Load());
        Assert.Throws<InvalidDataException>(() => storage.Save(value));
        Assert.Empty(storage.Load());
    }

    [Fact]
    public void LocalCatalogueAndNewPoiWritesStayLocalAfterReconnect()
    {
        string path = NewPath();
        var value = Sample();
        Offline(path).Save(value);
        int attempts = 0;
        bool connected = false;
        var storage = new PoiTemplateStorage(new LocalTemplateStore(path), () => connected, () => { attempts++; throw new IOException("unexpected database read"); });
        connected = true;
        var reopened = Assert.Single(storage.Load());
        Assert.True(storage.IsLocal);
        reopened.PoiPoints[0].PixX = 108.5;
        storage.Save(reopened);
        storage.Save(Sample());
        Assert.Equal(108.5, storage.ReadPoints(value.Id)[0].PixX);
        Assert.Equal(2, storage.Load().Count);
        Assert.Equal(0, attempts);
    }

    [Fact]
    public void UnavailableServerDoesNotReadLocalCatalogueOrCopyFailedWrites()
    {
        string path = NewPath();
        var local = Sample();
        Offline(path).Save(local);
        var server = Sample();
        server.Id = 1;
        server.Name = "服务器模板";
        server.DetailsLoaded = true;
        bool connected = true;
        var storage = new PoiTemplateStorage(new LocalTemplateStore(path), () => connected, () => throw new IOException("Disconnected"));
        Assert.Throws<IOException>(() => storage.Load());
        Assert.Throws<IOException>(() => storage.Save(server));
        connected = false;
        Assert.Throws<InvalidOperationException>(() => storage.Save(server));
        Assert.Throws<InvalidOperationException>(() => storage.Save(Sample()));
        Assert.False(storage.IsLocal);
        Assert.Equal(1, server.Id);
        Assert.Equal(local.Name, Assert.Single(Offline(path).Load()).Name);
    }

    [Fact]
    public void SnapshotReadsLatestMasterAndPointsFromTheSelectedOwner()
    {
        string path = NewPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bool connected = true;
        SqlSugar.SqlSugarClient OpenServer() => new(new SqlSugar.ConnectionConfig
        {
            ConnectionString = $"Data Source={path};Pooling=False", DbType = SqlSugar.DbType.Sqlite, IsAutoCloseConnection = true,
            InitKeyType = SqlSugar.InitKeyType.Attribute,
            ConfigureExternalServices = new SqlSugar.ConfigureExternalServices
            {
                EntityService = (_, column) => { if (!column.IsPrimarykey) column.IsNullable = true; }
            }
        });
        using (var db = OpenServer())
        {
            db.CodeFirst.InitTables<PoiMasterModel, PoiDetailModel>();
        }
        var owner = new PoiTemplateStorage(new LocalTemplateStore(NewPath()), () => connected, OpenServer);
        var value = Sample();
        value.LeftTopX = 0; value.LeftTopY = 0;
        value.RightTopX = 639; value.RightTopY = 0;
        value.LeftBottomX = 0; value.LeftBottomY = 479;
        value.RightBottomX = 639; value.RightBottomY = 479;
        owner.Save(value);
        var selected = Assert.Single(owner.Load());
        var alternate = Offline(NewPath());
        var changed = owner.ReadSnapshot(selected);
        changed.Name = "新的名称";
        changed.PoiPoints[0].PixX = 33;
        owner.Save(changed);
        var latest = alternate.ReadSnapshot(selected);
        Assert.Equal(changed.Name, latest.Name);
        Assert.Equal(new[] { 33.0, 320.0 }, latest.PoiPoints.Select(point => point.PixX));
        connected = false;
        Assert.Throws<InvalidOperationException>(() => alternate.ReadSnapshot(selected));
        connected = true;
        owner.Delete(selected.Id);
        Assert.Throws<InvalidDataException>(() => alternate.ReadSnapshot(selected));
    }

    [Fact]
    public void DisconnectedServerMasterCannotBecomeAnEmptyLocalTemplate()
    {
        var storage = Offline(NewPath());
        Assert.Throws<InvalidOperationException>(() => storage.Save(new PoiParam { Id = 1, Name = "仅主表" }));
        Assert.Throws<InvalidOperationException>(() => storage.ReadPoints(1));
        Assert.Empty(storage.Load());
    }

    [Fact]
    public void LocalOrderingKeepsIdentitiesAndNamespacesSeparate()
    {
        string path = NewPath();
        var documents = new LocalTemplateStore(path);
        documents.Save("camera-profile", null, "相机配置", 1, "{}");
        var storage = Offline(path);
        var first = Sample();
        var second = Sample(); second.Name = "第二个";
        storage.Save(first); storage.Save(second);
        storage.SwapLocalOrder(first.Id, second.Id);
        var reopened = storage.Load();
        Assert.Equal(new[] { second.Id, first.Id }, reopened.Select(x => x.Id));
        storage.Delete(first.Id);
        Assert.Single(documents.List("camera-profile"));
        Assert.Single(storage.Load());
    }

    [Fact]
    public void CorruptOrFutureDocumentsFailExplicitly()
    {
        string path = NewPath();
        new LocalTemplateStore(path).Save("poi", null, "未来模板", 999, "{}");
        Assert.Throws<InvalidDataException>(() => Offline(path).Load());
        string corrupt = NewPath();
        new LocalTemplateStore(corrupt).Save("poi", null, "损坏模板", 1, "not json");
        Assert.Throws<JsonReaderException>(() => Offline(corrupt).Load());
    }

    [Fact]
    public void TemplateManagerCreatesCopiesImportsAndExportsWithoutMySql()
    {
        WpfTestHost.Invoke(() =>
        {
            var original = TemplatePoi.Params.ToArray();
            try
            {
                string path = NewPath();
                var template = new TemplatePoi(Offline(path));
                template.Load();
                template.ImportTemp = Sample();
                template.Create("原始");
                Assert.True(template.CopyTo(0));
                template.Create("复制");
                var exported = Assert.IsType<PoiParam>(template.CaptureFlowPackageValue(1));
                Assert.Equal(-1, exported.Id);
                Assert.Equal(2, exported.PoiPoints.Count);
                string file = Path.ChangeExtension(path, ".cfg");
                File.WriteAllText(file, JsonConvert.SerializeObject(exported));
                Assert.True(template.ImportFile(file));
                template.Create("导入");
                template.Load();
                Assert.Equal(3, template.Count);
                Assert.Equal(3, TemplatePoi.Params.Select(x => x.Id).Distinct().Count());
                Assert.All(TemplatePoi.Params, x => Assert.Equal(2, x.Value.PoiPoints.Count));
            }
            finally { TemplatePoi.Params.Clear(); foreach (var item in original) TemplatePoi.Params.Add(item); }
        });
    }

    [Fact]
    public async Task ImageViewCanSelectLocalTemplateAndEditorCanSaveAndReopenGeometry()
    {
        string path = NewPath();
        var storage = Offline(path);
        var value = Sample(); storage.Save(value);
        ImageView? view = null;
        EditPoiParam? editor = null;
        TemplateModel<PoiParam>[] previous = [];
        var previousConfig = ConfigService.Instance;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                previous = TemplatePoi.Params.ToArray();
                view = new ImageView();
                PoiImageViewComponent.SetIsTemplateSelectorEnabled(view, false);
            });
            await WpfTestHost.Invoke(() => view!.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task);
            WpfTestHost.Invoke(() =>
            {
                PoiImageViewComponent.SetIsTemplateSelectorEnabled(view!, true);
                new PoiImageViewComponent(storage).Execute(view!);
            });
            await WpfTestHost.Invoke(() => view!.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task);
            WpfTestHost.Invoke(() =>
            {
                var selector = view!.ToolBarAl.Items.OfType<ComboBox>().Single(x => x.Name == "PoiTemplateSelector");
                Assert.Contains("本地模板", selector.ToolTip.ToString());
                Assert.Same(selector, Assert.Single(view.ToolBarAl.Items));
                var menu = selector.ContextMenu;
                var entries = menu.Items.OfType<MenuItem>().ToDictionary(item => item.Name);
                menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                Assert.Equal(Visibility.Collapsed, entries["PoiTemplateSave"].Visibility);
                Assert.Equal(Visibility.Collapsed, entries["PoiTemplateEdit"].Visibility);
                Assert.Equal(Visibility.Visible, entries["PoiTemplateManager"].Visibility);
                Assert.Equal(Visibility.Visible, entries["PoiTemplateRefresh"].Visibility);
                view.SetImageSource(new WriteableBitmap(640, 480, 96, 96, PixelFormats.Gray8, null),
                    enableEditorImageServices: false, configureDefaultLayerController: false);
                menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                Assert.Equal(Visibility.Collapsed, entries["PoiTemplateSave"].Visibility);
                selector.SelectedValue = Assert.Single(TemplatePoi.Params).Value;
                Assert.True(PoiImageViewComponent.TryGetSelectedTemplate(view, out var selected));
                Assert.Equal(value.Id, selected.Id);
                Assert.Equal(2, view.EditorContext.DrawingVisualLists.Count);
                menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                Assert.Equal(Visibility.Visible, entries["PoiTemplateSave"].Visibility);
                Assert.Equal(Visibility.Visible, entries["PoiTemplateEdit"].Visibility);
                editor = new EditPoiParam(selected);
            });
            await WpfTestHost.Invoke(() => editor!.PoiLoadTask);
            await WpfTestHost.Invoke(() =>
            {
                var rect = editor!.DrawingVisualLists.OfType<DVRectangleText>().Single();
                rect.Attribute.Rect = new Rect(60.25, 50.75, 44.5, 32.25);
                rect.Attribute.Text = "已编辑";
                return editor.SaveTemplateAsync();
            });
            var reopened = Assert.Single(Offline(path).Load());
            Assert.Equal("已编辑", reopened.PoiPoints[0].Name);
            Assert.Equal(82.5, reopened.PoiPoints[0].PixX);
            Assert.Equal(66.875, reopened.PoiPoints[0].PixY);
            Assert.Equal(44.5, reopened.PoiPoints[0].PixWidth);
            Assert.Equal(2, reopened.PoiPoints.Count);
            await CloseEditorAsync(editor!);
            WpfTestHost.Invoke(() => editor = new EditPoiParam(reopened));
            await WpfTestHost.Invoke(() => editor!.PoiLoadTask);
            await WpfTestHost.Invoke(() =>
            {
                Assert.Equal("已编辑", editor!.DrawingVisualLists.OfType<DVRectangleText>().Single().Attribute.Text);
                editor.PoiConfig.IsAreaMask = true;
                editor.PoiConfig.IsPointRect = true;
                editor.PoiConfig.Polygon1 = new Point(100, 100);
                editor.PoiConfig.Polygon2 = new Point(500, 100);
                editor.PoiConfig.Polygon3 = new Point(500, 300);
                editor.PoiConfig.Polygon4 = new Point(100, 300);
                editor.PoiConfig.AreaPolygonRow = 1;
                editor.PoiConfig.AreaPolygonCol = 1;

                editor.PointSizePercentageButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("40 × 20 px", editor.PointSizePercentagePreview.Text);
                editor.PoiConfig.IsPointCircle = true;
                editor.PointSizePercentageButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("40 px", editor.PointSizePercentagePreview.Text);
                editor.PointSizeRadiusByHeight.IsSelected = true;
                Assert.Equal("20 px", editor.PointSizePercentagePreview.Text);
                editor.ApplyPointSizePercentageButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(20, editor.PoiConfig.DefaultCircleRadius);

                editor.PoiConfig.IsPointRect = true;
                editor.PointSizePercentageButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                editor.PointSizePercentageCanvas.IsSelected = true;
                Assert.Equal("64 × 48 px", editor.PointSizePercentagePreview.Text);
                editor.ApplyPointSizePercentageButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal(2, editor.DrawingVisualLists.Count);
                Assert.Equal(64, editor.PoiConfig.DefaultRectWidth);
                Assert.Equal(48, editor.PoiConfig.DefaultRectHeight);
                editor.button2.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(3, editor.DrawingVisualLists.Count);
                return editor.SaveTemplateAsync();
            });
            var withPercentagePoint = Assert.Single(Offline(path).Load());
            Assert.Equal(64, withPercentagePoint.PoiPoints[2].PixWidth);
            Assert.Equal(48, withPercentagePoint.PoiPoints[2].PixHeight);
        }
        finally
        {
            if (editor != null) await CloseEditorAsync(editor);
            WpfTestHost.Invoke(() =>
            {
                view?.Dispose();
                TemplatePoi.Params.Clear(); foreach (var item in previous) TemplatePoi.Params.Add(item);
                TestEnvironment.SetConfig(previousConfig!);
            });
        }
    }

    private static async Task CloseEditorAsync(EditPoiParam editor)
    {
        // Wait for the owner's cleanup before restoring the shared configuration.
        Task closed = WpfTestHost.Invoke(() =>
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            editor.Closed += (_, _) => completion.TrySetResult();
            editor.Close();
            return completion.Task;
        });
        await closed.WaitAsync(TimeSpan.FromSeconds(10));
        WpfTestHost.Invoke(() => Assert.Throws<ObjectDisposedException>(() =>
            Assert.IsType<ImageView>(editor.FindName("ImageView")).RegisterSettingsProvider(() => [])));
    }
}
