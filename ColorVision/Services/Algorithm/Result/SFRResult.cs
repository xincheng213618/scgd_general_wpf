using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Services.Algorithm.MySql;
using System;

namespace ColorVision.Services.Algorithm.Result
{
    public class SFRResult : ViewModelBase
    {
        public AlgorithmSfrResultModel Model { get; set; }

        public SFRResult(AlgorithmSfrResultModel model)
        {
            Model = model;

            Pdfrequency = Util.DeserializeObject<float[]>(model.Pdfrequency) ?? Array.Empty<float>();
            PdomainSamplingData = Util.DeserializeObject<float[]>(model.PdomainSamplingData) ?? Array.Empty<float>();



            Batch = MySQLHelper.GetBatch(model.BatchId);
            IMG = MySQLHelper.GetMeasureResultImg(model.ImgId);

        }

        public float[] Pdfrequency { get; set; }

        public float[] PdomainSamplingData { get; set; }


        public BatchResultMasterModel? Batch { get; set; }

        public MeasureImgResultModel? IMG { get; set; }
    }
}
