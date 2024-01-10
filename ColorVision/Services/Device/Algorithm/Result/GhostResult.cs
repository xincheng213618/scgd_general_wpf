using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Services.Device.Algorithm.Dao;

namespace ColorVision.Services.Device.Algorithm.Result
{
    public class GhostResult : ViewModelBase
    {
        public AlgorithmGhostResultModel Model { get; set; }

        public GhostResult(AlgorithmGhostResultModel model)
        {
            Model = model;
            Batch = ImageHelper.GetBatch(model.BatchId);
            IMG = ImageHelper.GetMeasureResultImg(model.ImgId);
        }

        public float[] Pdfrequency { get; set; }

        public float[] PdomainSamplingData { get; set; }


        public BatchResultMasterModel? Batch { get; set; }

        public MeasureImgResultModel? IMG { get; set; }
    }
}
