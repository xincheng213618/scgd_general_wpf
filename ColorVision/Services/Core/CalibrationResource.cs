using ColorVision.Common.Sorts;
using ColorVision.Services.Dao;

namespace ColorVision.Services.Core
{
    public class CalibrationResource : BaseResource, ISortID, ISortFilePath
    {
        public CalibrationResource(SysResourceModel sysResourceModel) : base(sysResourceModel) 
        { 
        }



    }
}
