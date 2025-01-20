using ColorVision.Engine.Impl.CommonImage;

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
