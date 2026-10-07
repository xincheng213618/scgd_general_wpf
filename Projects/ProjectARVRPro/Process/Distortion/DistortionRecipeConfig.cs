
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("TV")]
        public RecipeBase HorizontalTVDistortion { get => _HorizontalTVDistortion; set { _HorizontalTVDistortion = value; OnPropertyChanged(); } }
        private RecipeBase _HorizontalTVDistortion = new RecipeBase(0, 2.1);

        [Category("TV")]
        public RecipeBase VerticalTVDistortion { get => _VerticalTVDistortion; set { _VerticalTVDistortion = value; OnPropertyChanged(); } }
        private RecipeBase _VerticalTVDistortion = new RecipeBase(0, 2.1);

        [Category("Optic")]
        [DisplayName("Optic_Distortion")]
        public RecipeBase OpticDistortion { get => _OpticDistortion; set { _OpticDistortion = value; OnPropertyChanged(); } }
        private RecipeBase _OpticDistortion = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase DistortionTop { get => _DistortionTop; set { _DistortionTop = value; OnPropertyChanged(); } }
        private RecipeBase _DistortionTop = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase DistortionBottom { get => _DistortionBottom; set { _DistortionBottom = value; OnPropertyChanged(); } }
        private RecipeBase _DistortionBottom = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase DistortionLeft { get => _DistortionLeft; set { _DistortionLeft = value; OnPropertyChanged(); } }
        private RecipeBase _DistortionLeft = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase DistortionRight { get => _DistortionRight; set { _DistortionRight = value; OnPropertyChanged(); } }
        private RecipeBase _DistortionRight = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase KeystoneHoriz { get => _KeystoneHoriz; set { _KeystoneHoriz = value; OnPropertyChanged(); } }
        private RecipeBase _KeystoneHoriz = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase KeystoneVert { get => _KeystoneVert; set { _KeystoneVert = value; OnPropertyChanged(); } }
        private RecipeBase _KeystoneVert = new RecipeBase(0, 0);

        [Category("四角几何"), DisplayName("最大倾斜角 (°)"), Description("四边相对图像水平/垂直方向的最大夹角，包含整体旋转；上下限为0表示不限。")]
        public RecipeBase MaximumTiltDegrees { get => _MaximumTiltDegrees; set { _MaximumTiltDegrees = value; OnPropertyChanged(); } }
        private RecipeBase _MaximumTiltDegrees = new RecipeBase(0, 0);

        [Category("四角几何"), DisplayName("最大边长差比例 (%)"), Description("上下、左右对边长度差除以对应对边平均长度，取最大绝对百分比；不随旧P9口径改变，上下限为0表示不限。")]
        public RecipeBase MaximumEdgeLengthDifferencePercent { get => _MaximumEdgeLengthDifferencePercent; set { _MaximumEdgeLengthDifferencePercent = value; OnPropertyChanged(); } }
        private RecipeBase _MaximumEdgeLengthDifferencePercent = new RecipeBase(0, 0);
    }
}
