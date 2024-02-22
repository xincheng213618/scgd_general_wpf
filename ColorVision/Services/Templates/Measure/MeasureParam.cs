using ColorVision.Services.Dao;
using ColorVision.Services.Templates;

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
