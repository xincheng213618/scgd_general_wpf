using System.ComponentModel;

namespace ProjectARVRPro.Process.Demura
{
    public class DemuraProcessConfig : ProcessConfigBase
    {
        [Category("导出配置")]
        [DisplayName("导出名称")]
        [Description("导出CSV和DynamicTestResults时显示的测试画面名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = "Demura";

        [Category("筛选配置")]
        [DisplayName("ImageConvert结果类型")]
        [Description("t_scgd_algorithm_result_master.img_file_type，默认93=ImageConvert")]
        public int ImageConvertResultType { get => _ImageConvertResultType; set { _ImageConvertResultType = value; OnPropertyChanged(); } }
        private int _ImageConvertResultType = 93;

        [Category("筛选配置")]
        [DisplayName("128文件关键字")]
        [Description("从ImageConvert结果文件名中匹配128灰阶CSV")]
        public string W128Keyword { get => _W128Keyword; set { _W128Keyword = value; OnPropertyChanged(); } }
        private string _W128Keyword = "W128";

        [Category("筛选配置")]
        [DisplayName("255文件关键字")]
        [Description("从ImageConvert结果文件名中匹配255灰阶CSV")]
        public string W255Keyword { get => _W255Keyword; set { _W255Keyword = value; OnPropertyChanged(); } }
        private string _W255Keyword = "W255";

        [Category("CSV配置")]
        [DisplayName("输入宽度")]
        [Description("DemuraTool读取CSV时使用的原始宽度")]
        public int InputWidth { get => _InputWidth; set { _InputWidth = value; OnPropertyChanged(); } }
        private int _InputWidth = 640;

        [Category("CSV配置")]
        [DisplayName("输入高度")]
        [Description("DemuraTool读取CSV时使用的原始高度")]
        public int InputHeight { get => _InputHeight; set { _InputHeight = value; OnPropertyChanged(); } }
        private int _InputHeight = 480;

        [Category("CSV配置")]
        [DisplayName("校验输入点数")]
        [Description("校验CSV数值数量是否等于输入宽度*输入高度")]
        public bool ValidateInputSize { get => _ValidateInputSize; set { _ValidateInputSize = value; OnPropertyChanged(); } }
        private bool _ValidateInputSize = true;

        [Category("CSV配置")]
        [DisplayName("W128曝光时间")]
        [Description("准备G128.csv时，每个CSV数值会除以该曝光时间，默认1")]
        public double W128ExposureTime { get => _W128ExposureTime; set { _W128ExposureTime = value; OnPropertyChanged(); } }
        private double _W128ExposureTime = 1;

        [Category("CSV配置")]
        [DisplayName("W255曝光时间")]
        [Description("准备G255.csv时，每个CSV数值会除以该曝光时间，默认1")]
        public double W255ExposureTime { get => _W255ExposureTime; set { _W255ExposureTime = value; OnPropertyChanged(); } }
        private double _W255ExposureTime = 1;

        [Category("工具配置")]
        [DisplayName("准备DemuraTool")]
        [Description("首次复制打包的DemuraTool_x64到用户可写目录，并把CSV改名为G128.csv/G255.csv")]
        public bool PrepareDemuraTool { get => _PrepareDemuraTool; set { _PrepareDemuraTool = value; OnPropertyChanged(); } }
        private bool _PrepareDemuraTool = true;

        [Category("工具配置")]
        [DisplayName("启动DemuraTool")]
        [Description("准备完成后启动DemuraTool_x64.exe；当前工具是GUI程序，默认不自动启动")]
        public bool LaunchDemuraTool { get => _LaunchDemuraTool; set { _LaunchDemuraTool = value; OnPropertyChanged(); } }
        private bool _LaunchDemuraTool;

        [Category("工具配置")]
        [DisplayName("直接生成Bin")]
        [Description("使用DemuraSaveBin.dll直接生成DemuraStatic.bin、DemuraDynamic.bin和DemuraMerged.bin")]
        public bool GenerateBinWithDll { get => _GenerateBinWithDll; set { _GenerateBinWithDll = value; OnPropertyChanged(); } }
        private bool _GenerateBinWithDll = true;

        [Category("工具配置")]
        [DisplayName("要求合并Bin")]
        [Description("要求工作目录存在DemuraMerged.bin才算通过；直接生成Bin开启时会自动校验")]
        public bool RequireMergedBin { get => _RequireMergedBin; set { _RequireMergedBin = value; OnPropertyChanged(); } }
        private bool _RequireMergedBin;

        [Category("工具配置")]
        [DisplayName("输出宽度")]
        [Description("写入DemuraConfig.ini的目标bin宽度")]
        public int OutputWidth { get => _OutputWidth; set { _OutputWidth = value; OnPropertyChanged(); } }
        private int _OutputWidth = 660;

        [Category("工具配置")]
        [DisplayName("输出高度")]
        [Description("写入DemuraConfig.ini的目标bin高度")]
        public int OutputHeight { get => _OutputHeight; set { _OutputHeight = value; OnPropertyChanged(); } }
        private int _OutputHeight = 504;

        [Category("工具配置")]
        [DisplayName("Block模式")]
        [Description("写入DemuraConfig.ini，0=1x1，1=2x1，2=1x2")]
        public int BlockMode { get => _BlockMode; set { _BlockMode = value; OnPropertyChanged(); } }
        private int _BlockMode;

        [Category("工具配置")]
        [DisplayName("Padding模式")]
        [Description("写入DemuraConfig.ini，0=补0，1=复制边缘，2=复制最后一行/列")]
        public int PaddingMode { get => _PaddingMode; set { _PaddingMode = value; OnPropertyChanged(); } }
        private int _PaddingMode = 2;

        [Category("烧录配置")]
        [DisplayName("生成后自动烧录")]
        [Description("生成bin后读取通用传感器配置，直连PG发送SENDFILE指令")]
        public bool BurnAfterGenerate { get => _BurnAfterGenerate; set { _BurnAfterGenerate = value; OnPropertyChanged(); } }
        private bool _BurnAfterGenerate = true;

        [Category("烧录配置")]
        [DisplayName("通用传感器Code")]
        [Description("持有PG TCP连接配置的通用传感器服务Code")]
        public string GeneralSensorCode { get => _GeneralSensorCode; set { _GeneralSensorCode = value; OnPropertyChanged(); } }
        private string _GeneralSensorCode = "DEV.Sensor.Default";

        [Category("烧录配置")]
        [DisplayName("通用传感器Category")]
        [Description("找不到指定Code时，按Category查找通用传感器服务")]
        public string GeneralSensorCategory { get => _GeneralSensorCategory; set { _GeneralSensorCategory = value; OnPropertyChanged(); } }
        private string _GeneralSensorCategory = "Sensor.Default";

        [Category("烧录配置")]
        [DisplayName("烧录源Bin")]
        [Description("SENDFILE指令中的本地文件路径，默认使用生成的DemuraDynamic.bin")]
        public string BurnSourceBinName { get => _BurnSourceBinName; set { _BurnSourceBinName = value; OnPropertyChanged(); } }
        private string _BurnSourceBinName = "DemuraDynamic.bin";

        [Category("烧录配置")]
        [DisplayName("PG目标文件名")]
        [Description("SENDFILE指令最后一个参数，默认DemuraMerged.bin")]
        public string BurnTargetFileName { get => _BurnTargetFileName; set { _BurnTargetFileName = value; OnPropertyChanged(); } }
        private string _BurnTargetFileName = "DemuraMerged.bin";

        [Category("烧录配置")]
        [DisplayName("PG通道")]
        [Description("SENDFILE指令PG后的通道参数，默认01；命令长度会按正文自动计算")]
        public string BurnPgChannel { get => _BurnPgChannel; set { _BurnPgChannel = value; OnPropertyChanged(); } }
        private string _BurnPgChannel = "01";

        [Category("烧录配置")]
        [DisplayName("PG文件序号")]
        [Description("SENDFILE,START后的文件序号，默认1")]
        public int BurnFileIndex { get => _BurnFileIndex; set { _BurnFileIndex = value; OnPropertyChanged(); } }
        private int _BurnFileIndex = 1;

        [Category("烧录配置")]
        [DisplayName("成功回包关键字")]
        [Description("TCP回包包含此关键字时判定烧录成功")]
        public string BurnSuccessResponse { get => _BurnSuccessResponse; set { _BurnSuccessResponse = value; OnPropertyChanged(); } }
        private string _BurnSuccessResponse = "SENDFILE,END,OK";

        [Category("烧录配置")]
        [DisplayName("TCP连接超时ms")]
        [Description("直连PG TCP端口的连接超时时间")]
        public int BurnTcpConnectTimeoutMs { get => _BurnTcpConnectTimeoutMs; set { _BurnTcpConnectTimeoutMs = value; OnPropertyChanged(); } }
        private int _BurnTcpConnectTimeoutMs = 5000;

        [Category("烧录配置")]
        [DisplayName("TCP回包超时ms")]
        [Description("发送SENDFILE后等待PG回包的时间")]
        public int BurnTcpResponseTimeoutMs { get => _BurnTcpResponseTimeoutMs; set { _BurnTcpResponseTimeoutMs = value; OnPropertyChanged(); } }
        private int _BurnTcpResponseTimeoutMs = 60000;
    }
}
