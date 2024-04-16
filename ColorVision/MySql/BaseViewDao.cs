using log4net;

namespace ColorVision.MySql
{
    public class BaseViewDao<T> : BaseDao1 where T : IPKModel
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseViewDao<T>));

        public BaseViewDao(string viewName, string pkField, bool isLogicDel) : base(viewName, pkField, isLogicDel)
        {

        }

    }
}
