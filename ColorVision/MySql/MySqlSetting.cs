using ColorVision.Common.MVVM;
using log4net;

namespace ColorVision.MySql
{
    public class MySqlSetting : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlSetting));
        private static MySqlSetting _instance;
        private static readonly object _locker = new();
        public static MySqlSetting GetInstance() { lock (_locker) { return _instance ??= new MySqlSetting(); } }




    }
}
