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

        [Category("工具配置")]
        [DisplayName("准备DemuraTool")]
        [Description("复制打包的DemuraTool_x64到用户可写目录，并把CSV改名为G128.csv/G255.csv")]
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
    }
}
