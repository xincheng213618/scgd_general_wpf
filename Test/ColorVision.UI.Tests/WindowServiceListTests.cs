using ColorVision.Common.MVVM;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class WindowServiceListTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(-1, 1)]
    [InlineData(3, 1)]
    public void LegacyTypeViewBecomesServicesAndDevicePreferenceSurvives(int saved, int expected)
    {
        var config = new WindowServiceConfig { ShowType2 = saved };
        config.NormalizeListMode();
        Assert.Equal(expected, config.ShowType2);
        config.ToggleListMode();
        Assert.Equal(expected == 1 ? 2 : 1, config.ShowType2);
        config.ToggleListMode();
        Assert.Equal(expected, config.ShowType2);
        Assert.Equal(1, new WindowServiceConfig().ShowType2);
    }

    [Theory]
    [InlineData("zh-Hans", "滤色轮", "算法服务", "新建服务配置并添加设备")]
    [InlineData("en-US", "Filter wheel", "Algorithm service", "Create service configuration and add device")]
    [InlineData("zh-Hant", "濾色輪", "演算法服務", "新增服務設定並新增設備")]
    public void CreationKeepsExistingServicesAndEmptyTypesWithoutRenamingDatabaseData(string culture, string filterName, string algorithmName, string createName)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                var filter = MakeType(ServiceTypes.FilterWheel, "数据库原始类型名称");
                var emptyAlgorithm = MakeType(ServiceTypes.Algorithm, "自定义算法类型");
                var unsupported = MakeType(ServiceTypes.None, "不支持的类型");
                // No MQTT subscriptions or database constructors are needed for menu projection.
                var terminal = (TerminalService)RuntimeHelpers.GetUninitializedObject(typeof(TerminalService));
                terminal.SysResourceModel = new SysResourceModel { Name = "客户服务配置", Code = "SVR.FilterWheel.Custom", Type = (int)ServiceTypes.FilterWheel };
                int existingInvocations = 0;
                terminal.OpenCreateWindowCommand = new RelayCommand(_ => existingInvocations++);
                filter.VisualChildren.Add(terminal);
                TypeService? requested = null;

                var menu = WindowService.BuildCreateDeviceMenu([filter, emptyAlgorithm, unsupported], type => requested = type);
                var groups = menu.Items.OfType<MenuItem>().ToArray();
                Assert.Equal(2, groups.Length);
                Assert.Equal(filterName, groups[0].Header);
                Assert.Equal(algorithmName, groups[1].Header);
                var existing = Assert.IsType<MenuItem>(groups[0].Items[0]);
                Assert.Equal("客户服务配置", existing.Header);
                Assert.Same(terminal.OpenCreateWindowCommand, existing.Command);
                existing.Command.Execute(null);
                Assert.Equal(1, existingInvocations);
                Assert.IsType<Separator>(groups[0].Items[1]);
                var create = Assert.IsType<MenuItem>(Assert.Single(groups[1].Items.Cast<object>()));
                Assert.Equal(createName, create.Header);
                create.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Same(emptyAlgorithm, requested);
                Assert.Equal("数据库原始类型名称", filter.Name);
                Assert.Equal("数据库原始类型名称", filter.SysDictionaryModel.Name);
                Assert.Equal("客户服务配置", terminal.Name);
                Assert.Equal("SVR.FilterWheel.Custom", terminal.Code);
                Assert.Equal((int)ServiceTypes.FilterWheel, terminal.SysResourceModel.Type);
            }
            finally { CultureInfo.CurrentUICulture = previousCulture; }
        });
    }

    private static TypeService MakeType(ServiceTypes type, string name) => new()
    {
        Name = name,
        SysDictionaryModel = new SysDictionaryModel { Name = name, Value = (int)type },
        VisualChildren = new ObservableCollection<ServiceObjectBase>()
    };
}
