# **键盘测试**

用户可以在主程序中配置关注点和关注点相关的参数信息，然后导入到KB模板中。

流程的配置过程，添加一个取图的相机模块，然后配置KB模块，设置KB模板，根据相机图像和KB模板获取图像后，计算KB的灰度值，然后通过校正获取亮度值。
在计算完成流程后，根据KB模板的设定，将计算结果与KB模板的设定值进行比较，然后显示结果和图像信息，然后生成CSV和报告。

## SPEC：

最小亮度（MiniLv）
最大亮度（MaxLv）
平均亮度（Avg Lv）
亮度一致性（Uniformity）

结果根据SPEC, 平均亮度大于最小亮度，小于最大亮度，平均亮度大于预设， 亮度一致性大于预设，即判定成功，否则失败。

数据会写入预设的CSV中，然后生成报告，保存到用户指定的路径中。

**CSV格式：**

"Id","Model", "SerialNumber", "POISet", "AvgLv", "MinLv", "MaxLv", "LvUniformity","DarkestKey", "BrightestKey", "ColorDifference", "NbrFailedPts", "LvFailures",  "LocalContrastFailures", "DarkKeyLocalContrast", "BrightKeyLocalContrast",  "LocalDarkestKey", "LocalBrightestKey", "StrayLight", "Result", "DateTime", ....., "LimitProfile",            "MinKeyLv", "MaxKeyLv", "MinAvgLv", "MaxAvgLv", "MinLvUniformity", "MaxDarkLocalContrast", "MaxBrightLocalContrast", "MaxNbrFailedPoints", "MaxColorDifference", "MaxStrayLight", "MinInterKeyUniformity","MinInterKeyColorUniformity"。



## **Modbus配置**

按照要求，程序可以手动执行，也可以根据Modbus配置触发执行。

默认配置modbus 192.168.6.1 端口502, 寄存器地址 D0, 即 0号

默认modbus 会在UI启动时自动尝试连接，连接后会在状态栏中显示是否已经连接。



## 其他

Key：字符
Halo：光晕
键帽：字符+光源等整体

最小亮度：
1.单独测量Key或者Halo，所有key或者Halo中最小值。
2.Key与Halo一起测量的时候，键帽整体最小值

亮度一致性：
Uniformity=min/max

扫码枪

Gryphon GFS4470

需要下载驱动，并安装 Datalogic  Aladdin，配置为键盘模式 。默认是串口模式。需要监听串口返回信息。

序列号格式
DF241202D1MMD4J9F
DF241202D1MMD4J9K





