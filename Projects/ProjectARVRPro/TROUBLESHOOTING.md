# ProjectARVRPro 故障排查指南

## 目录

1. [常见问题快速索引](#常见问题快速索引)
2. [启动和初始化问题](#启动和初始化问题)
3. [设备连接问题](#设备连接问题)
4. [测试执行问题](#测试执行问题)
5. [结果和数据问题](#结果和数据问题)
6. [性能问题](#性能问题)
7. [配置问题](#配置问题)
8. [日志和诊断](#日志和诊断)

---

## 常见问题快速索引

| 问题症状 | 可能原因 | 快速解决 | 详细说明 |
|---------|---------|---------|---------|
| 程序无法启动 | 缺少.NET运行时 | 安装.NET 8.0 | [链接](#程序无法启动) |
| 设备连接失败 | MQTT服务未运行 | 启动MQTT服务 | [链接](#设备连接失败) |
| 测试立即完成 | 所有步骤被禁用 | 启用至少一个步骤 | [链接](#测试立即完成没有执行) |
| 测试卡住不动 | 设备无响应 | 检查设备状态 | [链接](#测试卡住不动) |
| 结果不准确 | 参数配置错误 | 检查Recipe配置 | [链接](#测试结果不准确) |
| 内存占用过高 | 资源未释放 | 重启程序 | [链接](#内存占用过高) |
| 导出失败 | 权限不足 | 以管理员运行 | [链接](#导出结果失败) |

---

## 启动和初始化问题

### 程序无法启动

**症状**：双击exe文件没有反应，或显示错误对话框

**可能原因**：
1. 缺少.NET 8.0运行时
2. 系统不兼容（不是Windows 10/11）
3. 文件损坏或不完整

**解决方法**：

#### 步骤1: 检查.NET运行时
```powershell
# 在PowerShell中执行
dotnet --list-runtimes
```

查看是否包含：
```
Microsoft.WindowsDesktop.App 8.0.x
```

如果没有，下载安装：
https://dotnet.microsoft.com/download/dotnet/8.0

#### 步骤2: 检查系统兼容性
- 确认操作系统为Windows 10/11 64位
- 检查系统更新是否安装

#### 步骤3: 重新安装
1. 完全卸载现有版本
2. 下载最新安装包
3. 以管理员权限安装

### 启动后界面异常

**症状**：窗口显示不完整、控件错位、主题异常

**解决方法**：

1. **重置窗口配置**
   - 删除配置文件：`%AppData%\ColorVision\ProjectARVRPro\Settings.json`
   - 重新启动程序

2. **检查显示设置**
   - Windows显示缩放：推荐100%或125%
   - 分辨率：至少1920x1080

3. **更新显卡驱动**
   - 访问显卡厂商网站
   - 下载并安装最新驱动

### 初始化失败

**症状**：程序启动后显示初始化失败错误

**解决方法**：

1. **检查配置文件**
   ```
   配置目录：%AppData%\ColorVision\ProjectARVRPro\
   
   关键文件：
   - ProcessMetas.json
   - RecipeConfig.json
   - FixConfig.json
   ```

2. **重置配置**
   - 备份现有配置
   - 删除配置文件
   - 重启程序使用默认配置

3. **查看错误日志**
   ```
   日志位置：程序目录\Logs\
   文件：ColorVision_YYYYMMDD.log
   ```

---

## 设备连接问题

### 设备连接失败

**症状**：设备管理器显示设备离线或连接失败

**诊断步骤**：

#### 1. 检查MQTT服务
```powershell
# 检查MQTT服务是否运行
Get-Service | Where-Object {$_.Name -like "*mqtt*"}
```

如果未运行：
```powershell
# 启动MQTT服务（根据实际服务名）
Start-Service MosquittoService
```

#### 2. 检查网络连接
```powershell
# 测试MQTT服务器连接
Test-NetConnection -ComputerName localhost -Port 1883
```

预期输出：
```
TcpTestSucceeded : True
```

#### 3. 检查防火墙
- 打开Windows防火墙设置
- 确保允许程序通过防火墙
- 添加例外规则（如需要）

#### 4. 验证设备配置
在设备管理器中：
1. 检查设备IP地址
2. 验证端口号
3. 测试设备响应

### 设备频繁断连

**症状**：设备连接不稳定，频繁掉线

**解决方法**：

1. **优化MQTT配置**
   ```csharp
   // 增加KeepAlive时间
   KeepAlivePeriod = TimeSpan.FromSeconds(120)
   
   // 增加超时时间
   CommunicationTimeout = TimeSpan.FromSeconds(30)
   ```

2. **检查网络质量**
   - 使用有线连接替代WiFi
   - 减少网络负载
   - 检查网络设备状态

3. **更新设备固件**
   - 联系设备厂商
   - 获取最新固件
   - 按说明更新

---

## 测试执行问题

### 测试立即完成，没有执行

**症状**：点击开始测试后，立即显示完成，没有执行任何步骤

**原因**：所有ProcessMeta被禁用

**解决方法**：

1. **打开流程管理器**
   ```
   工具 → 流程管理
   ```

2. **检查IsEnabled状态**
   - 查看"是否启用"列
   - 至少启用一个步骤

3. **手动启用步骤**
   ```csharp
   // 如果需要代码修复
   ProcessManager.GetInstance().ProcessMetas[0].IsEnabled = true;
   ```

### 测试卡住不动

**症状**：测试执行到某一步后不再继续

**诊断步骤**：

#### 1. 查看当前状态
- 检查StepBar当前高亮步骤
- 查看状态栏信息
- 观察日志输出

#### 2. 检查设备响应
```
设备 → 设备管理 → 查看设备状态
```

#### 3. 查看日志
```
窗口 → 日志查看器
筛选条件：最近5分钟，级别=Error
```

#### 4. 处理方法

**如果是设备无响应**：
1. 重启设备
2. 检查设备连接
3. 重新执行测试

**如果是程序卡死**：
1. 等待超时（默认60秒）
2. 如果长时间无响应，强制停止测试
3. 检查日志找出原因

### 某些步骤被跳过

**症状**：部分配置的测试步骤没有执行

**原因**：IsEnabled设置为false

**验证方法**：

1. 打开流程管理器
2. 检查被跳过步骤的"是否启用"复选框
3. 勾选需要执行的步骤
4. 重新执行测试

### 测试结果不准确

**症状**：测试数据异常、偏差过大

**排查步骤**：

#### 1. 检查Recipe配置
```
流程管理器 → 选择步骤 → 编辑Recipe
```

**关键参数检查**：
- ROI区域是否正确
- Gamma值是否合适
- 阈值设置是否合理

#### 2. 验证测试环境
- 环境温度：20-25°C
- 环境湿度：40-60%
- 光源稳定性
- 避免外界干扰

#### 3. 设备校准
- 执行设备校准程序
- 检查校准数据有效性
- 定期重新校准（建议每月一次）

#### 4. 对比历史数据
- 查询同类设备历史测试数据
- 分析数据趋势
- 识别异常偏差

---

## 结果和数据问题

### 查询不到历史结果

**症状**：在结果查询窗口找不到之前的测试记录

**排查步骤**：

#### 1. 检查数据库连接
```
设置 → 数据库配置 → 测试连接
```

#### 2. 验证查询条件
- 日期范围是否正确
- 批次号是否匹配
- 筛选条件是否过严

#### 3. 检查数据库
```sql
-- 直接查询数据库
SELECT COUNT(*) FROM t_scgd_algorithm_result_detail_mtf;
```

#### 4. 恢复数据
如果数据丢失：
1. 检查数据库备份
2. 联系管理员恢复
3. 查看导出的CSV/PDF文件

### 导出结果失败

**症状**：点击导出时报错或无反应

**常见原因和解决**：

#### 1. 权限问题
```
解决方法：
1. 以管理员身份运行程序
2. 选择有写权限的目录
3. 检查文件是否被占用
```

#### 2. 磁盘空间不足
```powershell
# 检查可用空间
Get-PSDrive C | Select-Object Used,Free
```

#### 3. 文件名非法字符
```
避免使用：\ / : * ? " < > |
推荐格式：TestResult_20251209_001.csv
```

#### 4. 数据过大
```
解决方法：
1. 分批导出
2. 压缩导出
3. 清理不必要的数据
```

### 结果显示乱码

**症状**：导出的CSV文件打开后显示乱码

**解决方法**：

#### 用Excel打开
1. 打开Excel
2. 数据 → 从文本/CSV
3. 选择文件编码为UTF-8
4. 点击加载

#### 用记事本转换
1. 用记事本打开CSV
2. 文件 → 另存为
3. 编码选择ANSI
4. 保存后用Excel打开

---

## 性能问题

### 测试速度慢

**症状**：完整测试需要很长时间

**优化方法**：

#### 1. 使用IsEnabled精简流程
```
只启用必要的测试步骤
预计提速：50-70%
```

#### 2. 优化ROI区域
```csharp
// 减小ROI区域
RoiWidth = 1920;   // 从3840减小
RoiHeight = 1080;  // 从2160减小
预计提速：300%
```

#### 3. 调整采样率
```csharp
SamplingRate = 0.5;  // 从1.0降低
预计提速：100%
精度损失：<5%
```

详细优化方案参考：[性能优化指南](PERFORMANCE_OPTIMIZATION.md)

### 内存占用过高

**症状**：程序运行时内存使用超过2GB

**解决方法**：

#### 1. 定期重启程序
```
建议：每执行50-100次测试后重启
```

#### 2. 清理缓存
```csharp
// 在测试间隙执行
ImageCache.Clear();
GC.Collect();
```

#### 3. 限制历史记录
```
设置 → 数据保留策略
只保留最近30天的数据
```

### CPU占用率过高

**症状**：程序运行时CPU占用接近100%

**检查项目**：

1. **并发测试数量**
   - 减少同时运行的测试
   - 建议单线程执行

2. **后台任务**
   - 关闭不必要的后台程序
   - 暂停系统更新

3. **算法优化**
   - 使用CUDA加速（如可用）
   - 降低图像分辨率

---

## 配置问题

### 配置丢失或重置

**症状**：程序重启后配置恢复为默认值

**原因分析**：
1. 配置文件损坏
2. 保存失败
3. 权限问题

**恢复方法**：

#### 1. 从备份恢复
```
备份位置：
%AppData%\ColorVision\ProjectARVRPro\Backup\
```

#### 2. 导入配置
```
流程管理器 → 导入配置 → 选择备份文件
```

#### 3. 手动修复配置文件
```json
// ProcessMetas.json示例
[
  {
    "Name": "White255测试",
    "FlowTemplate": "White255Flow",
    "ProcessTypeFullName": "ProjectARVRPro.Process.W255.White255Process",
    "IsEnabled": true
  }
]
```

### 配置无法保存

**症状**：修改配置后无法保存或保存后无效

**解决方法**：

#### 1. 检查文件权限
```powershell
# 查看文件属性
Get-Acl "%AppData%\ColorVision\ProjectARVRPro\ProcessMetas.json"
```

#### 2. 以管理员运行
```
右键exe → 以管理员身份运行
```

#### 3. 检查文件是否只读
```
文件属性 → 取消只读勾选
```

---

## 日志和诊断

### 查看日志

#### 日志位置
```
主日志：程序目录\Logs\ColorVision_YYYYMMDD.log
错误日志：程序目录\Logs\Error\Error_YYYYMMDD.log
```

#### 通过UI查看
```
窗口 → 日志查看器
```

### 日志级别

```xml
<!-- log4net.config -->
<root>
  <!-- DEBUG: 最详细，用于开发调试 -->
  <level value="DEBUG" />
  
  <!-- INFO: 正常信息，用于生产环境 -->
  <!-- <level value="INFO" /> -->
  
  <!-- WARN: 警告信息 -->
  <!-- <level value="WARN" /> -->
  
  <!-- ERROR: 错误信息 -->
  <!-- <level value="ERROR" /> -->
</root>
```

### 常见错误信息

#### "MQTT connection failed"
```
原因：MQTT服务未运行
解决：启动MQTT服务
```

#### "Database connection timeout"
```
原因：数据库连接超时
解决：检查数据库服务，增加超时时间
```

#### "Image processing failed"
```
原因：图像处理错误
解决：检查图像文件完整性，验证参数配置
```

#### "Device not responding"
```
原因：设备无响应
解决：检查设备连接，重启设备
```

### 启用详细日志

```csharp
// 临时启用调试日志
log4net.Repository.ILoggerRepository repository = 
    LogManager.GetRepository();
repository.Threshold = log4net.Core.Level.Debug;
```

### 收集诊断信息

当需要技术支持时，收集以下信息：

1. **系统信息**
   ```powershell
   systeminfo > system_info.txt
   ```

2. **错误日志**
   ```
   Logs\Error\Error_YYYYMMDD.log
   ```

3. **配置文件**
   ```
   %AppData%\ColorVision\ProjectARVRPro\
   ```

4. **屏幕截图**
   - 错误对话框
   - 日志查看器
   - 配置界面

5. **复现步骤**
   - 详细操作步骤
   - 预期结果
   - 实际结果

---

## 获取帮助

### 自助资源

1. **项目文档**
   - [README.md](README.md)
   - [快速入门](GETTING_STARTED.md)
   - [性能优化](PERFORMANCE_OPTIMIZATION.md)

2. **在线文档**
   - https://xincheng213618.github.io/scgd_general_wpf/

3. **GitHub仓库**
   - https://github.com/xincheng213618/scgd_general_wpf
   - 查看Issues
   - 提交Bug报告

### 联系技术支持

**准备信息**：
- 问题详细描述
- 复现步骤
- 日志文件
- 系统信息
- 配置文件

**联系方式**：
- Email: support@colorvision.com
- GitHub Issues: https://github.com/xincheng213618/scgd_general_wpf/issues

---

*本指南最后更新：2025年12月*
