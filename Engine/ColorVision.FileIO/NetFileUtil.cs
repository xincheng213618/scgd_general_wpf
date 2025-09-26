#pragma warning disable CS8601,CA1822
using System.IO;
using System.Threading.Tasks;

namespace ColorVision.FileIO
{
    public class NetFileUtil
    {
        public NetFileUtil()
        {
        }

        public CVCIEFile OpenLocalCVFile(string fileName)
        {
            CVType extType = CVType.Src;
            if (Path.GetExtension(fileName).Contains("cvraw"))
            {
                extType = CVType.Raw;
            }
            else if (Path.GetExtension(fileName).Contains("cvcie"))
            {
                extType = CVType.CIE;
            }
            return OpenLocalCVFile(fileName, extType);
        }

        public CVCIEFile OpenLocalCVFile(string fileName, CVType extType)
        {
            ReadLocalFile(fileName, extType, out CVCIEFile fileInfo);
            return fileInfo;
        }

        private int ReadLocalFile(string fileName, CVType extType, out CVCIEFile fileInfo)
        {
            fileInfo = new CVCIEFile();
            if (extType == CVType.CIE)
            {
               CVFileUtil.ReadCVCIE(fileName, out fileInfo);
                return 0;
            }
            else if (extType == CVType.Raw)
            {
                CVFileUtil.ReadCVRaw(fileName, out fileInfo);
                return 0;
            }
            else if (extType == CVType.Src)
            {
                CVFileUtil.ReadCVRaw(fileName, out fileInfo);
                return 0;
            }
            return -1;
        }

    }


}
