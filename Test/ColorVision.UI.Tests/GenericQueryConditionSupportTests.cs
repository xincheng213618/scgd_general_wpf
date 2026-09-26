using ColorVision.Database;
using SqlSugar;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class GenericQueryConditionSupportTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"colorvision-generic-query-{Guid.NewGuid():N}");
    private readonly List<SqlSugarClient> _clients = [];

    [Fact]
    public void QueryableProperties_UseFriendlyNamesAndExcludeUnsupportedMembers()
    {
        IReadOnlyList<KeyValuePair<string, PropertyInfo>> properties =
            GenericQueryConditionSupport.GetQueryableProperties(typeof(QueryEntity));

        Assert.Contains(properties, item => item.Key == "层级" && item.Value.Name == nameof(QueryEntity.ZIndex));
        Assert.DoesNotContain(properties, item => item.Key == "z_index");
        Assert.DoesNotContain(properties, item => item.Value.Name == nameof(QueryEntity.Ignored));
        Assert.DoesNotContain(properties, item => item.Value.Name == nameof(QueryEntity.Hidden));
    }

    [Fact]
    public void ConditionValue_DistinguishesMissingInvalidAndZero()
    {
        PropertyInfo property = typeof(QueryEntity).GetProperty(nameof(QueryEntity.ZIndex))!;
        var condition = new QueryCondition { Property = property };

        Assert.False(GenericQueryConditionSupport.TryGetConditionValue(condition, out _, out string missingError));
        Assert.NotEmpty(missingError);

        condition.InputText = "not-a-number";
        Assert.False(GenericQueryConditionSupport.TryGetConditionValue(condition, out _, out string invalidError));
        Assert.NotEmpty(invalidError);

        condition.InputText = "0";
        Assert.True(GenericQueryConditionSupport.TryGetConditionValue(condition, out object? value, out string error));
        Assert.Equal(0, value);
        Assert.Empty(error);
    }

    [Fact]
    public void ApplyConditions_SupportsBooleanAndDuplicateRangeFields()
    {
        SqlSugarClient db = CreateDatabase();
        db.Insertable(new List<QueryEntity>
        {
            new QueryEntity { Id = 1, ZIndex = 5, Enabled = false, Name = "outside" },
            new QueryEntity { Id = 2, ZIndex = 15, Enabled = false, Name = "match" },
            new QueryEntity { Id = 3, ZIndex = 15, Enabled = true, Name = "wrong flag" },
            new QueryEntity { Id = 4, ZIndex = 25, Enabled = false, Name = "outside" }
        }).ExecuteCommand();

        PropertyInfo zIndex = typeof(QueryEntity).GetProperty(nameof(QueryEntity.ZIndex))!;
        PropertyInfo enabled = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Enabled))!;
        QueryCondition[] conditions =
        [
            new() { Property = zIndex, Operator = QueryOperator.GreaterOrEqual, InputText = "10" },
            new() { Property = zIndex, Operator = QueryOperator.LessOrEqual, InputText = "20" },
            new() { Property = enabled, Operator = QueryOperator.Equal, Value = false }
        ];

        List<QueryEntity> results = GenericQueryConditionSupport
            .ApplyConditions(db.Queryable<QueryEntity>(), conditions)
            .ToList();

        QueryEntity result = Assert.Single(results);
        Assert.Equal(2, result.Id);
    }

    [Fact]
    public void ApplyConditions_IgnoresBlankRows()
    {
        SqlSugarClient db = CreateDatabase();
        db.Insertable(new List<QueryEntity>
        {
            new QueryEntity { Id = 1, ZIndex = 0, Name = "first" },
            new QueryEntity { Id = 2, ZIndex = 20, Name = "second" }
        }).ExecuteCommand();

        QueryCondition[] conditions =
        [
            new()
            {
                Property = typeof(QueryEntity).GetProperty(nameof(QueryEntity.ZIndex))!,
                Operator = QueryOperator.Equal
            },
            new()
            {
                Property = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Name))!,
                Operator = QueryOperator.Equal,
                InputText = "second"
            }
        ];

        QueryEntity result = Assert.Single(GenericQueryConditionSupport
            .ApplyConditions(db.Queryable<QueryEntity>(), conditions)
            .ToList());

        Assert.Equal(2, result.Id);
    }

    [Fact]
    public void SessionState_RestoresLastAppliedConditionsAndSettings()
    {
        StaTest.Run(() =>
        {
            GenericQuerySessionStore.ClearAll();
            SqlSugarClient db = CreateDatabase();
            PropertyInfo nameProperty = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Name))!;
            var firstQuery = new GenericQuery<QueryEntity>(db, new List<QueryEntity>());
            firstQuery.GetControl();
            firstQuery.AddPropertyInfo(nameProperty);
            QueryCondition firstCondition = Assert.Single(firstQuery.QueryConditions);
            firstCondition.Operator = QueryOperator.NotEqual;
            firstCondition.InputText = "remember";
            firstQuery.QueryConfig.Count = 250;
            firstQuery.QueryConfig.OrderByType = OrderByType.Asc;
            firstQuery.SaveSessionState();

            var restoredQuery = new GenericQuery<QueryEntity>(db, new List<QueryEntity>());
            restoredQuery.GetControl();
            QueryCondition restoredCondition = Assert.Single(restoredQuery.QueryConditions);

            Assert.Equal(nameof(QueryEntity.Name), restoredCondition.Property!.Name);
            Assert.Equal(QueryOperator.NotEqual, restoredCondition.Operator);
            Assert.Equal("remember", restoredCondition.InputText);
            Assert.Equal("remember", Assert.IsType<TextBox>(restoredCondition.ValueEditor).Text);
            Assert.Equal(250, restoredQuery.QueryConfig.Count);
            Assert.Equal(OrderByType.Asc, restoredQuery.QueryConfig.OrderByType);
            GenericQuerySessionStore.ClearAll();
        });
    }

    [Fact]
    public void InvalidCondition_DoesNotClearExistingResults()
    {
        StaTest.Run(() =>
        {
            SqlSugarClient db = CreateDatabase();
            var existing = new QueryEntity { Id = 99, Name = "keep" };
            var results = new List<QueryEntity> { existing };
            var query = new GenericQuery<QueryEntity>(db, results);
            query.QueryConditions.Add(new QueryCondition
            {
                Property = typeof(QueryEntity).GetProperty(nameof(QueryEntity.ZIndex))!,
                Operator = QueryOperator.Equal,
                InputText = "invalid"
            });

            Assert.Throws<FormatException>(query.QueryDB);
            Assert.Same(existing, Assert.Single(results));
        });
    }

    [Theory]
    [InlineData("层级", true)]
    [InlineData("INDEX", true)]
    [InlineData(" z_index ", true)]
    [InlineData("unknown", false)]
    public void FieldSearch_MatchesDisplayPropertyAndColumnNames(string search, bool expected)
    {
        var field = GenericQueryConditionSupport.GetQueryableProperties(typeof(QueryEntity))
            .Single(item => item.Value.Name == nameof(QueryEntity.ZIndex));
        Assert.Equal(expected, GenericQueryConditionSupport.MatchesField(field, search));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InlineRows_WorkForBothGenericQueryFormsAndRestoreOnlySelectedFields(bool converted)
    {
        StaTest.Run(() =>
        {
            SqlSugarClient db = CreateDatabase();
            db.Insertable(new List<QueryEntity>
            {
                new() { Id = 1, Name = "sample", Enabled = true },
                new() { Id = 2, Name = "sample-extra", Enabled = true },
                new() { Id = 3, Name = "sample", Enabled = false }
            }).ExecuteCommand();
            var results = new List<QueryEntity>();
            GenericQueryBase query = converted ? new GenericQuery<QueryEntity, QueryEntity>(db, results, row => row)
                : new GenericQuery<QueryEntity>(db, results);
            query.AddEmptyCondition();
            query.AddEmptyCondition();
            query.AddEmptyCondition();
            var conditions = Conditions(query);
            conditions[0].FieldEditor!.SelectedValue = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Name));
            Assert.IsType<TextBox>(conditions[0].ValueEditor).Text = "sample";
            OperatorEditor(conditions[0]).SelectedValue = QueryOperator.Equal;
            conditions[1].FieldEditor!.SelectedValue = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Enabled));
            Assert.IsType<ComboBox>(conditions[1].ValueEditor).SelectedValue = true;
            query.QueryDB();
            Assert.Equal(1, Assert.Single(results).Id);
            query.SaveSessionState();
            Assert.Equal(2, GenericQuerySessionStore.Load(typeof(QueryEntity))!.Conditions.Count);

            GenericQueryBase restored = converted ? new GenericQuery<QueryEntity, QueryEntity>(db, results, row => row)
                : new GenericQuery<QueryEntity>(db, results);
            restored.GetControl();
            Assert.Equal(2, restored.ConditionCount);
            Assert.Equal(QueryOperator.Equal, Conditions(restored)[0].Operator);
            Assert.Equal("sample", Assert.IsType<TextBox>(Conditions(restored)[0].ValueEditor).Text);
            restored.AddEmptyCondition();
            restored.AddAllPropertyInfos();
            Assert.Equal(restored.PropertyInfos.Count + 1, restored.ConditionCount);
            restored.RemoveCondition(Conditions(restored)[0]);
            Assert.Equal(restored.PropertyInfos.Count, restored.ConditionCount);
            restored.ResetConditions();
            Assert.Empty(Conditions(restored));
        });
    }

    [Fact]
    public void ChangingFields_ReplacesTypedEditorsAndClearsOldValuesAndErrors()
    {
        StaTest.Run(() =>
        {
            var query = new GenericQuery<QueryEntity>(CreateDatabase(), new List<QueryEntity>());
            query.AddPropertyInfo(typeof(QueryEntity).GetProperty(nameof(QueryEntity.Name))!);
            QueryCondition condition = Assert.Single(query.QueryConditions);
            Assert.Equal(QueryOperator.Like, condition.Operator);
            Assert.IsType<TextBox>(condition.ValueEditor).Text = "old value";
            condition.ErrorText!.Text = "old error";
            condition.ErrorText.Visibility = Visibility.Visible;
            condition.FieldEditor!.SelectedValue = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Enabled));
            Assert.Null(condition.InputText);
            Assert.Null(condition.Value);
            Assert.Equal(QueryOperator.Equal, condition.Operator);
            Assert.Equal(Visibility.Collapsed, condition.ErrorText.Visibility);
            var booleanEditor = Assert.IsType<ComboBox>(condition.ValueEditor);
            Assert.Equal(2, booleanEditor.Items.Count);
            booleanEditor.SelectedValue = false;
            Assert.Equal(false, condition.Value);

            condition.FieldEditor.SelectedValue = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Created));
            Assert.Null(condition.Value);
            Assert.IsType<DatePicker>(condition.ValueEditor).SelectedDate = new DateTime(2026, 1, 2);
            condition.FieldEditor.SelectedValue = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Kind));
            Assert.Null(condition.Value);
            Assert.IsType<ComboBox>(condition.ValueEditor).SelectedValue = QueryKind.Second;
            Assert.Equal(QueryKind.Second, condition.Value);
            condition.FieldEditor.SelectedValue = typeof(QueryEntity).GetProperty(nameof(QueryEntity.ZIndex));
            Assert.Null(condition.Value);
            Assert.Equal(string.Empty, Assert.IsType<TextBox>(condition.ValueEditor).Text);
            Assert.Equal(6, OperatorEditor(condition).Items.Count);
        });
    }

    [Theory]
    [InlineData(760, true)]
    [InlineData(640, false)]
    public void Window_InlineSearchKeyboardResetAndLayoutUseRealControls(double width, bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            Theme previous = ThemeManager.Current.CurrentUITheme;
            ThemeManager.Current.ApplyThemeChanged(Application.Current, dark ? Theme.Light : Theme.Dark);
            ThemeManager.Current.ApplyThemeChanged(Application.Current, dark ? Theme.Dark : Theme.Light);
            var query = new GenericQuery<QueryEntity>(CreateDatabase(), new List<QueryEntity> { new() { Id = 99 } });
            var window = new GenericQueryWindow(query)
            {
                Width = width, Height = dark ? 460 : 400, Left = -20000, Top = -20000,
                ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual
            };
            try
            {
                int completed = 0;
                query.QueryCompleted += (_, _) => completed++;
                window.Show();
                Pump();
                QueryCondition first = Assert.Single(query.QueryConditions);
                Assert.Null(first.Property);
                Assert.False(first.ValueEditor!.IsEnabled);
                AssertAddButtonInViewport(window);
                Capture(window, $"query-empty-{width}.png");
                ((Button)window.FindName("AddConditionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                QueryCondition second = query.QueryConditions[1];
                int allFields = second.FieldEditor!.Items.Count;
                first.FieldEditor!.ApplyTemplate();
                var search = (TextBox)first.FieldEditor.Template.FindName("PART_EditableTextBox", first.FieldEditor);
                search.Text = "z_ind";
                Assert.Single(first.FieldEditor.Items);
                Assert.Equal(allFields, second.FieldEditor.Items.Count);
                var enter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                first.FieldEditor.RaiseEvent(enter);
                Assert.True(enter.Handled);
                Assert.Equal(nameof(QueryEntity.ZIndex), first.Property!.Name);
                Assert.Equal(0, completed);
                Assert.IsType<TextBox>(first.ValueEditor).Text = "12";
                search.Text = "not a field";
                Assert.Null(first.Property);
                Assert.False(first.ValueEditor!.IsEnabled);
                Assert.Throws<FormatException>(query.QueryDB);
                Assert.Equal(99, Assert.Single(query.ViewResluts).Id);
                Assert.Equal(Visibility.Visible, first.ErrorText!.Visibility);
                search.Clear();
                Assert.Equal(allFields, first.FieldEditor.Items.Count);
                first.FieldEditor.IsDropDownOpen = true;
                first.FieldEditor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Down)
                { RoutedEvent = Keyboard.KeyDownEvent });
                first.FieldEditor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert.NotNull(first.Property);
                Assert.Equal(0, completed);
                first.FieldEditor.SelectedValue = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Name));
                Assert.IsType<TextBox>(first.ValueEditor).Text = "sample-A";
                OperatorEditor(first).SelectedValue = QueryOperator.Equal;
                second.FieldEditor.SelectedValue = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Enabled));
                Assert.IsType<ComboBox>(second.ValueEditor).SelectedValue = false;
                first.FieldEditor.IsDropDownOpen = false;
                first.ValueEditor!.Focus();
                Pump();
                foreach (QueryCondition row in query.QueryConditions)
                {
                    Assert.True(row.ValueEditor!.ActualWidth > 80);
                    Point right = row.ValueEditor.TranslatePoint(new Point(row.ValueEditor.ActualWidth, 0), window);
                    Assert.True(right.X < window.ActualWidth);
                }
                if (dark) AssertAddButtonInViewport(window);
                Capture(window, $"query-inline-{width}.png");
                ((Button)window.FindName("ResetConditionsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Null(Assert.Single(query.QueryConditions).Property);
                window.Close();
                Assert.Empty(GenericQuerySessionStore.Load(typeof(QueryEntity))!.Conditions);
            }
            finally
            {
                window.Close();
                ThemeManager.Current.ApplyThemeChanged(Application.Current, previous == Theme.Dark ? Theme.Light : Theme.Dark);
                ThemeManager.Current.ApplyThemeChanged(Application.Current, previous);
            }
        });
    }

    private static IList<QueryCondition> Conditions(GenericQueryBase query) => query is GenericQuery<QueryEntity> direct
        ? direct.QueryConditions : ((GenericQuery<QueryEntity, QueryEntity>)query).QueryConditions;

    private static ComboBox OperatorEditor(QueryCondition condition) => ((Grid)((Border)condition.UiRow!).Child)
        .Children.OfType<ComboBox>().Single(control => Grid.GetColumn(control) == 1);

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void AssertAddButtonInViewport(Window window)
    {
        var button = (Button)window.FindName("AddConditionButton");
        DependencyObject parent = button;
        while (parent is not ScrollViewer) parent = VisualTreeHelper.GetParent(parent);
        var scroll = (ScrollViewer)parent;
        Point bottom = button.TranslatePoint(new Point(0, button.ActualHeight), scroll);
        Assert.InRange(bottom.Y, button.ActualHeight, scroll.ActualHeight);
    }

    private static void Capture(Window window, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("GENERIC_QUERY_PREVIEW_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var content = (FrameworkElement)window.Content;
        var bounds = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle((Brush)window.FindResource("GlobalBackground"), null, bounds);
            drawing.DrawRectangle(new VisualBrush(content), null, bounds);
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(bounds.Width * 1.5), (int)Math.Ceiling(bounds.Height * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

    private SqlSugarClient CreateDatabase()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        string databasePath = Path.Combine(_temporaryDirectory, $"{Guid.NewGuid():N}.db");
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<QueryEntity>();
        _clients.Add(db);
        return db;
    }

    public void Dispose()
    {
        GenericQuerySessionStore.ClearAll();
        foreach (SqlSugarClient client in _clients)
        {
            client.Close();
            client.Dispose();
        }

        if (Directory.Exists(_temporaryDirectory))
            Directory.Delete(_temporaryDirectory, recursive: true);
    }

    [SugarTable("generic_query_test")]
    public sealed class QueryEntity : IEntity
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Display(Name = "层级")]
        [SugarColumn(ColumnName = "z_index")]
        public int ZIndex { get; set; }

        public bool Enabled { get; set; }
        public string Name { get; set; } = string.Empty;
        [SugarColumn(IsNullable = true)]
        public DateTime? Created { get; set; }
        public QueryKind Kind { get; set; }

        [SugarColumn(IsIgnore = true)]
        public string Ignored { get; set; } = string.Empty;

        [Browsable(false)]
        public string Hidden { get; set; } = string.Empty;
    }

    public enum QueryKind { First, Second }
}
