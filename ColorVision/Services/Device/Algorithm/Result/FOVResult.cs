using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Services.Device.Algorithm.Dao;

namespace ColorVision.Services.Device.Algorithm.Result
{
    public class FOVResult : ViewModelBase
    {
        public AlgorithmFovResultModel Model { get; set; }
        public FOVResult(AlgorithmFovResultModel algorithmFovResultModel)
        {
            Model = algorithmFovResultModel;
            Batch = ImageHelper.GetBatch(algorithmFovResultModel.BatchId);
            IMG = ImageHelper.GetMeasureResultImg(algorithmFovResultModel.ImgId);
        }
        public BatchResultMasterModel? Batch { get; set; }

        public MeasureImgResultModel? IMG { get; set; }

    }
}
