# ChangelogWindow 更新日志窗口

## 概述

ChangelogWindow 提供了一个直观的界面来浏览软件的版本历史和更新记录。窗口采用双面板设计，左侧为版本树形导航，右侧为详细的更新内容。

## 架构设计

### 三层版本组织结构

ChangelogWindow 使用分层树形结构组织版本信息，使得浏览 300+ 版本记录更加高效：

```
主版本组 (Major Version Group)
└── 1.x.x.x (包含所有 1.x 系列版本)
    ├── 次版本组 (Minor Version Group)
    │   └── 1.3.x.x (包含所有 1.3 系列版本)
    │       ├── 具体版本 (Individual Entries)
    │       │   ├── 1.3.13.1
    │       │   ├── 1.3.12.1
    │       │   └── ...
    │   └── 1.2.x.x
    │       └── ...
    └── 1.1.x.x
        └── ...
```

### 数据模型

#### VersionTreeNode (基础节点)
```csharp
public abstract class VersionTreeNode : ViewModelBase
{
    public string DisplayName { get; set; }      // 显示名称
    public bool IsExpanded { get; set; }         // 展开/折叠状态
    public bool IsSelected { get; set; }         // 选中状态
}
```

#### MajorVersionNode (主版本节点)
```csharp
public class MajorVersionNode : VersionTreeNode
{
    public int MajorVersion { get; set; }
    public ObservableCollection\<MinorVersionNode\> MinorVersions { get; set; }
}
```

示例显示: `1.x.x.x (150 versions)`

#### MinorVersionNode (次版本节点)
```csharp
public class MinorVersionNode : VersionTreeNode
{
    public int MajorVersion { get; set; }
    public int MinorVersion { get; set; }
    public ObservableCollection\<ChangeLogEntry\> Entries { get; set; }
}
```

示例显示: `1.3.x.x (45 versions)`

#### ChangeLogEntry (版本条目)
```csharp
public class ChangeLogEntry : ViewModelBase
{
    public string Version { get; set; }           // 版本号 (如: 1.3.13.1)
    public DateTime ReleaseDate { get; set; }     // 发布日期
    public List\\<string\> Changes { get; set; }     // 变更列表
    public string ChangeLog { get; }              // 格式化的变更日志
    public bool IsSelected { get; set; }          // 选中状态
}
```

## 主要功能

### 1. 层级导航

**优势：**
- 将 300+ 版本按主版本和次版本分组
- 默认展开最新的主版本，其他折叠
- 减少界面混乱，提高查找效率

**使用方式：**
- 点击版本组旁的展开/折叠按钮
- 最新主版本默认展开，便于快速访问最新更新

### 2. 双向选择同步

左侧树形视图与右侧详情面板保持同步：

```csharp
// 从TreeView选择 -> 同步到详情面板
private void ChangeLogListView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs\<object\> e)
{
    if (treeView.SelectedItem is ChangeLogEntry selectedEntry)
    {
        ChangeLogDetailsPanel.SelectedItem = selectedEntry;
        ChangeLogDetailsPanel.ScrollIntoView(selectedEntry);
    }
}

// 从详情面板选择 -> 同步到TreeView
private void ChangeLogDetailsPanel_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (listView.SelectedItem is ChangeLogEntry selectedEntry)
    {
        SelectEntryInTreeView(selectedEntry);  // 自动展开父节点并定位
    }
}
```

### 3. 智能搜索

搜索功能支持在版本号、发布日期和变更内容中查找：

**搜索流程：**
1. 输入关键词后自动防抖（300ms延迟）
2. 筛选匹配的版本条目
3. 重建树形结构，仅显示匹配结果
4. 自动展开所有匹配项的父节点

**支持的搜索内容：**
- 版本号: `1.3.13`
- 日期: `2025-10-09` 或 `2025.10.09`
- 变更内容: `修复`, `Bug`, `优化`
- 多关键词: `1.3 修复` (AND 逻辑)

