using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Templates;
using ColorVision.Themes;
using Microsoft.Data.Sqlite;
using MQTTMessageLib.Sensor;
using SqlSugar;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SensorTemplatePresetTests
{
    [Theory]
    [InlineData("pg-power-on", "PG,POWER,ON")]
    [InlineData("pg-power-off", "PG,POWER,OFF")]
    [InlineData("pg-white-255", "PG,IMAGE,w255.png")]
    [InlineData("pg-gray-128", "PG,IMAGE,w128.png")]
    [InlineData("pg-black", "PG,IMAGE,w0.png")]
    [InlineData("pg-pattern-1by4", "PG,IMAGE,1by4.png")]
    [InlineData("pg-pattern-2by4", "PG,IMAGE,2by4.png")]
    [InlineData("pg-pattern-3by4", "PG,IMAGE,3by4.png")]
    [InlineData("pg-pattern-4by4", "PG,IMAGE,4by4.png")]
    [InlineData("pg-apl-10", "PG,IMAGE,apl10.png")]
    [InlineData("pg-apl-25", "PG,IMAGE,apl25.png")]
    public void ShippedPgCommandsPreserveWireBytesAndLegacyFiveFields(string id, string body)
    {
        var preset = Assert.IsType<SensorTemplatePreset>(SensorTemplatePresets.Find(id));
        var command = Assert.Single(SensorTemplatePresets.CreateDraft(preset: preset).SensorCommands);
        Assert.Equal(SensorCmdType.Hex, command.SensorCmdType);
        Assert.Equal(body + "\n", SensorCommandTextFormatter.ConvertEncoding(command.Request, SensorCmdType.Hex, SensorCmdType.Ascii));
        Assert.Equal(body + ",END\r\n", SensorCommandTextFormatter.ConvertEncoding(command.Response, SensorCmdType.Hex, SensorCmdType.Ascii));
        Assert.Equal(5, command.Model.ValueA.Split(',').Length);
        Assert.Equal(body + "[LF]", command.RequestReadableText);
        Assert.Equal(5000, command.Timeout);
        Assert.Contains("本地通用传感器", preset.ModuleGuide);
    }

    [Theory]
    [InlineData("sa-light-on", "SA0200#")]
    [InlineData("sa-light-off", "SA0000#\n")]
    public void ShippedLightCommandsPreserveAsymmetricTerminators(string id, string request)
    {
        var command = Assert.Single(SensorTemplatePresets.CreateDraft(preset: SensorTemplatePresets.Find(id)!).SensorCommands);
        Assert.Equal(SensorCmdType.Ascii, command.SensorCmdType);
        Assert.Equal(request, command.Request);
        Assert.Equal("A", command.Response);
        Assert.Equal(1000, command.Timeout);
    }

    [Fact]
    public void ReadingCatalogDoesNotModifyItOrCreateSidecarsAndFiltersTemplateType()
    {
        string path = BuiltInTemplateCatalog.DatabasePath;
        byte[] before = File.ReadAllBytes(path);
        Assert.Equal(13, SensorTemplatePresets.All.Count);
        Assert.Empty(BuiltInTemplateCatalog.Read("Sensor.Custom"));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + "-wal"));
        Assert.False(File.Exists(path + "-shm"));
    }

    [Fact]
    public void MissingCatalogIsNotCreatedAndUnknownSchemaIsNotMigrated()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        Assert.Throws<FileNotFoundException>(() => BuiltInTemplateCatalog.Read("Sensor.Default", path));
        Assert.False(File.Exists(path));
        try
        {
            using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
            {
                db.Open();
                using var command = db.CreateCommand();
                command.CommandText = "PRAGMA user_version = 99;";
                command.ExecuteNonQuery();
            }
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<InvalidDataException>(() => BuiltInTemplateCatalog.Read("Sensor.Default", path));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DraftCopiesEveryCommandWithoutSourceIdsOrSharedMutableRows()
    {
        var source = new SensorParam();
        source.ModDetailModels.Add(new() { Id = 10, Pid = 20, SysPid = 3, ValueA = "A,B,Ascii,1000/0,0", ValueB = "metadata" });
        source.ModDetailModels.Add(new() { Id = 11, Pid = 20, SysPid = 3, ValueA = "C,D,Ascii,1000/0,0", IsEnable = false });
        var draft = SensorTemplatePresets.CreateDraft(source);
        Assert.Equal(2, draft.SensorCommands.Count);
        draft.SensorCommands[0].Request = "E";
        draft.CreateCommand.Execute(null);
        Assert.Equal(2, source.SensorCommands.Count);
        Assert.Equal("A", source.SensorCommands[0].Request);
        Assert.Equal(3, draft.ModDetailModels.Count);
        var saved = TemplateSensor.BuildCreationDetails(draft, 90, [new() { Id = 42 }]);
        Assert.Equal(3, saved.Count);
        Assert.All(saved, row => { Assert.Equal(0, row.Id); Assert.Equal(90, row.Pid); Assert.Equal(42, row.SysPid); });
        Assert.Equal("metadata", saved[0].ValueB);
        Assert.Equal("C,D,Ascii,1000/0,0", saved[1].ValueA);
        Assert.False(saved[1].IsEnable);
        draft.ModDetailModels.Clear();
        Assert.Throws<InvalidOperationException>(() => TemplateSensor.BuildCreationDetails(draft, 90, [new() { Id = 42 }]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SavingDraftCommitsAllCommandsOrRollsBackMasterAndDefinitionRepair(bool fail)
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        // Production is MySQL; adapt only its insert-ignore syntax in this isolated transaction test.
        db.Aop.OnExecutingChangeSql = (sql, parameters) => new KeyValuePair<string, SugarParameter[]>(sql.Replace("INSERT IGNORE", "INSERT OR IGNORE"), parameters);
        db.CodeFirst.InitTables<ModMasterModel, ModDetailModel, SysDictionaryModDetaiModel>();
        int oldId = db.Insertable(new ModMasterModel { Pid = 1, Name = "Existing" }).ExecuteReturnIdentity();
        db.Insertable(new ModDetailModel { Pid = oldId, SysPid = 999, ValueA = "old" }).ExecuteCommand();
        var draft = SensorTemplatePresets.CreateDraft(preset: SensorTemplatePresets.Find("pg-power-on")!);
        draft.ModDetailModels.Add(new ModDetailModel { ValueA = "C,D,Ascii,1000/0,0", ValueB = "metadata" });
        if (fail)
        {
            db.Ado.ExecuteCommand("CREATE TRIGGER fail_save BEFORE INSERT ON t_scgd_mod_param_detail BEGIN SELECT RAISE(ABORT,'simulated failure'); END");
            Assert.ThrowsAny<Exception>(() => TemplateSensor.InsertTemplate(db, 1, "New", draft));
            Assert.Equal(1, db.Queryable<ModMasterModel>().Count());
            Assert.Equal(1, db.Queryable<ModDetailModel>().Count());
            Assert.Empty(db.Queryable<SysDictionaryModDetaiModel>().ToList());
            Assert.Equal(999, db.Queryable<ModDetailModel>().Single().SysPid);
        }
        else
        {
            var (master, definitions) = TemplateSensor.InsertTemplate(db, 1, "New", draft);
            var rows = db.Queryable<ModDetailModel>().Where(row => row.Pid == master.Id).OrderBy(row => row.Id).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal(draft.ModDetailModels.Select(row => row.ValueA), rows.Select(row => row.ValueA));
            Assert.All(rows, row => Assert.Equal(Assert.Single(definitions).Id, row.SysPid));
            Assert.Equal("metadata", rows[1].ValueB);
            Assert.All(draft.ModDetailModels, row => Assert.Equal(0, row.Id));
        }
    }

    [Fact]
    public void ChoosingEditingAndCancellingPresetIsDetachedAndShowsModuleAndReadableCommands()
    {
        WithTheme(() =>
        {
            var template = new PreviewTemplate();
            var view = new TemplateCreateView();
            view.Initialize(template);
            var sources = (ListBox)view.FindName("SourceListBox");
            sources.SelectedIndex = 1;
            var host = (Grid)view.FindName("CustomPreviewHost");
            var preview = Assert.IsType<SensorTemplateCreatePreview>(Assert.Single(host.Children.Cast<UIElement>()));
            preview.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            Assert.Contains("本地通用传感器", ((TextBlock)preview.FindName("ModuleGuideText")).Text);
            Assert.Equal("PG,POWER,ON[LF]", ((TextBox)preview.FindName("ReadableRequest")).Text);
            Assert.Equal("PG,POWER,ON,END[CR][LF]", ((TextBox)preview.FindName("ReadableResponse")).Text);
            Assert.StartsWith("POWER_ON", ((TextBox)view.FindName("CreateNameTextBox")).Text);
            var draft = template.CreateTemp!;
            draft.SensorCommands[0].Timeout = 1234;
            sources.SelectedIndex = 2;
            Assert.Equal(5000, template.CreateTemp!.SensorCommands[0].Timeout);
            Assert.Equal(1234, draft.SensorCommands[0].Timeout);
            view.Discard();
            Assert.Null(template.CreateTemp);
            Assert.Null(template.ImportTemp);
            Assert.False(template.Created);
            Assert.Equal(5000, SensorTemplatePresets.CreateDraft(preset: SensorTemplatePresets.Find("pg-power-on")!).SensorCommands[0].Timeout);
        });
    }

    [Fact]
    public void CatalogFailureLeavesDefaultCreationAvailableWithVisibleExplanation()
    {
        WithTheme(() =>
        {
            var template = new PreviewTemplate { FailCatalog = true };
            var view = new TemplateCreateView();
            view.Initialize(template);
            Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("PresetLoadStatus")).Visibility);
            Assert.NotNull(template.CreateTemp);
            Assert.Equal(0, ((ListBox)view.FindName("SourceListBox")).SelectedIndex);
            view.Discard();
        });
    }

    private static void WithTheme(Action action) => WpfTestHost.Invoke(() =>
    {
        var old = Application.Current.Resources;
        try
        {
            var resources = new ResourceDictionary();
            foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
            Application.Current.Resources = resources;
            action();
        }
        finally { Application.Current.Resources = old; }
    });

    private sealed class PreviewTemplate : ITemplate<SensorParam>, ITemplateCreatePresetProvider
    {
        public bool FailCatalog { get; init; }
        public bool Created { get; private set; }
        public PreviewTemplate() { Code = "Sensor.PreviewTest"; IsUserControl = true; }
        public override IEnumerable<TemplateCreatePreset> GetCreatePresets() => FailCatalog
            ? throw new InvalidDataException("Unsupported catalog") : SensorTemplatePresets.All.Select(p => p.Source);
        public override bool ApplyCreatePreset(string id) { ImportTemp = SensorTemplatePresets.CreateDraft(preset: SensorTemplatePresets.Find(id)!); return true; }
        public override object CreateDefault() => CreateTemp = SensorTemplatePresets.CreateDraft(ImportTemp);
        public override UserControl CreateUserControl() => new SensorTemplateCreatePreview();
        public override void Create(string name) => Created = true;
        public override bool Import() => false;
    }
}
