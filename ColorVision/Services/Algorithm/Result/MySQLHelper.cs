using ColorVision.MySql.DAO;

namespace ColorVision.Services.Algorithm.Result
{
    public static class MySQLHelper
    {
        private static BatchResultMasterDao BatchResultMasterDao { get; set; } = new BatchResultMasterDao();

        public static BatchResultMasterModel? GetBatch(int id) => id >= 0 ? BatchResultMasterDao.GetByID(id) : null;


        private static MeasureImgResultDao MeasureImgResultDao { get; set; } = new MeasureImgResultDao();

        public static MeasureImgResultModel? GetMeasureResultImg(int id) => id >= 0 ? MeasureImgResultDao.GetByID(id) : null;
    }
}
