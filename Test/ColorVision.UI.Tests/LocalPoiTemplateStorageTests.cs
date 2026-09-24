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
    public void UnusableMySqlFallsBackAndLocalRecordsStayLocalAfterReconnect()
    {
        string path = NewPath();
        var value = Sample();
        Offline(path).Save(value);
        int attempts = 0;
        var storage = new PoiTemplateStorage(new LocalTemplateStore(path), () => true, () => { attempts++; throw new IOException("MySQL unavailable"); });
        var reopened = Assert.Single(storage.Load());
        Assert.True(storage.IsLocal);
        Assert.Equal(1, attempts);
        reopened.PoiPoints[0].PixX = 108.5;
        storage.Save(reopened);
        Assert.Equal(1, attempts);
        Assert.Equal(108.5, storage.ReadPoints(value.Id)[0].PixX);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public void FailedServerSavePreservesCompleteLocalCopyWithoutOverwritingSameNumberedLocalRecord()
    {
        string path = NewPath();
        var local = Sample();
        Offline(path).Save(local);
        var server = Sample();
        server.Id = 1;
        server.Name = "服务器副本";
        server.DetailsLoaded = true;
        var storage = new PoiTemplateStorage(new LocalTemplateStore(path), () => true, () => throw new IOException("Disconnected"));
        storage.Save(server);
        Assert.True(server.Id < -1);
        Assert.NotEqual(local.Id, server.Id);
        Assert.Equal(2, Offline(path).Load().Count);
        Assert.Equal(local.Name, Offline(path).Load()[0].Name);
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
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
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
                selector.SelectedValue = Assert.Single(TemplatePoi.Params).Value;
                Assert.True(PoiImageViewComponent.TryGetSelectedTemplate(view, out var selected));
                Assert.Equal(value.Id, selected.Id);
                Assert.Equal(2, view.EditorContext.DrawingVisualLists.Count);
                Assert.Single(view.ToolBarAl.Items.OfType<Button>(), x => x.Name == "PoiTemplateManager");
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
            WpfTestHost.Invoke(() => { editor!.Close(); editor = new EditPoiParam(reopened); });
            await WpfTestHost.Invoke(() => editor!.PoiLoadTask);
            WpfTestHost.Invoke(() => Assert.Equal("已编辑", editor!.DrawingVisualLists.OfType<DVRectangleText>().Single().Attribute.Text));
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                editor?.Close(); view?.Dispose();
                TemplatePoi.Params.Clear(); foreach (var item in previous) TemplatePoi.Params.Add(item);
                ConfigService.SetInstance(previousConfig!);
            });
        }
    }
}
