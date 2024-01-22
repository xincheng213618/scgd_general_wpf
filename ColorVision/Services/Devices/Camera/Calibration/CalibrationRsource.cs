#pragma warning disable CS8603,CS0649
using ColorVision.MVVM;
using ColorVision.Services.Dao;
using ColorVision.Sorts;

namespace ColorVision.Services.Devices.Camera.Calibrations
{
    public class CalibrationRsource : ViewModelBase, ISortID, ISortName, ISortFilePath
    {
        public SysResourceModel SysResourceModel { get; set; }
        public CalibrationRsource(SysResourceModel SysResourceModel)
        {
            this.SysResourceModel = SysResourceModel;
            Name = SysResourceModel.Name;
            FilePath = SysResourceModel.Value;
            Id = SysResourceModel.Id;
            Pid = SysResourceModel.Pid;
        }
        public string? Name { get; set; }
        public string? FilePath { get; set; }
        public int Id { get; set; }
        public int? Pid { get; set; }
    }


}
