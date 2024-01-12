
using ColorVision.MySql.DAO;

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
