using ColorVision.SettingUp;

namespace ColorVision.Solution
{
    /// <summary>
    /// 工程模块控制中心
    /// </summary>
    public class SolutionControl
    {
        private static SolutionControl _instance;
        private static readonly object _locker = new();
        public static SolutionControl GetInstance() { lock (_locker) { return _instance ??= new SolutionControl(); } }

        public SolutionConfig SolutionConfig { get => SoftwareConfig.SolutionConfig; }
        public SolutionSetting SolutionSetting { get => SolutionConfig.SolutionSetting; }
        public SoftwareConfig SoftwareConfig { get; private set; }

        public SolutionControl() 
        {
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
        }



    }
}
