using ColorVision.Common.MVVM;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class CommandPanelTests
{
    [Fact]
    public void Groups_UseMetadataOrderLocalizationAndInheritedCommands()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new Fixture();
            var source = new Commands();
            PropertyEditorHelper.GetResourceManager(typeof(Commands), new CommandResources());
            try
            {
                PropertyEditorHelper.GenCommand(source, fixture.Host);
                var groups = fixture.Groups;
                Assert.Equal(new[] { "设备与连接", "维护与诊断" }, groups.Select(group => ((TextBlock)((StackPanel)group.Child).Children[0]).Text));
                Assert.Equal(new[] { "InheritedCommand", "RunCommand", "DeleteCommand" }, fixture.Buttons.Select(button => ((PropertyInfo)button.Tag).Name));
                Assert.Equal("执行操作", AutomationProperties.GetName(fixture.Buttons[1]));
                Assert.Equal("操作说明", AutomationProperties.GetHelpText(fixture.Buttons[1]));
                Assert.Contains("操作说明", fixture.Buttons[1].ToolTip.ToString());
                Assert.Equal(0, source.ExecutionCount);
            }
            finally
            {
                PropertyEditorHelper.ResourceManagerCache.TryRemove(typeof(Commands), out _);
            }
        });
    }

    [Fact]
    public void Regeneration_ReplacesOldButtonsAndKeepsLiveCommandBinding()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new Fixture();
            var source = new Commands();
            PropertyEditorHelper.GenCommand(source, fixture.Host);
            PropertyEditorHelper.GenCommand(source, fixture.Host);
            Assert.Equal(3, fixture.Buttons.Count);
            Button button = fixture.Buttons.Single(button => ((PropertyInfo)button.Tag).Name == nameof(Commands.RunCommand));
            var replacement = new RelayCommand(_ => { }, _ => false);
            source.RunCommand = replacement;
            Assert.Same(replacement, button.Command);
            Assert.False(button.IsEnabled);
        });
    }

    [Fact]
    public void CompactMode_KeepsTheFlatFlowCommandBarAndOrder()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new Fixture();
            PropertyEditorHelper.GenCommand(new Commands(), fixture.Host, compact: true);
            Assert.All(fixture.Host.Children.Cast<UIElement>(), child => Assert.IsType<Button>(child));
            Assert.Equal(new[] { "DeleteCommand", "InheritedCommand", "RunCommand" }, fixture.Host.Children.OfType<Button>().Select(button => ((PropertyInfo)button.Tag).Name));
            Assert.All(fixture.Host.Children.OfType<Button>(), button => Assert.IsType<string>(button.Content));
        });
    }

    [Fact]
    public void EmptyOrUncategorizedCommands_DoNotCreateEmptyHeadings()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new Fixture();
            PropertyEditorHelper.GenCommand(new object(), fixture.Host);
            Assert.Empty(fixture.Host.Children.Cast<UIElement>());
            PropertyEditorHelper.GenCommand(new UncategorizedCommands(), fixture.Host);
            var content = Assert.IsType<StackPanel>(Assert.Single(fixture.Groups).Child);
            Assert.Single(content.Children.Cast<UIElement>());
            Assert.IsType<Grid>(content.Children[0]);
            Assert.Single(fixture.Buttons);
        });
    }

    [Fact]
    public void CategoryLayout_FillsOddRowsAndStacksAtNarrowWidths()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new Fixture();
            PropertyEditorHelper.GenCommand(new ThreeCommands(), fixture.Host);
            var grid = ((StackPanel)fixture.Groups[0].Child).Children.OfType<Grid>().Single();
            fixture.Host.Measure(new Size(600, 1000));
            fixture.Host.Arrange(new Rect(0, 0, 600, fixture.Host.DesiredSize.Height));
            fixture.Host.UpdateLayout();
            Assert.Equal(2, grid.ColumnDefinitions.Count);
            Assert.Equal(2, Grid.GetColumnSpan(fixture.Buttons[2]));
            fixture.Host.Measure(new Size(300, 1000));
            fixture.Host.Arrange(new Rect(0, 0, 300, fixture.Host.DesiredSize.Height));
            fixture.Host.UpdateLayout();
            Assert.Single(grid.ColumnDefinitions);
            Assert.Equal(3, grid.RowDefinitions.Count);
        });
    }

    private sealed class ThreeCommands
    {
        [CommandDisplay("First")] public RelayCommand First { get; } = new(_ => { });
        [CommandDisplay("Second")] public RelayCommand Second { get; } = new(_ => { });
        [CommandDisplay("Third")] public RelayCommand Third { get; } = new(_ => { });
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ResourceDictionary savedResources = Application.Current.Resources;
        public UniformGrid Host { get; } = new();
        public List<Border> Groups => ((StackPanel)Host.Children[0]).Children.OfType<Border>().ToList();
        public List<Button> Buttons => Groups.SelectMany(group => ((StackPanel)group.Child).Children.OfType<Grid>().Single().Children.OfType<Button>()).ToList();

        public Fixture()
        {
            Application.Current.Resources = new ResourceDictionary
            {
                ["ButtonDefault"] = new Style(typeof(Button)),
                ["ButtonDefault.Small"] = new Style(typeof(Button)),
                ["GlobalBackground"] = Brushes.White,
                ["BorderBrush"] = Brushes.Gray,
                ["PrimaryTextBrush"] = Brushes.Black,
                ["SecondaryTextBrush"] = Brushes.Gray,
                ["DangerBrush"] = Brushes.Firebrick,
                ["UpdateDialogSurfaceColor"] = Colors.WhiteSmoke,
                ["UpdateDialogSurfaceHoverColor"] = Colors.White,
                ["UpdateDialogBorderColor"] = Colors.Gray,
                ["UpdateDialogTextPrimaryColor"] = Colors.Black,
                ["UpdateDialogTextSecondaryColor"] = Colors.Black,
                ["UpdateDialogAccentColor"] = Colors.DodgerBlue
            };
        }

        public void Dispose()
        {
            Application.Current.Resources = savedResources;
        }
    }

    private class BaseCommands : ViewModelBase
    {
        [CommandDisplay("Inherited", Order = 0, CategoryOrder = 0), Category("Connection")]
        public RelayCommand InheritedCommand { get; } = new(_ => { });
    }

    private sealed class Commands : BaseCommands
    {
        public int ExecutionCount { get; private set; }
        public Commands() => runCommand = new RelayCommand(_ => ExecutionCount++);

        [CommandDisplay("Run", Order = 2, CategoryOrder = 0), Category("Connection"), Description("RunDescription")]
        public RelayCommand RunCommand { get => runCommand; set { runCommand = value; OnPropertyChanged(); } }
        private RelayCommand runCommand;

        [CommandDisplay("Delete", Order = -100, CategoryOrder = 3, CommandType = CommandType.Highlighted), Category("Maintenance")]
        public RelayCommand DeleteCommand { get; } = new(_ => { });

        [CommandDisplay("Hidden"), Browsable(false)]
        public RelayCommand HiddenCommand { get; } = new(_ => { });

        [CommandDisplay("Null")]
        public RelayCommand? NullCommand => null;
        public RelayCommand UnannotatedCommand { get; } = new(_ => { });
    }

    private sealed class UncategorizedCommands
    {
        [CommandDisplay("Action")]
        public RelayCommand ActionCommand { get; } = new(_ => { });
    }

    private sealed class CommandResources : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture) => name switch
        {
            "Connection" => "设备与连接",
            "Maintenance" => "维护与诊断",
            "Run" => "执行操作",
            "RunDescription" => "操作说明",
            _ => name
        };
    }
}
