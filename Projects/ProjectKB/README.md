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



## FunTestDll.dll


> PID DLL说明

1.  function CheckVersion(str: pchar): pchar; stdcall;

> 参数说明：str表示条码。
>
> 说明：此接口的功能是检查系统版本号。若正确，则返回"N"，否则返回"发现程式新版本，请更新后作业！"。

2.  function CheckOPNO(OPNO: pchar): pchar; stdcall;

> 参数说明：OPNO表示工号。
>
> 说明：此接口的功能是检查工号是否正确。
>
> 如果此工号在MES系统中没有权限，则提示"此账号\[\' + OPNO +
> \'\]不存在!"；
>
> 否则返回"N"。

3.  function ATE_test(str: PChar): PChar; stdcall;

> 参数说明：str表示条码。Collect_test中stage应为"ATE"。
>
> 说明：此接口的功能是检查条码当前状态是否是ATE测试。

4.  function AOI_test(str: PChar): PChar; stdcall;

> 参数说明：str表示条码。Collect_test中stage应为"AOI"。
>
> 说明：此接口的功能是检查条码当前状态是否是AOI测试。

5.  function DARK_test(str: PChar): PChar; stdcall;

> 参数说明：str表示条码。Collect_test中stage应为"DARK"。
>
> 说明：此接口的功能是检查条码当前状态是否是光箱测试。

6.  function Fun2Test_test(str: PChar): PChar; stdcall;

> 参数说明：str表示条码。Collect_test中stage应为"FUN2"。
>
> 说明：此接口的功能是检查条码当前状态是否是功能刷键测试。

7.  function KM_FEELING_test (str: PChar): PChar; stdcall;

> 参数说明：str表示条码。Collect_test中stage应为"KM_FEELING_TEST"。
>
> 说明：此接口的功能是检查条码当前状态是否是KM Feeling测试。

8.  function Sens_test(str: PChar): PChar; stdcall;

> 参数说明：str表示条码。Collect_test中stage应为"SENSTEST"。
>
> 说明：此接口的功能是检查条码当前状态是否是灵敏测试。

9.  function Resistance_test(str: PChar): PChar; stdcall;

> 参数说明：str表示条码。Collect_test中stage应为"RESISTANCE"。
>
> 说明：此接口的功能是检查条码当前状态是否是KM 测阻值测试。

10. function PLG_test(str: PChar): PChar; stdcall;

> 参数说明：str表示条码。PLG_test中stage应为"PLG"。
>
> 说明：此接口的功能是检查条码当前状态是否是KM PLG检验。

11. function Mouse_test(str: PChar): PChar; stdcall;

> 参数说明：str表示条码。Collect_test中stage应为"MOUSE"。
>
> 说明：此接口的功能是检查条码当前状态是否是MOUSE测试。

12. function Fun3Test_test(str: PChar): PChar; stdcall;

> 参数说明：str表示条码。Collect_test中stage应为"Fun3"。
>
> 说明：此接口的功能是检查条码当前状态是否是Fun3测试。
>
> 1.3-1.11的返回值如下：
>
> 如果条码还未归属过工单，则提示"此条码还没有归属工单，请先归属工单"；
>
> 如果条码所在的线别已达到不良率停线警告值值，则提示"XX站XX线不良率超出设置的停线值强制停线,请先解锁！"；
>
> 如条码所在的线别有重大不良（某站检到特定的不良现象）则提示"XX站XX线重大不良：XXX停线,请先解锁！"；
>
> 如果条码AOI站到包装站超时则提示"采集失败,条码XXX
> AOI到包装站超过规定时间,请先解码回流!"；
>
> 如果条码AOI站到包装站超时解锁后没有重新回流则提示"采集失败,条码XXX
> AOI到包装站超过规定时间解码后需重新回流!"；
>
> 如果条码ATE到AOI超时则提示"此条码XXX
> ATE与AOI测试超过时间，请先解锁"；如果条码已经维修报废则提示"此条码已经维修报废"；
>
> 如果条码的当前状态不是此站，则提示"此条码XXX当前应做XXX站"；
>
> 否则返回"N"。

13. function CheckWIP(Stage, Barcode: PChar): PChar; stdcall;

