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

        public List<PoiMasterModel> GetPoiMasterAll()
        {
            return poiMaster.GetAll();
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
            poiDetail.Save(poiDetails);

        }
    }
}
