using ColorVision.Solution.Imp.CommonImage;

namespace ColorVision.Engine.Impl.CVFile
{
    public class SolutionProcessCVCIE : SolutionProcessImage
    {
        public override void Open()
        {
            ImageView.OpenImage(FullName);
        }
    }



}