> 参数说明：Stage表示站别代码（F010,F160...），Barcode表示条码。
>
> 说明：此接口的功能是检查条码是否可以做当前站测试。
>
> 如果当前DLL版本不正确，则提示"发现程式新版本，请更新后作业！"；
>
> 如果当前站别代码不正确，则提示"站别\' + stage + \'错误,请先确认！"；
>
> 如果条码还未归属过工单，则提示"此条码还没有归属工单，请先归属工单"；
>
> 如果条码所在的线别已达到不良率停线警告值值，则提示"XX站XX线不良率超出设置的停线值强制停线,请先解锁！"；
>
> 如条码所在的线别有重大不良（某站检到特定的不良现象）则提示"XX站XX线重大不良：XXX停线,请先解锁！"；
>
> 如果条码AOI站到包装站超时则提示"采集失败,条码XXX
> AOI到包装站超过规定时间,请先解码回流!"；
>
> 如果条码AOI站到包装站超时解锁后没有重新回流则提示"采集失败,条码XXX
> AOI到包装站超过规定时间解码后需重新回流!"；
>
> 如果条码ATE到AOI超时则提示"此条码XXX
> ATE与AOI测试超过时间，请先解锁"；如果条码已经维修报废则提示"此条码已经维修报废"；
>
> 如果条码的当前状态不是此站，则提示"此条码XXX当前应做XXX站"；
>
> 否则返回"N"。
>
> 此接口不适用贴背光站F007！！

14. function
    Collect_test(Stage,Barcode_NO,Barcode_Result,MachineNO,Line,Opno,

> DefectCode_Result,Barcode_Test: PChar): PChar; stdcall;

参数说明：有8个参数，其中stage表示站别(比如：ATE、FUN2、DARK、AOI,PLG或者直接用站别代码表示)，Barcode_NO表示扫描的条码，Barcode_Result
表示测试结果（pass/fail），MachineNO表示机台号，Line表示线别，opno表示作业人员工号，DefectCode_Result表示不良信息（格式如下:NG:不良代码1\^不良键位1:
不良代码2\^不良键位2 举例：NG: FQ3378\^G:
FQ3377\^A），Barcode_Test存放其他数值(比如电流、电压，可为空)。

> 说明：此接口的功能是上传测试数据。
>
> 上传成功返回"N"。

15. function GetModelVer(BCNO: PChar): PChar; stdcall;

> 参数说明:BCNO表示工单号
>
> 说明:按工单号返回机种-版本。其中：机种为工单号所在机种；版本为工单号所在料号对应的版本（SFC中"功能测试版本维护"的资料）。
>
> 返回值说明：输入的工单不存在，则返回"工单\[\' + BCNO + \'\]不存在！"；
>
> 输入的工单对应的版本不存在，则返回"料号\[\' + partno +
> \'\]对应的版本不存在！"；否则返回机种-版本。

16. function CheckBL_WIP(Stage, str, BL: PChar): PChar; stdcall;

> 参数说明：Stage表示站别代码（F007），str表示条码, BL背光条码。
>
> 说明：此接口的功能是检查条码是否可以做当前站测试。
>
> 如果当前DLL版本不正确，则提示"发现程式新版本，请更新后作业！"；
>
> 如果当前站别代码不正确，则提示"站别\' + stage + \'错误,请先确认！"；
>
> 如果条码还未归属过工单，则提示"此条码还没有归属工单，请先归属工单"；
>
> 如果条码所在的线别已达到不良率停线警告值值，则提示"XX站XX线不良率超出设置的停线值强制停线,请先解锁！"；
>
> 如条码所在的线别有重大不良（某站检到特定的不良现象）则提示"XX站XX线重大不良：XXX停线,请先解锁！"；
>
> 如果条码AOI站到包装站超时则提示"采集失败,条码XXX
> AOI到包装站超过规定时间,请先解码回流!"；
>
> 如果条码AOI站到包装站超时解锁后没有重新回流则提示"采集失败,条码XXX
> AOI到包装站超过规定时间解码后需重新回流!"；
>
> 如果条码ATE到AOI超时则提示"此条码XXX
> ATE与AOI测试超过时间，请先解锁"；如果条码已经维修报废则提示"此条码已经维修报废"；
>
> 如果条码的当前状态不是此站，则提示"此条码XXX当前应做XXX站"；
>
> 如果设定背光规则，则按背光规则片片卡控唯一性，否则背光须以6K或9Z
> 开头；
>
> 否则返回"N"。

17. function Get_ModelVersion(BCNO:PChar ):PChar ;stdcall ;

> 说明：此接口功能是输入工单号，带出机种-版本，有一个参数，类型为string，其中BCNO表示工单号。

18. function Get_ModelVersionLanguage(BCNO:PChar ):PChar ;stdcall ;

> 说明：此接口功能是输入工单号，带出机种-版本-语言别，有一个参数，类型为string，其中BCNO表示工单号。







