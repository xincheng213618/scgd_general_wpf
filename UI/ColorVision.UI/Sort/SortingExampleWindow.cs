using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Examples
{
    /// <summary>
    /// ʹ��ʾ����չʾ���ʹ���µ�ͨ��������
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
            // ����ʾ������
            DataList = new ObservableCollection<SampleData>
            {
                new SampleData { Id = 3, Name = "Item 3", CreateTime = DateTime.Now.AddDays(-1), Value = 15.5 },
                new SampleData { Id = 1, Name = "Item 1", CreateTime = DateTime.Now, Value = 10.2 },
                new SampleData { Id = 2, Name = "Item 2", CreateTime = DateTime.Now.AddDays(-2), Value = 20.8 }
            };

            // �������������
            _sortManager = new SortManager<SampleData>(DataList);

            // ������
            DataContext = this;
        }

        #region ʹ�÷���һ��ֱ��ʹ����չ�������Ƽ���

        private void SortById_Click(object sender, RoutedEventArgs e)
        {
            // ֱ��ʹ��ͨ��������չ����������ʵ�� ISortID
            DataList.SortBy("Id");
        }

        private void SortByName_Click(object sender, RoutedEventArgs e)
        {
            // ���ַ������������Զ�ʹ���߼�����
            DataList.SortBy("Name", descending: true);
        }

        private void SortByCreateTime_Click(object sender, RoutedEventArgs e)
        {
            // ������ʱ������
            DataList.SortBy("CreateTime");
        }

        private void SmartSort_Click(object sender, RoutedEventArgs e)
        {
            // ���������Զ���� Id��Key��Name ������
            DataList.SmartSort();
        }

        private void SortByLambda_Click(object sender, RoutedEventArgs e)
        {
            // ʹ�� Lambda ���ʽ����
            DataList.SortBy(x => x.Value, descending: true);
        }

        private void MultiSort_Click(object sender, RoutedEventArgs e)
        {
            // �༶�����Ȱ� Name���ٰ� Id
            DataList.SortByMultiple(
                ("Name", false),
                ("Id", false)
            );
        }

        #endregion

        #region ʹ�÷�������ʹ��������������߼����ܣ�

        private void ApplySort_Click(object sender, RoutedEventArgs e)
        {
            // Ӧ�����򲢼�¼����
            _sortManager.ApplySort("Name");
        }

        private void ToggleSort_Click(object sender, RoutedEventArgs e)
        {
            // �л�������
            _sortManager.ToggleSortDirection();
        }

        private void SaveSort_Click(object sender, RoutedEventArgs e)
        {
            // ���浱ǰ��������
            _sortManager.SaveSort("MySort");
        }

        private void LoadSort_Click(object sender, RoutedEventArgs e)
        {
            // ���ر������������
            _sortManager.LoadSort("MySort");
        }

        #endregion

        #region ʹ�÷�������ListView ��ǿ���ܣ���򵥣�

        private void SetupEnhancedListView()
        {
            // �������Ҫ�Զ���������й����ܣ�����ʹ�� EnhancedListView
            var enhancedListView = new EnhancedListView
            {
                EnableSorting = true,
                EnableColumnManagement = true,
                DefaultSortProperty = "Id",
                ItemsSource = DataList
            };

            // ֧�ֵ����ͷ����
            // ֧���Ҽ��˵������й��������
            // �Զ���������״̬
        }

        #endregion

        #region ʹ�÷����ģ������� GridViewColumnVisibility ����

        private void SetupGridViewColumnVisibility()
        {
            var listView = new ListView();
            var gridView = new GridView();
            
            // �����...
            
            listView.View = gridView;

            var contextMenu = new ContextMenu();
            var columnVisibilities = new ObservableCollection<GridViewColumnVisibility>();

            // ������ǿ���Ҽ��˵������������ܣ�
            GridViewColumnVisibility.GenContentMenuGridViewColumn(
                contextMenu, gridView.Columns, columnVisibilities, listView);

            listView.ContextMenu = contextMenu;

            // �����Ҽ��˵�������
            // - �Զ������п�
            // - ������������/����
            // - ����������
            // - ��ʾ/������
        }

        #endregion

        #region ���ΨһԪ��ʾ��

        private void AddUniqueItem_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new SampleData 
            { 
                Id = 1, // ��ͬ�� Id
                Name = "Duplicate Item", 
                CreateTime = DateTime.Now, 
                Value = 5.0 
            };

            // ʹ��ͨ�õ����ΨһԪ�ط���
            DataList.AddUniqueBy(newItem, x => x.Id);
            // ������ӣ���Ϊ�Ѵ��� Id = 1 ��Ԫ��
        }

        #endregion
    }
}

/* XAML ʾ��
<Window x:Class="ColorVision.UI.Examples.SortingExampleWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:sorts="clr-namespace:ColorVision.UI.Sorts"
        Title="����ʾ��" Height="600" Width="800">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        
        <!-- ��ť��� -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="10">
            <Button Content="�� ID ����" Click="SortById_Click" Margin="5"/>
            <Button Content="����������" Click="SortByName_Click" Margin="5"/>
            <Button Content="��������" Click="SmartSort_Click" Margin="5"/>
            <Button Content="�༶����" Click="MultiSort_Click" Margin="5"/>
            <Button Content="�л�����" Click="ToggleSort_Click" Margin="5"/>
        </StackPanel>
        
        <!-- ListView -->
        <ListView Grid.Row="1" ItemsSource="{Binding DataList}" Margin="10">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="ID" DisplayMemberBinding="{Binding Id}" Width="80"/>
                    <GridViewColumn Header="����" DisplayMemberBinding="{Binding Name}" Width="150"/>
                    <GridViewColumn Header="����ʱ��" DisplayMemberBinding="{Binding CreateTime}" Width="150"/>
                    <GridViewColumn Header="ֵ" DisplayMemberBinding="{Binding Value}" Width="100"/>
                </GridView>
            </ListView.View>
        </ListView>
        
        <!-- ����ʹ����ǿ�� ListView -->
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
                    <GridViewColumn Header="����" DisplayMemberBinding="{Binding Name}" Width="150"/>
                    <GridViewColumn Header="����ʱ��" DisplayMemberBinding="{Binding CreateTime}" Width="150"/>
                    <GridViewColumn Header="ֵ" DisplayMemberBinding="{Binding Value}" Width="100"/>
                </GridView>
            </sorts:EnhancedListView.View>
        </sorts:EnhancedListView>
        -->
    </Grid>
</Window>
*/