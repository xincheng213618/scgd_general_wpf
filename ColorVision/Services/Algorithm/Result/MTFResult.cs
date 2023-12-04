using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Services.Algorithm.MySql;

namespace ColorVision.Services.Algorithm.Result
{
    public class MTFResult : ViewModelBase
    {
        public AlgorithmMTFResultModel Model { get; set; }

        public MTFResult(AlgorithmMTFResultModel model)
        {
            Model = model;

            Batch = MySQLHelper.GetBatch(model.BatchId);
            IMG = MySQLHelper.GetMeasureResultImg(model.ImgId);
        }

        public BatchResultMasterModel? Batch { get; set; }

        public MeasureImgResultModel? IMG { get; set; }

    }
}