**实现代码：**
```csharp
private async void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
{
    await Task.Delay(SearchDebounceMs, token);  // 防抖
    
    var filteredResults = ChangeLogEntrys.Where(entry => 
        keywords.All(keyword =>
            entry.Version.Contains(keyword, OrdinalIgnoreCase) ||
            entry.ReleaseDate.ToString("yyyy-MM-dd").Contains(keyword) ||
            entry.ChangeLog.Contains(keyword, OrdinalIgnoreCase)
        ));
    
    BuildVersionTree();  // 重建树形结构
    ExpandAllSearchResults();  // 展开所有匹配项
}
```

### 4. 版本更新

点击版本条目的更新命令可以：
- **升级**: 安装更高版本
- **回退**: 提示用户回退流程
- **当前版本**: 显示版本相同提示

## UI 组件

### TreeView (左侧面板)

```xaml
<TreeView x:Name="ChangeLogListView" Grid.Column="0">
    \<TreeView.Resources\>
        <!-- 主版本节点模板 -->
        <HierarchicalDataTemplate DataType="{x:Type local:MajorVersionNode}" 
                                  ItemsSource="{Binding MinorVersions}">
            <TextBlock Text="{Binding DisplayName}" FontWeight="Bold"/>
        </HierarchicalDataTemplate>
        
        <!-- 次版本节点模板 -->
        <HierarchicalDataTemplate DataType="{x:Type local:MinorVersionNode}" 
                                  ItemsSource="{Binding Entries}">
            <TextBlock Text="{Binding DisplayName}" FontWeight="SemiBold"/>
        </HierarchicalDataTemplate>
        
        <!-- 具体版本条目模板 -->
        <DataTemplate DataType="{x:Type local:ChangeLogEntry}">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Version}" Width="80"/>
                <TextBlock Text="{Binding ReleaseDate, StringFormat='yyyy/MM/dd'}" Width="100"/>
            </StackPanel>
        </DataTemplate>
    </TreeView.Resources>
</TreeView>
```

### ListView (右侧详情面板)

```xaml
<ListView x:Name="ChangeLogDetailsPanel" Grid.Column="1">
    \<ListView.ItemTemplate\>
        <DataTemplate DataType="{x:Type local:ChangeLogEntry}">
            <StackPanel Margin="0,0,0,20">
                <TextBox Text="{Binding Version, StringFormat='## {0}'}" 
                         FontSize="18" FontWeight="Bold"/>
                <TextBox Text="{Binding ReleaseDate, StringFormat='yyyy/MM/dd'}" 
                         FontSize="12" Foreground="Gray"/>
                <TextBox Text="{Binding ChangeLog}" TextWrapping="Wrap"/>
            </StackPanel>
        </DataTemplate>
    </ListView.ItemTemplate>
</ListView>
```

## 关键算法

### BuildVersionTree - 构建版本树

将平面的版本列表转换为层级树形结构：

```csharp
private void BuildVersionTree()
{
    VersionTree.Clear();
    
    // 1. 按主版本分组
    var majorGroups = ChangeLogEntrys.GroupBy(entry => {
        var versionParts = entry.Version.Split('.');
        return int.Parse(versionParts[0]);
    }).OrderByDescending(g => g.Key);
    
    // 2. 为每个主版本创建节点
    foreach (var majorGroup in majorGroups)
    {
        var majorNode = new MajorVersionNode {
            DisplayName = $"{majorGroup.Key}.x.x.x ({majorGroup.Count()} versions)",
            IsExpanded = (majorGroup.Key == latestMajor)  // 最新主版本默认展开
        };
        
        // 3. 按次版本分组
        var minorGroups = majorGroup.GroupBy(entry => {
            var versionParts = entry.Version.Split('.');
            return int.Parse(versionParts[1]);
        }).OrderByDescending(g => g.Key);
        
        // 4. 为每个次版本创建节点并添加条目
        foreach (var minorGroup in minorGroups)
        {
            var minorNode = new MinorVersionNode {
                DisplayName = $"{majorGroup.Key}.{minorGroup.Key}.x.x ({minorGroup.Count()} versions)"
            };
            
            foreach (var entry in minorGroup.OrderByDescending(e => e.Version))
            {
                minorNode.Entries.Add(entry);
            }
            
            majorNode.MinorVersions.Add(minorNode);
        }
        
        VersionTree.Add(majorNode);
    }
}
```

