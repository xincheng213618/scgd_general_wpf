using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql
{
    public interface IBaseModel
    {
        int GetPK();
        void SetPK(int id);
    }
}
