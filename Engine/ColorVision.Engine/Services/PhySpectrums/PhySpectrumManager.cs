using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;

namespace ColorVision.Engine.Services.PhySpectrums
{

    public class PhySpectrum:ViewModelBase
    {


    }

    public class PhySpectrumManager
    {
        private static PhySpectrumManager _instance;
        private static readonly object Locker = new();
        public static PhySpectrumManager GetInstance() { lock (Locker) { return _instance ??= new PhySpectrumManager(); } }






    }
}
