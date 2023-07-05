using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace ColorVision.MySql
{
    public class PoiService
    {
        private PoiMasterDao poiMaster;
        private PoiDetailDao poiDetail;

        public PoiService()
        {
            poiMaster = new PoiMasterDao();
            poiDetail = new PoiDetailDao();
        }

        public List<PoiMasterModel> GetPoiMasterAll(int tenantId)
        {
            return poiMaster.GetAll(tenantId);
        }
        public void Save(PoiMasterModel master)
        {
            poiMaster.Save(master);
        }
        public void Save(PoiParam poiParam)
        {
            PoiMasterModel poiMasterModel = new PoiMasterModel(poiParam);
            poiMaster.Save(poiMasterModel);

            List< PoiDetailModel > poiDetails = new List< PoiDetailModel >();
            foreach (PoiParamData pt in poiParam.PoiPoints)
            {
                PoiDetailModel poiDetail = new PoiDetailModel(poiParam.ID, pt);
                poiDetails.Add(poiDetail);
            }
            poiDetail.SaveByPid(poiParam.ID, poiDetails);

        }

        internal List<PoiDetailModel> GetPoiDetailByPid(int pid)
        {
            return poiDetail.GetAllByPid(pid);
        }

        internal PoiMasterModel? GetPoiMasterById(int pkId)
        {
            return poiMaster.GetByID(pkId);
        }
    }
}
