using ProjectARVRPro.Process.KeyedResults;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTF.VRPartition;

public sealed class VRMtfPartitionProcessConfig : ProcessConfigBase<VRMtfPartitionRecipeConfig>
{
    [Category("输出配置"), DisplayName("输出Key")]
    [Description("VR分区结果的画面名称。H/V或多个画面分别使用不同Key，例如VRMTFH、VRMTFV。")]
    public string Key { get => _key; set { _key = value; OnPropertyChanged(); } }
    private string _key = "VRMTFH";

    [Category("解析配置"), DisplayName("MTF结果名称")]
    [Description("按算法结果TName精确匹配；留空时批次必须只有一份MTF 2.0结果。")]
    public string ResultName { get => _resultName; set { _resultName = value; OnPropertyChanged(); } }
    private string _resultName = string.Empty;

    [Category("分区配置"), DisplayName("网格边长")]
    [Description("按原始点名称自然排序后，逐行组成Size×Size网格。")]
    public int Size { get => _size; set { _size = value; OnPropertyChanged(); } }
    private int _size = 30;

    [Category("分区配置"), DisplayName("网格中心")]
    [Description("以0开始的行、列索引使用同一个中心；30×30的中心为14.5。")]
    public double Center { get => _center; set { _center = value; OnPropertyChanged(); } }
    private double _center = 14.5;

    [Category("分区配置"), DisplayName("A区外半径")]
    public double RA { get => _ra; set { _ra = value; OnPropertyChanged(); } }
    private double _ra = 1;

    [Category("分区配置"), DisplayName("B区外半径")]
    public double RB { get => _rb; set { _rb = value; OnPropertyChanged(); } }
    private double _rb = 4;

    [Category("分区配置"), DisplayName("C区外半径")]
    public double RC { get => _rc; set { _rc = value; OnPropertyChanged(); } }
    private double _rc = 9;

    [Category("分区配置"), DisplayName("D区外半径")]
    [Description("A/B/C/D为互斥圆环，边界点归内侧区域，D区以外的点不参与区域统计。旧V参数为1、2、4、7.5。")]
    public double RD { get => _rd; set { _rd = value; OnPropertyChanged(); } }
    private double _rd = 15;

    [Category("显示配置"), DisplayName("显示格式")]
    public string ShowConfig { get => _showConfig; set { _showConfig = value; OnPropertyChanged(); } }
    private string _showConfig = "F3";

    [Category("输出配置"), DisplayName("单位")]
    [Description("仅标记单位，不换算原始MTF数值；默认保留旧分区的原始尺度。")]
    public string Unit { get => _unit; set { _unit = value; OnPropertyChanged(); } }
    private string _unit = string.Empty;

    [Browsable(false)]
    public new bool SaveCsv { get => base.SaveCsv; set => base.SaveCsv = value; }

    public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, "VRMTFH");
}
