# Templates API 参考

## 目录
1. [核心接口](#核心接口)
2. [基类API](#基类api)
3. [模板控制](#模板控制)
4. [数据模型](#数据模型)
5. [UI组件](#ui组件)
6. [工具类](#工具类)

## 核心接口

### ITemplate 基类

模板系统的核心基类，所有模板都应继承此类。

#### 属性

```csharp
public class ITemplate
{
    // 模板名称（唯一标识符）
    public string Name { get; set; }
    
    // 模板代码（用于文件路径和注册）
    public string Code { get; set; }
    
    // 模板标题（显示名称）
    public virtual string Title { get; set; }
    
    // 模板数据源
    public virtual IEnumerable ItemsSource { get; }
    
    // 模板项数量
    public virtual int Count { get; }
    
    // 模板字典ID
    public int TemplateDicId { get; set; }
    
    // 初始目录（用于导入导出）
    public virtual string InitialDirectory { get; set; }
    
    // 是否隐藏侧边栏
    public bool IsSideHide { get; set; }
    
    // 保存索引列表
    public List<int> SaveIndex { get; set; }
}
```

#### 方法

##### GetValue()
获取所有模板值的枚举。

```csharp
public virtual IEnumerable GetValue()
```

**返回值**: IEnumerable - 模板值集合

**示例**:
```csharp
var template = new TemplateMTF();
template.Load();
foreach (var item in template.GetValue())
{
    Console.WriteLine(item);
}
```

##### GetValue(int index)
获取指定索引的模板值。

```csharp
public virtual object GetValue(int index)
```

**参数**:
- `index` (int): 模板项索引

**返回值**: object - 模板值对象

**异常**: NotImplementedException - 如果未实现

##### GetParamValue(int index)
获取指定索引的参数值。

```csharp
public virtual object GetParamValue(int index)
```

**参数**:
- `index` (int): 参数索引

**返回值**: object - 参数值对象

##### GetTemplateName(int index)
获取指定索引的模板名称。

```csharp
public virtual string GetTemplateName(int index)
```

**参数**:
- `index` (int): 模板索引

**返回值**: string - 模板名称

##### GetTemplateIndex(string templateName)
根据模板名称获取索引。

```csharp
public virtual int GetTemplateIndex(string templateName)
```

**参数**:
- `templateName` (string): 模板名称

**返回值**: int - 模板索引

##### Load()
从数据库加载模板数据。

```csharp
public virtual void Load()
```

**示例**:
```csharp
var template = new TemplatePOI();
template.Load();
```

##### Save()
保存模板数据到数据库。

```csharp
public virtual void Save()
```

**示例**:
```csharp
template.Save();
```

##### Import()
从文件导入模板。

```csharp
public virtual bool Import()
```

**返回值**: bool - 导入是否成功

##### ImportFile(string filePath)
从指定路径导入模板文件。

```csharp
public virtual bool ImportFile(string filePath)
```

**参数**:
- `filePath` (string): 文件路径

**返回值**: bool - 导入是否成功

##### Export(int index)
导出指定索引的模板。

```csharp
public virtual void Export(int index)
```

**参数**:
- `index` (int): 要导出的模板索引

##### CopyTo(int index)
复制指定索引的模板。

```csharp
public virtual bool CopyTo(int index)
```

**参数**:
- `index` (int): 要复制的模板索引

**返回值**: bool - 复制是否成功

##### CreateDefault()
创建默认模板对象。

```csharp
public virtual object CreateDefault()
```

**返回值**: object - 默认模板对象

##### GetTemplateNames()
获取所有模板名称列表。

```csharp
public virtual List<string> GetTemplateNames()
```

**返回值**: List<string> - 模板名称列表

##### NewCreateFileName(string fileName)
生成新的唯一文件名。

```csharp
public string NewCreateFileName(string fileName)
```

**参数**:
- `fileName` (string): 基础文件名

**返回值**: string - 唯一文件名（如果存在重复，会添加数字后缀）

**示例**:
```csharp
string uniqueName = template.NewCreateFileName("MyTemplate");
// 如果 "MyTemplate1" 已存在，返回 "MyTemplate2"
```

##### SetSaveIndex(int index)
标记索引为待保存。

```csharp
public void SetSaveIndex(int index)
```

**参数**:
- `index` (int): 要标记的索引

##### GetMysqlCommand()
获取MySQL命令对象（用于数据库操作）。

```csharp
public virtual IMysqlCommand? GetMysqlCommand()
```

**返回值**: IMysqlCommand? - MySQL命令对象，可为null

### ITemplate<T> 泛型模板类

继承自ITemplate，提供类型安全的模板实现。

```csharp
public class ITemplate<T> : ITemplate where T : ParamModBase, new()
{
    // 参数集合
    public ObservableCollection<T> Params { get; set; }
    
    // 重写ItemsSource
    public override IEnumerable ItemsSource => Params;
    
    // 重写Count
    public override int Count => Params.Count;
}
```

**类型约束**: T必须继承自ParamModBase且有无参构造函数

**示例**:
```csharp
public class TemplateMTF : ITemplate<MTFParam>
{
    public override string Title => "MTF模板";
    public string Code => "MTF";
    
    public void Load()
    {
        // 从数据库加载MTF参数
    }
}
```

## 基类API

### ParamModBase 参数模型基类

所有模板参数类的基类。

#### 属性

```csharp
public class ParamModBase : ModelBase
{
    // 主模型
    [Browsable(false)]
    public ModMasterModel ModMaster { get; set; }
    
    // 详情模型集合
    [Browsable(false)]
    public ObservableCollection<ModDetailModel> ModDetailModels { get; set; }
    
    // 创建命令
    [Browsable(false)]
    [JsonIgnore]
    public virtual RelayCommand CreateCommand { get; set; }
}
```

#### 构造函数

```csharp
// 默认构造函数
public ParamModBase()

// 从数据库模型构造
public ParamModBase(ModMasterModel modMaster, List<ModDetailModel> detail)
```

**示例**:
```csharp
public class MTFParam : ParamModBase
{
    public MTFParam() { }
    
    public MTFParam(ModMasterModel modMaster, List<ModDetailModel> detail) 
        : base(modMaster, detail) { }
    
    // 定义参数属性
    public double Gamma 
    { 
        get => GetValue(_Gamma); 
        set => SetProperty(ref _Gamma, value); 
    }
    private double _Gamma = 0.01;
}
```

### ModelBase 模型基类

提供属性绑定和数据转换功能。

#### 方法

##### GetValue<T>
获取属性值（从数据库参数字典）。

```csharp
public T? GetValue<T>(T? storage, [CallerMemberName] string propertyName = "")
```

**参数**:
- `storage` (T?): 默认存储值
- `propertyName` (string): 属性名称（自动获取）

**返回值**: T? - 属性值

**支持的类型**:
- int, uint
- string
- bool
- float, double
- Enum
- double[]

**示例**:
```csharp
public double MyValue 
{ 
    get => GetValue(_MyValue); 
    set => SetProperty(ref _MyValue, value); 
}
private double _MyValue = 1.0;
```

##### SetProperty<T>
设置属性值并触发变更通知。

```csharp
protected override bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
```

**参数**:
- `storage` (ref T): 存储字段引用
- `value` (T): 新值
- `propertyName` (string): 属性名称（自动获取）

**返回值**: bool - 是否成功设置

**副作用**:
- 更新ModDetailModel中的ValueA和ValueB
- 触发PropertyChanged事件
- 标记为已修改

##### GetParameter
获取参数详情对象。

```csharp
public ModDetailModel? GetParameter(string key)
```

**参数**:
- `key` (string): 参数键名

**返回值**: ModDetailModel? - 参数详情对象，可为null

##### GetDetail
获取所有参数详情列表。

```csharp
public void GetDetail(List<ModDetailModel> list)
```

**参数**:
- `list` (List<ModDetailModel>): 输出列表

**效果**: 将所有参数详情添加到列表中

#### 静态工具方法

##### StringToDoubleArray
将字符串转换为double数组。

```csharp
public static double[] StringToDoubleArray(string input, char separator = ',')
```

**参数**:
- `input` (string): 输入字符串
- `separator` (char): 分隔符（默认为逗号）

**返回值**: double[] - 转换后的数组，失败返回空数组

**示例**:
```csharp
double[] arr = ModelBase.StringToDoubleArray("1.0,2.5,3.7");
// arr = [1.0, 2.5, 3.7]
```

##### DoubleArrayToString
将double数组转换为字符串。

```csharp
public static string DoubleArrayToString(double[] array, char separator = ',')
```

**参数**:
- `array` (double[]): 输入数组
- `separator` (char): 分隔符（默认为逗号）

**返回值**: string - 转换后的字符串

**示例**:
```csharp
string str = ModelBase.DoubleArrayToString(new[] { 1.0, 2.5, 3.7 });
// str = "1.0,2.5,3.7"
```

## 模板控制

### TemplateControl 模板控制器

全局模板注册和管理中心。

#### 静态属性

```csharp
public class TemplateControl
{
    // 模板注册字典
    public static Dictionary<string, ITemplate> ITemplateNames { get; set; }
}
```

#### 静态方法

##### GetInstance
获取TemplateControl单例实例。

```csharp
public static TemplateControl GetInstance()
```

**返回值**: TemplateControl - 单例实例

**线程安全**: 是

##### AddITemplateInstance
注册模板实例到全局字典。

```csharp
public static void AddITemplateInstance(string code, ITemplate template)
```

**参数**:
- `code` (string): 模板代码（唯一键）
- `template` (ITemplate): 模板实例

**行为**: 如果键已存在，会覆盖原值

**示例**:
```csharp
var template = new TemplateMTF();
TemplateControl.AddITemplateInstance("MTF", template);
```

##### ExitsTemplateName
检查模板名称是否已存在。

```csharp
public static bool ExitsTemplateName(string templateName)
```

**参数**:
- `templateName` (string): 模板名称

**返回值**: bool - 名称是否已存在

**比较方式**: 大小写不敏感

**示例**:
```csharp
if (TemplateControl.ExitsTemplateName("MyTemplate"))
{
    Console.WriteLine("模板名称已存在");
}
```

##### FindDuplicateTemplate
查找包含指定名称的模板。

```csharp
public static ITemplate? FindDuplicateTemplate(string templateName)
```

**参数**:
- `templateName` (string): 模板名称

**返回值**: ITemplate? - 包含该名称的模板实例，未找到返回null

### TemplateInitializer 模板初始化器

系统启动时的模板初始化组件。

```csharp
public class TemplateInitializer : InitializerBase
{
    public override int Order => 4;
    public override string Name => nameof(TemplateInitializer);
    public override IEnumerable<string> Dependencies => new List<string>() { nameof(MySqlInitializer) };
    
    public override async Task InitializeAsync()
    {
        // 加载所有模板
    }
}
```

**初始化顺序**: 4（在MySQL初始化之后）

**依赖**: MySqlInitializer

## 数据模型

### ModMasterModel 模板主表模型

```csharp
[SugarTable("t_scgd_mod_master")]
public class ModMasterModel : EntityBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Type { get; set; }
    public string CreateDate { get; set; }
    public int SysResourceId { get; set; }
    public int Pid { get; set; }
    public string Remark { get; set; }
    public int TenantId { get; set; }
    public bool IsDelete { get; set; }
}
```

### ModDetailModel 模板详情模型

```csharp
[SugarTable("t_scgd_mod_detail")]
public class ModDetailModel : EntityBase
{
    public int Id { get; set; }
    public int ModMasterId { get; set; }
    public int SysPid { get; set; }
    public string ValueA { get; set; }
    public string ValueB { get; set; }
    public string Remark { get; set; }
}
```

### SysDictionaryModDetaiModel 系统字典模型

```csharp
[SugarTable("t_scgd_sys_dictionary_mod_detail")]
public class SysDictionaryModDetaiModel : EntityBase
{
    public int Id { get; set; }
    public string Symbol { get; set; }
    public string Name { get; set; }
    public int Type { get; set; }
    public string DefaultValue { get; set; }
}
```

## UI组件

### TemplateManagerWindow 模板管理窗口

主模板管理界面。

#### 构造函数

```csharp
public TemplateManagerWindow()
```

#### 事件

- `Window_Initialized`: 窗口初始化，加载模板列表
- `Searchbox_TextChanged`: 搜索框文本变化，执行搜索
- `ListView2_SelectionChanged`: 模板选择变化
- `ListView2_PreviewMouseDoubleClick`: 双击打开编辑窗口

### TemplateEditorWindow 模板编辑窗口

模板编辑主界面。

#### 构造函数

```csharp
public TemplateEditorWindow(ITemplate template, int defaultIndex = 0)
```

**参数**:
- `template` (ITemplate): 要编辑的模板实例
- `defaultIndex` (int): 默认选中的索引（-1表示不选中）

#### 命令绑定

```csharp
// 新建 (Ctrl+N)
ApplicationCommands.New

// 复制 (Ctrl+C)
ApplicationCommands.Copy

// 保存 (Ctrl+S)
ApplicationCommands.Save

// 删除 (Delete)
ApplicationCommands.Delete

// 重命名
Commands.ReName
```

#### 方法

```csharp
// 创建新模板
private void New()

// 复制模板
private void CreateCopy()

// 删除模板
private void Delete()

// 重命名模板
private void ReName()

// 导入模板
private void Import_Click(object sender, RoutedEventArgs e)

// 导出模板
private void Export_Click(object sender, RoutedEventArgs e)
```

### TemplateCreate 模板创建窗口

创建或导入新模板。

#### 构造函数

```csharp
public TemplateCreate(ITemplate template, bool isImport = false)
```

**参数**:
- `template` (ITemplate): 模板类型
- `isImport` (bool): 是否为导入模式

#### 属性

```csharp
public ITemplate ITemplate { get; set; }
public string CreateName { get; set; }
private string TemplateFile { get; set; }
```

#### 方法

```csharp
// 创建模板卡片
private RadioButton CreateTemplateCard(string title, string description, bool isChecked)

// 确认创建
private void Button_Click(object sender, RoutedEventArgs e)

// 键盘事件处理
private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
```

## 工具类

### SearchProvider 搜索提供者

```csharp
public interface ISearch
{
    string Header { get; }
    string GuidId { get; }
}

public class SearchProvider : ISearch
{
    public string Header { get; set; }
    public string GuidId { get; set; }
    public ICommand Command { get; set; }
}
```

**用途**: 为模板管理窗口提供搜索功能

**搜索字段**: Header（标题）、GuidId（GUID）

### SymbolCache 符号缓存

```csharp
public class SymbolCache
{
    public static SymbolCache Instance { get; set; }
    public ConcurrentDictionary<int, SysDictionaryModDetaiModel> Cache { get; set; }
}
```

**用途**: 缓存系统字典数据，提高参数解析性能

**线程安全**: 是（使用ConcurrentDictionary）

## 使用示例

### 创建新模板类型

```csharp
// 1. 定义参数类
public class MyAlgorithmParam : ParamModBase
{
    public MyAlgorithmParam() { }
    
    public MyAlgorithmParam(ModMasterModel modMaster, List<ModDetailModel> detail) 
        : base(modMaster, detail) { }
    
    [Category("算法参数")]
    [Description("阈值")]
    public double Threshold 
    { 
        get => GetValue(_Threshold); 
        set => SetProperty(ref _Threshold, value); 
    }
    private double _Threshold = 0.5;
    
    [Category("算法参数")]
    [Description("迭代次数")]
    public int Iterations 
    { 
        get => GetValue(_Iterations); 
        set => SetProperty(ref _Iterations, value); 
    }
    private int _Iterations = 100;
}

// 2. 创建模板类
public class TemplateMyAlgorithm : ITemplate<MyAlgorithmParam>, IITemplateLoad
{
    public override string Title => "我的算法";
    public string Code => "MyAlg";
    
    public void Load()
    {
        // 从数据库加载
        var items = Db.Queryable<ModMasterModel>()
            .Where(a => a.Type == 123)  // 你的类型ID
            .ToList();
            
        Params = new ObservableCollection<MyAlgorithmParam>();
        foreach (var item in items)
        {
            var details = Db.Queryable<ModDetailModel>()
                .Where(d => d.ModMasterId == item.Id)
                .ToList();
            Params.Add(new MyAlgorithmParam(item, details));
        }
    }
    
    public override void Save()
    {
        foreach (var param in Params)
        {
            // 保存逻辑
        }
    }
}

// 3. 创建菜单项
public class MenuMyAlgorithm : MenuITemplateAlgorithmBase
{
    public override string Header => "我的算法";
    public override int Order => 9999;
    public override ITemplate Template => new TemplateMyAlgorithm();
}
```

### 使用模板

```csharp
// 获取模板实例
var template = new TemplateMyAlgorithm();
template.Load();

// 访问参数
var firstParam = template.Params[0];
Console.WriteLine($"阈值: {firstParam.Threshold}");
Console.WriteLine($"迭代次数: {firstParam.Iterations}");

// 修改参数
firstParam.Threshold = 0.75;
firstParam.Iterations = 200;

// 保存更改
template.Save();

// 导出模板
template.Export(0);

// 导入模板
if (template.Import())
{
    Console.WriteLine("导入成功");
}
```

### 自定义UI控件

```csharp
// 1. 创建UserControl
public partial class MyAlgorithmEditor : UserControl
{
    public MyAlgorithmParam Param { get; set; }
    
    public MyAlgorithmEditor()
    {
        InitializeComponent();
    }
    
    private void UserControl_Initialized(object sender, EventArgs e)
    {
        DataContext = Param;
    }
}

// 2. 在模板类中指定自定义控件
public class TemplateMyAlgorithm : ITemplate<MyAlgorithmParam>
{
    public override UserControl GetCustomControl()
    {
        return new MyAlgorithmEditor { Param = CurrentParam };
    }
}
```

## 最佳实践

### 参数定义
1. 使用有意义的属性名称
2. 添加Category和Description特性
3. 为数值参数设置合理的默认值
4. 使用枚举代替魔法数字

### 性能优化
1. 缓存频繁访问的数据（使用SymbolCache）
2. 批量操作时使用事务
3. 避免在UI线程执行长时间操作

### 错误处理
1. 在Import/Export方法中捕获IO异常
2. 验证参数范围
3. 提供清晰的错误消息

## 相关资源

- [Templates架构设计](./Templates架构设计.md)
- [模板管理文档](../template-management/模板管理.md)
- [ColorVision.Engine组件文档](../../engine-components/ColorVision.Engine.md)
