using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib.End;
using ST.Library.UI.NodeEditor;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class LocalFlowTemplateStorageTests
{
    private static string NewPath() => Path.Combine(Path.GetTempPath(), "ColorVision-local-flow-tests", Guid.NewGuid().ToString("N"), "ColorVision.Local.db");
    private static LocalFlowTemplateStorage Open(string path) => new(new LocalTemplateStore(path));
    private static TemplateFlow Offline(LocalFlowTemplateStorage storage) => new(storage, () => false,
        () => throw new InvalidOperationException("Offline flow editing must never open MySQL."));

    [Fact]
    public void CanvasCanBeOpenedEditedSavedAndReopenedThroughTemplateSave()
    {
        WithTemplates((storage, path) =>
        {
            var templates = Offline(storage);
            templates.Load();
            Assert.IsType<FlowParam>(templates.CreateDefault());
            templates.Create("本地流程");
            FlowParam value = Assert.Single(TemplateFlow.Params).Value;
            Assert.True(value.Id < -1);
            string identity = value.FlowKey!;
            value.DataBase64 = Convert.ToBase64String(CreateCanvas("原始节点"));
            TemplateFlow.Save2DB(value, new FlowTemplateSaveCondition(value.LoadedContentHash));

            FlowParam reopened = Assert.Single(Open(path).Load());
            using var editor = new STNodeEditor();
            editor.LoadCanvas(Convert.FromBase64String(reopened.DataBase64));
            Assert.Equal(2, editor.Nodes.Count);
            Assert.Single(editor.GetConnectionInfo());
            var start = Assert.IsType<HeadlessTestStartNode>(editor.Nodes[0]);
            Assert.Equal("原始节点", start.NodeName);
            Assert.Equal(25, start.Left);
            start.NodeName = "修改后的节点";
            start.Left = 150;
            reopened.DataBase64 = Convert.ToBase64String(editor.GetCanvasData());
            TemplateFlow.Save2DB(reopened, new FlowTemplateSaveCondition(reopened.LoadedContentHash));

            FlowParam saved = Assert.Single(Open(path).Load());
            Assert.Equal(identity, saved.FlowKey);
            Assert.Equal(value.Id, saved.Id);
            using var finalEditor = new STNodeEditor();
            finalEditor.LoadCanvas(Convert.FromBase64String(saved.DataBase64));
            var finalStart = Assert.IsType<HeadlessTestStartNode>(finalEditor.Nodes[0]);
            Assert.Equal("修改后的节点", finalStart.NodeName);
            Assert.Equal(150, finalStart.Left);
            Assert.Single(finalEditor.GetConnectionInfo());
            Assert.Null(saved.ModMaster);
            Assert.Empty(saved.ModDetailModels);
            string payload = Assert.Single(new LocalTemplateStore(path).List("flow")).Payload;
            Assert.DoesNotContain("ModMaster", payload);
            Assert.DoesNotContain("ModDetailModels", payload);
        });
    }

    [Fact]
    public void CopyRenameOrderingAndDeleteKeepLocalIdentitiesAndPoiDataSeparate()
    {
        WithTemplates((storage, path) =>
        {
            var documents = new LocalTemplateStore(path);
            documents.Save("poi", null, "已有 POI", 1, "{}");
            var templates = Offline(storage);
            templates.Load();
            templates.Create("原始");
            FlowParam first = TemplateFlow.Params[0].Value;
            Assert.True(templates.CopyTo(0));
            templates.Create("复制");
            FlowParam second = TemplateFlow.Params[1].Value;
            Assert.NotEqual(first.FlowKey, second.FlowKey);
            Assert.True(templates.SwapTemplateOrder(0, 1));
            templates.Load();
            Assert.Equal(new[] { second.Id, first.Id }, TemplateFlow.Params.Select(item => item.Id));
            TemplateFlow.Params[0].Value.Name = "重命名";
            templates.SetSaveIndex(0);
            templates.Save();
            Assert.Empty(templates.SaveIndex);
            Assert.Equal("重命名", Open(path).Read(second.Id).Name);
            TemplateFlow.Params[1].IsSelected = true;
            templates.Delete(0);
            Assert.Equal(second.Id, Assert.Single(Open(path).Load()).Id);
            Assert.Single(documents.List("poi"));
            Assert.Throws<InvalidDataException>(() => storage.Save(first));
        });
    }

    [Fact]
    public void MySqlReadFailureLoadsLocalListAndAnOpenedLocalFlowStaysLocalAfterReconnect()
    {
        WithTemplates((storage, path) =>
        {
            var value = new FlowParam { Name = "本地" };
            storage.Save(value);
            TemplateFlow.Params.Add(new TemplateModel<FlowParam>("服务器", new FlowParam { Id = 8, Name = "服务器" }));
            int attempts = 0;
            bool connected = false;
            var templates = new TemplateFlow(storage, () => connected, () => { attempts++; throw new IOException("Unavailable MySQL"); });
            templates.Load();
            FlowParam loaded = Assert.Single(TemplateFlow.Params).Value;
            Assert.Equal(value.Id, loaded.Id);
            Assert.Contains("本地", templates.Title);
            connected = true;
            loaded.Name = "联网后保存";
            TemplateFlow.Save2DB(loaded);
            Assert.Equal(0, attempts);
            Assert.Equal("联网后保存", Open(path).Read(value.Id).Name);
            templates.Load();
            Assert.Equal(1, attempts);
            Assert.Equal(value.Id, Assert.Single(TemplateFlow.Params).Id);
            templates.Create("回退期间新建");
            Assert.Equal(2, Open(path).Load().Count);
        });
    }

    [Fact]
    public void StaleWindowCannotOverwriteNewerCanvasOrRecreateDeletedFlow()
    {
        WpfTestHost.Invoke(() =>
        {
            var storage = Open(NewPath());
            var value = new FlowParam { Name = "并发流程", DataBase64 = Convert.ToBase64String(CreateCanvas("初始")) };
            storage.Save(value);
            FlowParam first = storage.Read(value.Id);
            FlowParam second = storage.Read(value.Id);
            string originalHash = second.LoadedContentHash!;
            first.DataBase64 = Convert.ToBase64String(CreateCanvas("新版本"));
            storage.Save(first);
            // The explicit document baseline must win even if a shared model baseline was refreshed.
            second.LoadedContentHash = first.LoadedContentHash;
            second.DataBase64 = Convert.ToBase64String(CreateCanvas("过期窗口"));
            Assert.Throws<FlowTemplateConcurrencyException>(() => storage.Save(second, new FlowTemplateSaveCondition(originalHash)));
            Assert.Equal(first.DataBase64, storage.Read(value.Id).DataBase64);
            storage.Delete(value.Id);
            Assert.Throws<InvalidDataException>(() => storage.Save(second));
            Assert.Empty(storage.Load());
        });
    }

    [Fact]
    public void ConditionalDocumentUpdateDoesNotOverwriteAConcurrentChange()
    {
        var store = new LocalTemplateStore(NewPath());
        int id = store.Save("flow", null, "原名", 1, "first");
        var previous = store.Read("flow", id);
        store.Save("flow", id, "新名", 1, "second");
        Assert.False(store.TryUpdate("flow", previous, "过期", 1, "stale"));
        Assert.Equal("second", store.Read("flow", id).Payload);
        Assert.Equal("新名", store.Read("flow", id).Name);
    }

    [Fact]
    public void StnImportPreservesCanvasAndDoesNotRequireMySqlOrTemplateDictionary()
    {
        WithTemplates((storage, path) =>
        {
            var templates = Offline(storage);
            byte[] canvas = CreateCanvas("导入节点");
            string file = Path.ChangeExtension(path, ".stn");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, canvas);
            Assert.True(templates.ImportFile(file));
            templates.Create("导入");
            Assert.Equal(canvas, Convert.FromBase64String(Assert.Single(Open(path).Load()).DataBase64));
            File.WriteAllBytes(file, [1, 2, 3]);
            Assert.Throws<InvalidDataException>(() => templates.ImportFile(file));
            Assert.Single(Open(path).Load());
        });
    }

    [Theory]
    [InlineData(99, "{}")]
    [InlineData(1, "invalid json")]
    [InlineData(1, "{}")]
    [InlineData(1, "{\"FlowKey\":\"local-flow:93fa3c71f235441faf6fe1c842f4c366\",\"DataBase64\":\"AQID\"}")]
    public void InvalidOrFutureDocumentsFailInsteadOfBecomingEmptyFlows(int version, string payload)
    {
        string path = NewPath();
        new LocalTemplateStore(path).Save("flow", null, "损坏", version, payload);
        Exception? error = Record.Exception(() => Open(path).Load());
        Assert.True(error is InvalidDataException or Newtonsoft.Json.JsonException, error?.ToString() ?? "Expected an invalid document failure.");
        Assert.Equal(payload, new LocalTemplateStore(path).List("flow").Single().Payload);
    }

    [Fact]
    public void ServerIdentityCannotOverwriteOrBecomeALocalFlow()
    {
        string path = NewPath();
        var storage = Open(path);
        var local = new FlowParam { Name = "本地" };
        storage.Save(local);
        Assert.Throws<InvalidOperationException>(() => storage.Save(new FlowParam { Id = 1, Name = "服务器" }));
        Assert.Equal("本地", Assert.Single(storage.Load()).Name);
    }

    private static byte[] CreateCanvas(string name)
    {
        using var editor = new STNodeEditor();
        var start = new HeadlessTestStartNode();
        var end = new CVEndNode();
        start.Create();
        end.Create();
        start.NodeName = name;
        start.Left = 25;
        end.Left = 250;
        editor.Nodes.AddRange([start, end]);
        Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(end.m_in_start));
        return editor.GetCanvasData();
    }

    private static void WithTemplates(Action<LocalFlowTemplateStorage, string> action)
    {
        WpfTestHost.Invoke(() =>
        {
            var previous = TemplateFlow.Params;
            var registrations = TemplateControl.ITemplateNames.ToArray();
            string previousAppData = Environments.DirAppData;
            string path = NewPath();
            try
            {
                TemplateFlow.Params = [];
                // Keep the optional catalog sidecar out of the user's configuration directory.
                Environments.DirAppData = Path.GetDirectoryName(path)!;
                action(Open(path), path);
            }
            finally
            {
                TemplateFlow.Params = previous;
                Environments.DirAppData = previousAppData;
                TemplateControl.ITemplateNames.Clear();
                foreach (var item in registrations) TemplateControl.ITemplateNames.Add(item.Key, item.Value);
            }
        });
    }
}
