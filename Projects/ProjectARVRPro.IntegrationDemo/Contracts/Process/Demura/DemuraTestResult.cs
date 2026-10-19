using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.Demura
{
    public class DemuraTestResult : ViewModelBase
    {
        public string FileName { get; set; } = string.Empty;

        /// <summary>W128 源文件的文本内容，JSON 解析后已还原转义。</summary>
        public string Data { get; set; } = string.Empty;
    }
}
