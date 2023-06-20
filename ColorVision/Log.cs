using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision
{
    public static class Log
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));


        public static void LogWrite(string Log)
        {
            log.Info(Log);
        }


        public static void LogException(Exception ex)
        {
            log.Error(ex);
        }

        public static void LogDeBug(string Log)
        {
            log.Debug(Log);
        }
    }
}
