using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Browser;
using ColorVision.Engine.Templates.Flow;
using SqlSugar;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplateBrowserOrderTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, -1)]
    [InlineData(true, -1)]
    public async Task ReversedBrowserOrderKeepsSourceIdentityForFilteringOpeningSwappingAndDeleting(bool covers, int idSign)
    {
        await WpfTestHost.Invoke<Task>(async () =>
        {
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            var template = new BrowserTemplate();
            var first = new TemplateBase { Id = idSign * 10, Key = "Match first" };
            var middle = new TemplateBase { Id = idSign * 20, Key = "Middle" };
            var last = new TemplateBase { Id = idSign * 30, Key = "Match last" };
            template.Items.Add(first);
            template.Items.Add(middle);
            template.Items.Add(last);
            var service = new FlowTemplateCoverService(Path.Combine(Directory.CreateTempSubdirectory("ColorVision-browser-order-").FullName, "covers.db"));
            TemplateBrowserWindow? window = null;
            var settings = new TemplateSetting();
            try
            {
                window = new TemplateBrowserWindow(template, 0, new TemplateBrowserOptions("Test", covers, _ => { }), service, settings);
                var list = (ListView)window.FindName("TemplateList");
                var search = (TextBox)window.FindName("SearchBox");
                Assert.Equal(new[] { last, middle, first }, list.Items.Cast<TemplateBase>());
                Assert.Equal(new[] { first, middle, last }, template.Items);
                Assert.Same(first, list.SelectedItem);

                search.Text = "Match";
                Assert.Equal(new[] { last, first }, list.Items.Cast<TemplateBase>());
                list.SelectedItem = last;
                first.IsSelected = true;
                ((MenuItem)window.FindName("ListViewMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.IsType<GridView>(list.View);
                Assert.Same(last, list.SelectedItem);
                Assert.True(first.IsSelected);
                Assert.Equal("Match", search.Text);
                Assert.Equal(new[] { last, first }, list.Items.Cast<TemplateBase>());
                window.OpenSelected();
                Assert.Same(last, template.Opened);

                await window.SwapItemsAsync(last, first);
                Assert.Equal((2, 0), template.Swapped);
                Assert.Equal(new[] { first, last }, list.Items.Cast<TemplateBase>());
                Assert.Same(last, list.SelectedItem);

                search.Clear();
                var added = new TemplateBase { Id = idSign * 40, Key = "Newest" };
                template.Items.Add(added);
                Assert.Equal(new[] { added, first, middle, last }, list.Items.Cast<TemplateBase>());
                window.DeleteItems([last]);
                Assert.Same(last, template.Deleted);
                Assert.Equal(new[] { added, first, middle }, list.Items.Cast<TemplateBase>());

                window.Close();
                window = new TemplateBrowserWindow(template, 0, new TemplateBrowserOptions("Test", covers, _ => { }), service, settings);
                list = (ListView)window.FindName("TemplateList");
                Assert.IsType<GridView>(list.View);
                Assert.Equal(new[] { added, first, middle }, list.Items.Cast<TemplateBase>());
                var selected = list.SelectedItem;
                ((MenuItem)window.FindName("MediumIconsMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Null(list.View);
                Assert.Same(selected, list.SelectedItem);
                Assert.True(first.IsSelected);
            }
            finally
            {
                window?.Close();
                TemplateControl.ITemplateNames.Remove(template.Name);
                Application.Current.Resources.MergedDictionaries.Remove(theme);
            }
        });
    }

    private sealed class BrowserTemplate : ITemplate
    {
        public ObservableCollection<TemplateBase> Items { get; } = [];
        public override IEnumerable ItemsSource => Items;
        public override string Title { get; set; } = "Browser order test";
        public TemplateBase? Opened { get; private set; }
        public TemplateBase? Deleted { get; private set; }
        public (int, int) Swapped { get; private set; }
        public override void PreviewMouseDoubleClick(int index) => Opened = Items[index];
        public override void Delete(int index) { Deleted = Items[index]; Items.RemoveAt(index); }
        public override bool SwapTemplateOrder(int index1, int index2)
        {
            Swapped = (index1, index2);
            // Persisted swaps publish moves that keep source objects unique throughout.
            int low = Math.Min(index1, index2), high = Math.Max(index1, index2);
            Items.Move(high, low);
            if (high > low + 1) Items.Move(low + 1, high);
            return true;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwapKeepsEveryMasterFieldAndMovesAllDetailsWithItsTemplate(bool poi)
    {
        using var db = CreateDb(poi, out var master, out var detail);
        string original = Snapshot(db, master, detail);
        var fields10 = db.Ado.GetString($"SELECT name || code || cfg_json || create_date || remark FROM {master} WHERE id=10");
        var fields20 = db.Ado.GetString($"SELECT name || code || cfg_json || create_date || remark FROM {master} WHERE id=20");
        TemplateOrderSwap.SwapRecords(db, poi, 10, 20, "First", "Second");
        Assert.Equal(fields20, db.Ado.GetString($"SELECT name || code || cfg_json || create_date || remark FROM {master} WHERE id=10"));
        Assert.Equal(fields10, db.Ado.GetString($"SELECT name || code || cfg_json || create_date || remark FROM {master} WHERE id=20"));
        Assert.Equal(new[] { 20, 20, 10, 30 }, db.Ado.SqlQuery<int>($"SELECT pid FROM {detail} ORDER BY id"));
        Assert.Equal(new[] { "resource-a", "parameter-a", "resource-b", "untouched" }, db.Ado.SqlQuery<string>($"SELECT value_a FROM {detail} ORDER BY id"));
        Assert.Equal(0, db.Ado.GetInt($"SELECT COUNT(*) FROM {master} WHERE id<0"));
        TemplateOrderSwap.SwapRecords(db, poi, 10, 20, "Second", "First");
        Assert.Equal(original, Snapshot(db, master, detail));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FailureAfterMovingMasterIdsRollsBackTheEntireSwap(bool poi, bool ignoreMasterUpdate)
    {
        using var db = CreateDb(poi, out var master, out var detail);
        db.Ado.ExecuteCommand(ignoreMasterUpdate
            ? $"CREATE TRIGGER fail_swap BEFORE UPDATE ON {master} WHEN OLD.id=20 BEGIN SELECT RAISE(IGNORE); END"
            : $"CREATE TRIGGER fail_swap BEFORE UPDATE ON {detail} BEGIN SELECT RAISE(ABORT, 'blocked detail update'); END");
        string original = Snapshot(db, master, detail);
        Assert.ThrowsAny<Exception>(() => TemplateOrderSwap.SwapRecords(db, poi, 10, 20, "First", "Second"));
        Assert.Equal(original, Snapshot(db, master, detail));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletedOrRenamedRecordsAreRejectedBeforeAnyWrite(bool poi)
    {
        using var db = CreateDb(poi, out var master, out var detail);
        string original = Snapshot(db, master, detail);
        Assert.Throws<InvalidOperationException>(() => TemplateOrderSwap.SwapRecords(db, poi, 10, 20, "Old name", "Second"));
        Assert.Throws<InvalidOperationException>(() => TemplateOrderSwap.SwapRecords(db, poi, 10, 999, "First", "Missing"));
        Assert.Equal(original, Snapshot(db, master, detail));
    }

    private static SqlSugarClient CreateDb(bool poi, out string master, out string detail)
    {
        string path = Path.Combine(Directory.CreateTempSubdirectory("ColorVision-template-order-").FullName, "order.db");
        var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source={path}", DbType = DbType.Sqlite, IsAutoCloseConnection = true });
        master = poi ? "t_scgd_algorithm_poi_template_master" : "t_scgd_mod_param_master";
        detail = poi ? "t_scgd_algorithm_poi_template_detail" : "t_scgd_mod_param_detail";
        db.Ado.ExecuteCommand($"CREATE TABLE {master}(id INTEGER PRIMARY KEY,name TEXT,code TEXT,cfg_json TEXT,create_date TEXT,remark TEXT)");
        db.Ado.ExecuteCommand($"CREATE TABLE {detail}(id INTEGER PRIMARY KEY,pid INTEGER,value_a TEXT)");
        db.Ado.ExecuteCommand($"INSERT INTO {master} VALUES(10,'First','code-a','{{\"a\":1}}','2025-01-01','a'),(20,'Second','code-b','{{\"b\":2}}','2026-02-02','b'),(30,'Other','other','{{}}','2024-03-03','keep')");
        db.Ado.ExecuteCommand($"INSERT INTO {detail} VALUES(1,10,'resource-a'),(2,10,'parameter-a'),(3,20,'resource-b'),(4,30,'untouched')");
        return db;
    }

    private static string Snapshot(SqlSugarClient db, string master, string detail)
        => string.Join("|", db.Ado.SqlQuery<string>($"SELECT id || name || code || cfg_json || create_date || remark FROM {master} ORDER BY id"))
            + string.Join("|", db.Ado.SqlQuery<string>($"SELECT id || ':' || pid || ':' || value_a FROM {detail} ORDER BY id"));
}
