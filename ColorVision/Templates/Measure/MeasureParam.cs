using ColorVision.Services.Dao;

namespace ColorVision.Templates
{
    public class MeasureParam : ParamBase
    {
        public MeasureParam() { }
        public MeasureParam(MeasureMasterModel dbModel)
        {
            this.Id = dbModel.Id;
            this.IsEnable = dbModel.IsEnable;
        }
    }
}