**时间复杂度:** O(n log n) - 由于排序操作
**空间复杂度:** O(n) - 存储所有版本节点

### SelectEntryInTreeView - 树形定位

在树形结构中查找并选中指定版本：

```csharp
private void SelectEntryInTreeView(ChangeLogEntry entry)
{
    foreach (var majorNode in VersionTree)
    {
        foreach (var minorNode in majorNode.MinorVersions)
        {
            var foundEntry = minorNode.Entries.FirstOrDefault(e => e.Version == entry.Version);
            if (foundEntry != null)
            {
                // 展开父节点
                majorNode.IsExpanded = true;
                minorNode.IsExpanded = true;
                
                // 选中条目
                foundEntry.IsSelected = true;
                
                // 滚动到可见区域
                ScrollTreeViewItemIntoView(foundEntry);
                return;
            }
        }
    }
}
```

## 性能优化

### 1. 虚拟化支持

右侧详情面板使用虚拟化以提高大量条目的滚动性能：

```xaml
\<ListView.ItemsPanel\>
    \<ItemsPanelTemplate\>
        <VirtualizingStackPanel IsVirtualizing="True" VirtualizationMode="Recycling"/>
    </ItemsPanelTemplate>
</ListView.ItemsPanel>
```

### 2. 搜索防抖

避免频繁的搜索操作影响性能：

```csharp
private const int SearchDebounceMs = 300;
private CancellationTokenSource _searchCts;

private async void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
{
    _searchCts?.Cancel();
    _searchCts = new CancellationTokenSource();
    await Task.Delay(SearchDebounceMs, _searchCts.Token);
    // 执行搜索...
}
```

### 3. 延迟加载

- 主版本节点默认展开最新版本
- 其他版本组保持折叠状态
- 减少初始渲染负担

## 与旧版本的对比

### 旧实现 (ListView)

```
优点:
- 简单直接
- 适合少量版本

缺点:
- 300+ 版本滚动体验差
- 无层级组织
- 难以快速定位特定版本系列
```

### 新实现 (TreeView)

```
优点:
- 层级清晰，便于导航
- 支持展开/折叠，减少视觉混乱
- 分组计数，一目了然
- 搜索结果自动展开相关节点
- 适合大量版本管理

缺点:
- 实现相对复杂
- 需要额外的数据结构
```

## 使用示例

### 快速查找最新版本
1. 打开 ChangelogWindow
2. 最新主版本默认展开
3. 展开最新的次版本组
4. 点击最新版本查看详情

### 搜索特定功能更新
1. 在搜索框输入关键词，如 "图像编辑器"
2. 系统自动筛选包含该关键词的版本
3. 所有匹配项的父节点自动展开
4. 点击感兴趣的版本查看详情

### 浏览特定版本系列
1. 展开目标主版本组 (如 1.x.x.x)
2. 展开目标次版本组 (如 1.3.x.x)
3. 浏览该系列下的所有版本

## 技术栈

- **UI Framework**: WPF (Windows Presentation Foundation)
- **Data Binding**: MVVM 模式
- **Collections**: ObservableCollection (支持双向绑定)
- **Async/Await**: 异步搜索防抖
- **LINQ**: 数据查询和分组

## 相关文件

```
ColorVision/Update/ChangeLog/
├── ChangelogWindow.xaml           # UI 定义
├── ChangelogWindow.xaml.cs        # 窗口逻辑
├── ChangeLogEntry.cs              # 版本条目模型
├── VersionGroupNode.cs            # 版本树节点模型
└── MenuChangeLog.cs               # 菜单集成
```

## 未来优化方向

1. **版本比较**: 支持选择两个版本进行对比
2. **导出功能**: 导出特定版本范围的更新日志
3. **标签过滤**: 按更新类型过滤 (功能/修复/优化等)
4. **收藏功能**: 标记重要版本便于快速访问
5. **统计图表**: 展示版本发布频率趋势

---

*文档版本: 1.0*  
*最后更新: 2025-01-10*  
*作者: ColorVision Team*
