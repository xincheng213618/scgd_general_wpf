using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Services.Algorithm.MySql;

namespace ColorVision.Services.Algorithm.Result
{
    public class FOVResult : ViewModelBase
    {
        public AlgorithmFovResultModel Model { get; set; }
        public FOVResult(AlgorithmFovResultModel algorithmFovResultModel)
        {
            Model = algorithmFovResultModel;
            Batch = MySQLHelper.GetBatch(algorithmFovResultModel.BatchId);
            IMG = MySQLHelper.GetMeasureResultImg(algorithmFovResultModel.ImgId);
        }
        public BatchResultMasterModel? Batch { get; set; }

        public MeasureImgResultModel? IMG { get; set; }

    }
}
