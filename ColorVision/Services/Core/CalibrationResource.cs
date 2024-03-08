using ColorVision.Services.Dao;
using ColorVision.Sorts;

namespace ColorVision.Services.Core
{
    public class CalibrationResource : BaseResource, ISortID, ISortFilePath
    {
        public CalibrationResource(SysResourceModel sysResourceModel) : base(sysResourceModel) 
        { 
        }
    }
}
