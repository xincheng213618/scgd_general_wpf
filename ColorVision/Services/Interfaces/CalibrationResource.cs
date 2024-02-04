#pragma warning disable CS8603,CS0649
using ColorVision.Services.Dao;
using ColorVision.Sorts;

namespace ColorVision.Services.Interfaces
{
    public class CalibrationResource : BaseResource, ISortID, ISortFilePath
    {
        public CalibrationResource(SysResourceModel sysResourceModel) : base(sysResourceModel) 
        { 
        }
    }
}
