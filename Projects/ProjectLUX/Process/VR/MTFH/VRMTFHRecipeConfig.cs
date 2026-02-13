
using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.VR.MTFH
{
    public class VRMTFHRecipeConfig : ViewModelBase, IRecipeConfig
    {

        [Category("MTF")]
        public RecipeBase Region_A_Min { get => _Region_A_Min; set { _Region_A_Min = value; OnPropertyChanged(); } }
        private RecipeBase _Region_A_Min = new RecipeBase(0, 0);

        [Category("MTF")]
        public RecipeBase Region_A_Max { get => _Region_A_Max; set { _Region_A_Max = value; OnPropertyChanged(); } }
        private RecipeBase _Region_A_Max = new RecipeBase(0, 0);

        [Category("MTF")]
        public RecipeBase Region_A_Average { get => _Region_A_Average; set { _Region_A_Average = value; OnPropertyChanged(); } }
        private RecipeBase _Region_A_Average = new RecipeBase(0, 0);


        [Category("MTF")]
        public RecipeBase Region_B_Min { get => _Region_B_Min; set { _Region_B_Min = value; OnPropertyChanged(); } }
        private RecipeBase _Region_B_Min = new RecipeBase(0, 0);

        [Category("MTF")]
        public RecipeBase Region_B_Max { get => _Region_B_Max; set { _Region_B_Max = value; OnPropertyChanged(); } }
        private RecipeBase _Region_B_Max = new RecipeBase(0, 0);

        [Category("MTF")]
        public RecipeBase Region_B_Average { get => _Region_B_Average; set { _Region_B_Average = value; OnPropertyChanged(); } }
        private RecipeBase _Region_B_Average = new RecipeBase(0, 0);


        [Category("MTF")]
        public RecipeBase Region_C_Min { get => _Region_C_Min; set { _Region_C_Min = value; OnPropertyChanged(); } }
        private RecipeBase _Region_C_Min = new RecipeBase(0, 0);

        [Category("MTF")]
        public RecipeBase Region_C_Max { get => _Region_C_Max; set { _Region_C_Max = value; OnPropertyChanged(); } }
        private RecipeBase _Region_C_Max = new RecipeBase(0, 0);

        [Category("MTF")]
        public RecipeBase Region_C_Average { get => _Region_C_Average; set { _Region_C_Average = value; OnPropertyChanged(); } }
        private RecipeBase _Region_C_Average = new RecipeBase(0, 0);


        [Category("MTF")]
        public RecipeBase Region_D_Min { get => _Region_D_Min; set { _Region_D_Min = value; OnPropertyChanged(); } }
        private RecipeBase _Region_D_Min = new RecipeBase(0, 0);

        [Category("MTF")]
        public RecipeBase Region_D_Max { get => _Region_D_Max; set { _Region_D_Max = value; OnPropertyChanged(); } }
        private RecipeBase _Region_D_Max = new RecipeBase(0, 0);

        [Category("MTF")]
        public RecipeBase Region_D_Average { get => _Region_D_Average; set { _Region_D_Average = value; OnPropertyChanged(); } }
        private RecipeBase _Region_D_Average = new RecipeBase(0, 0);

    }
}