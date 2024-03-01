using ColorVision.Services.Dao;

namespace ColorVision.Services.Templates.Measure
{
    public class MeasureParam : ParamBase
    {
        public MeasureParam() { }
        public MeasureParam(MeasureMasterModel dbModel)
        {
            Id = dbModel.Id;
            IsEnable = dbModel.IsEnable;
        }
    }
}
