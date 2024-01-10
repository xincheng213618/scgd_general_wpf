using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Services.Device.Algorithm.Dao;
using System;

namespace ColorVision.Services.Device.Algorithm.Result
{
    public class SFRResult : ViewModelBase
    {
        public AlgorithmSfrResultModel Model { get; set; }

        public SFRResult(AlgorithmSfrResultModel model)
        {
            Model = model;

            Pdfrequency = Util.DeserializeObject<float[]>(model.Pdfrequency) ?? Array.Empty<float>();
            PdomainSamplingData = Util.DeserializeObject<float[]>(model.PdomainSamplingData) ?? Array.Empty<float>();



            Batch = ImageHelper.GetBatch(model.BatchId);
            IMG = ImageHelper.GetMeasureResultImg(model.ImgId);

        }

        public float[] Pdfrequency { get; set; }

        public float[] PdomainSamplingData { get; set; }


        public BatchResultMasterModel? Batch { get; set; }

        public MeasureImgResultModel? IMG { get; set; }
    }
}
