using ColorVision.MySql.DAO;

namespace ColorVision.MySql.Service
{
    public static class ImageHelper
    {
        private static BatchResultMasterDao BatchResultMasterDao { get; set; } = new BatchResultMasterDao();

        public static BatchResultMasterModel? GetBatch(int id) => id >= 0 ? BatchResultMasterDao.GetById(id) : null;

        private static MeasureImgResultDao MeasureImgResultDao { get; set; } = new MeasureImgResultDao();

        public static MeasureImgResultModel? GetMeasureResultImg(int id) => id >= 0 ? MeasureImgResultDao.GetById(id) : null;
    }
}
