
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTFHV048
{
    public class MTFHV048RecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("MTF048")]
        public RecipeBase MTF048_H_Center_0F { get => _MTF048_H_Center_0F; set { _MTF048_H_Center_0F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_H_Center_0F = new RecipeBase(0.5, 0);

        [Category("MTF048")]
        public RecipeBase MTF048_V_Center_0F { get => _MTF048_V_Center_0F; set { _MTF048_V_Center_0F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_V_Center_0F = new RecipeBase(0.5, 0);


        [Category("MTF_HV_058")]
        public RecipeBase MTF_HV_H_LeftUp_0_4F { get => _MTF_HV_H_LeftUp_0_4F; set { _MTF_HV_H_LeftUp_0_4F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_LeftUp_0_4F = new RecipeBase(0.5, 0);

        [Category("MTF_HV_058")]
        public RecipeBase MTF_HV_V_LeftUp_0_4F { get => _MTF_HV_V_LeftUp_0_4F; set { _MTF_HV_V_LeftUp_0_4F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_LeftUp_0_4F = new RecipeBase(0.5, 0);


        [Category("MTF_HV_058")]
        public RecipeBase MTF_HV_H_RightUp_0_4F { get => _MTF_HV_H_RightUp_0_4F; set { _MTF_HV_H_RightUp_0_4F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_RightUp_0_4F = new RecipeBase(0.5, 0);
        [Category("MTF_HV_058")]
        public RecipeBase MTF_HV_V_RightUp_0_4F { get => _MTF_HV_V_RightUp_0_4F; set { _MTF_HV_V_RightUp_0_4F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_RightUp_0_4F = new RecipeBase(0.5, 0);


        [Category("MTF_HV_058")]
        public RecipeBase MTF_HV_H_RightDown_0_4F { get => _MTF_HV_H_RightDown_0_4F; set { _MTF_HV_H_RightDown_0_4F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_RightDown_0_4F = new RecipeBase(0.5, 0);

        [Category("MTF_HV_058")]
        public RecipeBase MTF_HV_V_RightDown_0_4F { get => _MTF_HV_V_RightDown_0_4F; set { _MTF_HV_V_RightDown_0_4F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_RightDown_0_4F = new RecipeBase(0.5, 0);

        

        [Category("MTF_HV_058")]
        public RecipeBase MTF_HV_H_LeftDown_0_4F { get => _MTF_HV_H_LeftDown_0_4F; set { _MTF_HV_H_LeftDown_0_4F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_LeftDown_0_4F = new RecipeBase(0.5, 0);

        [Category("MTF_HV_058")]
        public RecipeBase MTF_HV_V_LeftDown_0_4F { get => _MTF_HV_V_LeftDown_0_4F; set { _MTF_HV_V_LeftDown_0_4F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_LeftDown_0_4F = new RecipeBase(0.5, 0);



        [Category("MTF048")]
        public RecipeBase MTF048_H_LeftUp_0_8F { get => _MTF048_H_LeftUp_0_8F; set { _MTF048_H_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_H_LeftUp_0_8F = new RecipeBase(0.5, 0);
        [Category("MTF048")]
        public RecipeBase MTF048_V_LeftUp_0_8F { get => _MTF048_V_LeftUp_0_8F; set { _MTF048_V_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_V_LeftUp_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF048")]
        public RecipeBase MTF048_H_RightUp_0_8F { get => _MTF048_H_RightUp_0_8F; set { _MTF048_H_RightUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_H_RightUp_0_8F = new RecipeBase(0.5, 0);


        [Category("MTF048")]
        public RecipeBase MTF048_V_RightUp_0_8F { get => _MTF048_V_RightUp_0_8F; set { _MTF048_V_RightUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_V_RightUp_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF048")]
        public RecipeBase MTF048_H_RightDown_0_8F { get => _MTF048_H_RightDown_0_8F; set { _MTF048_H_RightDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_H_RightDown_0_8F = new RecipeBase(0.5, 0);
        [Category("MTF048")]
        public RecipeBase MTF048_V_RightDown_0_8F { get => _MTF048_V_RightDown_0_8F; set { _MTF048_V_RightDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_V_RightDown_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF048")]
        public RecipeBase MTF048_H_LeftDown_0_8F { get => _MTF048_H_LeftDown_0_8F; set { _MTF048_H_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_H_LeftDown_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF048")]
        public RecipeBase MTF048_V_LeftDown_0_8F { get => _MTF048_V_LeftDown_0_8F; set { _MTF048_V_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF048_V_LeftDown_0_8F = new RecipeBase(0.5, 0);
    }
}
