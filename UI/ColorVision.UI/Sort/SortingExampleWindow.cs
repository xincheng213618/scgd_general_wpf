using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Examples
{
    /// <summary>
    /// 使用示例：展示如何使用新的通用排序功能
    /// </summary>
    public partial class SortingExampleWindow : Window
    {
        public class SampleData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
            public double Value { get; set; }
        }

        public ObservableCollection<SampleData> DataList { get; set; }
        private SortManager<SampleData> _sortManager;

        public SortingExampleWindow()
        {
            InitializeData();
        }

        private void InitializeData()
        {
            // 创建示例数据
            DataList = new ObservableCollection<SampleData>
            {
                new SampleData { Id = 3, Name = "Item 3", CreateTime = DateTime.Now.AddDays(-1), Value = 15.5 },
                new SampleData { Id = 1, Name = "Item 1", CreateTime = DateTime.Now, Value = 10.2 },
                new SampleData { Id = 2, Name = "Item 2", CreateTime = DateTime.Now.AddDays(-2), Value = 20.8 }
            };

            // 创建排序管理器
            _sortManager = new SortManager<SampleData>(DataList);

            // 绑定数据
            DataContext = this;
        }

        #region 使用方法一：直接使用扩展方法（推荐）

        private void SortById_Click(object sender, RoutedEventArgs e)
        {
            // 直接使用通用排序扩展方法，无需实现 ISortID
            DataList.SortBy("Id");
        }

        private void SortByName_Click(object sender, RoutedEventArgs e)
        {
            // 对字符串属性排序，自动使用逻辑排序
            DataList.SortBy("Name", descending: true);
        }

        private void SortByCreateTime_Click(object sender, RoutedEventArgs e)
        {
            // 对日期时间排序
            DataList.SortBy("CreateTime");
        }

        private void SmartSort_Click(object sender, RoutedEventArgs e)
        {
            // 智能排序：自动检测 Id、Key、Name 等属性
            DataList.SmartSort();
        }

        private void SortByLambda_Click(object sender, RoutedEventArgs e)
        {
            // 使用 Lambda 表达式排序
            DataList.SortBy(x => x.Value, descending: true);
        }

        private void MultiSort_Click(object sender, RoutedEventArgs e)
        {
            // 多级排序：先按 Name，再按 Id
            DataList.SortByMultiple(
                ("Name", false),
                ("Id", false)
            );
        }

        #endregion

        #region 使用方法二：使用排序管理器（高级功能）

        private void ApplySort_Click(object sender, RoutedEventArgs e)
        {
            // 应用排序并记录配置
            _sortManager.ApplySort("Name");
        }

        private void ToggleSort_Click(object sender, RoutedEventArgs e)
        {
            // 切换排序方向
            _sortManager.ToggleSortDirection();
        }

        private void SaveSort_Click(object sender, RoutedEventArgs e)
        {
            // 保存当前排序配置
            _sortManager.SaveSort("MySort");
        }

        private void LoadSort_Click(object sender, RoutedEventArgs e)
        {
            // 加载保存的排序配置
            _sortManager.LoadSort("MySort");
        }

        #endregion

        #region 使用方法三：ListView 增强功能（最简单）

        private void SetupEnhancedListView()
        {
            // 如果你想要自动的排序和列管理功能，可以使用 EnhancedListView
            var enhancedListView = new EnhancedListView
            {
                EnableSorting = true,
                EnableColumnManagement = true,
                DefaultSortProperty = "Id",
                ItemsSource = DataList
            };

            // 支持点击列头排序
            // 支持右键菜单进行列管理和排序
            // 自动保存排序状态
        }

        #endregion

        #region 使用方法四：与现有 GridViewColumnVisibility 集成

        private void SetupGridViewColumnVisibility()
        {
            var listView = new ListView();
            var gridView = new GridView();
            
            // 添加列...
            
            listView.View = gridView;

            var contextMenu = new ContextMenu();
            var columnVisibilities = new ObservableCollection<GridViewColumnVisibility>();

            // 生成增强的右键菜单（包含排序功能）
            GridViewColumnVisibility.GenContentMenuGridViewColumn(
                contextMenu, gridView.Columns, columnVisibilities, listView);

            listView.ContextMenu = contextMenu;

            // 现在右键菜单包含：
            // - 自动调整列宽
            // - 智能排序（升序/降序）
            // - 按各列排序
            // - 显示/隐藏列
        }

        #endregion

        #region 添加唯一元素示例

        private void AddUniqueItem_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new SampleData 
            { 
                Id = 1, // 相同的 Id
                Name = "Duplicate Item", 
                CreateTime = DateTime.Now, 
                Value = 5.0 
            };

            // 使用通用的添加唯一元素方法
            DataList.AddUniqueBy(newItem, x => x.Id);
            // 不会添加，因为已存在 Id = 1 的元素
        }

        #endregion
    }
}

/* XAML 示例
<Window x:Class="ColorVision.UI.Examples.SortingExampleWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:sorts="clr-namespace:ColorVision.UI.Sorts"
        Title="排序示例" Height="600" Width="800">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        
        <!-- 按钮面板 -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="10">
            <Button Content="按 ID 排序" Click="SortById_Click" Margin="5"/>
            <Button Content="按名称排序" Click="SortByName_Click" Margin="5"/>
            <Button Content="智能排序" Click="SmartSort_Click" Margin="5"/>
            <Button Content="多级排序" Click="MultiSort_Click" Margin="5"/>
            <Button Content="切换排序" Click="ToggleSort_Click" Margin="5"/>
        </StackPanel>
        
        <!-- ListView -->
        <ListView Grid.Row="1" ItemsSource="{Binding DataList}" Margin="10">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="ID" DisplayMemberBinding="{Binding Id}" Width="80"/>
                    <GridViewColumn Header="名称" DisplayMemberBinding="{Binding Name}" Width="150"/>
                    <GridViewColumn Header="创建时间" DisplayMemberBinding="{Binding CreateTime}" Width="150"/>
                    <GridViewColumn Header="值" DisplayMemberBinding="{Binding Value}" Width="100"/>
                </GridView>
            </ListView.View>
        </ListView>
        
        <!-- 或者使用增强的 ListView -->
        <!--
        <sorts:EnhancedListView Grid.Row="1" 
                                ItemsSource="{Binding DataList}" 
                                EnableSorting="True" 
                                EnableColumnManagement="True"
                                DefaultSortProperty="Id"
                                Margin="10">
            <sorts:EnhancedListView.View>
                <GridView>
                    <GridViewColumn Header="ID" DisplayMemberBinding="{Binding Id}" Width="80"/>
                    <GridViewColumn Header="名称" DisplayMemberBinding="{Binding Name}" Width="150"/>
                    <GridViewColumn Header="创建时间" DisplayMemberBinding="{Binding CreateTime}" Width="150"/>
                    <GridViewColumn Header="值" DisplayMemberBinding="{Binding Value}" Width="100"/>
                </GridView>
            </sorts:EnhancedListView.View>
        </sorts:EnhancedListView>
        -->
    </Grid>
</Window>
*/