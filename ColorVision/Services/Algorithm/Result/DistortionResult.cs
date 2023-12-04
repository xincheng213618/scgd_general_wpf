using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Services.Algorithm.MySql;

namespace ColorVision.Services.Algorithm.Result
{
    public class DistortionResult : ViewModelBase
    {
        public AlgorithmDistortionResultModel Model { get; set; }

        public DistortionResult(AlgorithmDistortionResultModel model)
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
