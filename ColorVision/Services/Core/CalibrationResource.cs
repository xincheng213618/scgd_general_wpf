using ColorVision.Common.MVVM;
using ColorVision.Common.Sorts;
using ColorVision.Services.Dao;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;

namespace ColorVision.Services.Core
{
    public class CalibrationFileConfig :ViewModelBase
    {
        public double Aperturein { get; set; }
        public double ExpTime { get; set; }
        public double ND { get; set; }
        public double ShotType { get; set; }
        public double Title { get; set; }
        public double Focallength { get; set; }
        public double GetImgMode { get; set; }
    }

    public class CalibrationResource : BaseFileResource, ISortID, ISortFilePath
    {
        public CalibrationFileConfig Config { get; set; }
        public CalibrationResource(SysResourceModel sysResourceModel) : base(sysResourceModel) 
        { 
            //Config = BaseResourceObjectExtensions.TryDeserializeConfig<CalibrationFileConfig>(sysResourceModel.Value);
        }

        public int IdShow { get; set; }

        public override void Save()
        {
            SysResourceModel.Remark = JsonConvert.SerializeObject(Config);
            VSysResourceDao.Instance.Save(SysResourceModel);
        }
    }
}
