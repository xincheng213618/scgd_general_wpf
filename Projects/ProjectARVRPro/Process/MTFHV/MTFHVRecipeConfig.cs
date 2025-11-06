
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTFHV
{
    public class MTFHVRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_Center_0F { get => _MTF_HV_H_Center_0F; set { _MTF_HV_H_Center_0F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_Center_0F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_Center_0F { get => _MTF_HV_V_Center_0F; set { _MTF_HV_V_Center_0F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_Center_0F = new RecipeBase(0.5, 0);


        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_LeftUp_0_3F { get => _MTF_HV_H_LeftUp_0_3F; set { _MTF_HV_H_LeftUp_0_3F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_LeftUp_0_3F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_LeftUp_0_3F { get => _MTF_HV_V_LeftUp_0_3F; set { _MTF_HV_V_LeftUp_0_3F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_LeftUp_0_3F = new RecipeBase(0.5, 0);


        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_RightUp_0_3F { get => _MTF_HV_H_RightUp_0_3F; set { _MTF_HV_H_RightUp_0_3F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_RightUp_0_3F = new RecipeBase(0.5, 0);
        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_RightUp_0_3F { get => _MTF_HV_V_RightUp_0_3F; set { _MTF_HV_V_RightUp_0_3F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_RightUp_0_3F = new RecipeBase(0.5, 0);


        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_RightDown_0_3F { get => _MTF_HV_H_RightDown_0_3F; set { _MTF_HV_H_RightDown_0_3F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_RightDown_0_3F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_RightDown_0_3F { get => _MTF_HV_V_RightDown_0_3F; set { _MTF_HV_V_RightDown_0_3F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_RightDown_0_3F = new RecipeBase(0.5, 0);

        

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_LeftDown_0_3F { get => _MTF_HV_H_LeftDown_0_3F; set { _MTF_HV_H_LeftDown_0_3F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_LeftDown_0_3F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_LeftDown_0_3F { get => _MTF_HV_V_LeftDown_0_3F; set { _MTF_HV_V_LeftDown_0_3F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_LeftDown_0_3F = new RecipeBase(0.5, 0);


        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_LeftUp_0_6F { get => _MTF_HV_H_LeftUp_0_6F; set { _MTF_HV_H_LeftUp_0_6F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_LeftUp_0_6F = new RecipeBase(0.5, 0);


        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_LeftUp_0_6F { get => _MTF_HV_V_LeftUp_0_6F; set { _MTF_HV_V_LeftUp_0_6F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_LeftUp_0_6F = new RecipeBase(0.5, 0);


        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_RightUp_0_6F { get => _MTF_HV_H_RightUp_0_6F; set { _MTF_HV_H_RightUp_0_6F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_RightUp_0_6F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_RightDown_0_6F { get => _MTF_HV_V_RightDown_0_6F; set { _MTF_HV_V_RightDown_0_6F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_RightDown_0_6F = new RecipeBase(0.5, 0);


        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_RightDown_0_6F { get => _MTF_HV_H_RightDown_0_6F; set { _MTF_HV_H_RightDown_0_6F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_RightDown_0_6F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_RightUp_0_6F { get => _MTF_HV_V_RightUp_0_6F; set { _MTF_HV_V_RightUp_0_6F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_RightUp_0_6F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_LeftDown_0_6F { get => _MTF_HV_H_LeftDown_0_6F; set { _MTF_HV_H_LeftDown_0_6F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_LeftDown_0_6F = new RecipeBase(0.5, 0);
        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_LeftDown_0_6F { get => _MTF_HV_V_LeftDown_0_6F; set { _MTF_HV_V_LeftDown_0_6F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_LeftDown_0_6F = new RecipeBase(0.5, 0);


        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_LeftUp_0_8F { get => _MTF_HV_H_LeftUp_0_8F; set { _MTF_HV_H_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_LeftUp_0_8F = new RecipeBase(0.5, 0);
        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_LeftUp_0_8F { get => _MTF_HV_V_LeftUp_0_8F; set { _MTF_HV_V_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_LeftUp_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_RightUp_0_8F { get => _MTF_HV_H_RightUp_0_8F; set { _MTF_HV_H_RightUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_RightUp_0_8F = new RecipeBase(0.5, 0);


        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_RightUp_0_8F { get => _MTF_HV_V_RightUp_0_8F; set { _MTF_HV_V_RightUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_RightUp_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_RightDown_0_8F { get => _MTF_HV_H_RightDown_0_8F; set { _MTF_HV_H_RightDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_RightDown_0_8F = new RecipeBase(0.5, 0);
        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_RightDown_0_8F { get => _MTF_HV_V_RightDown_0_8F; set { _MTF_HV_V_RightDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_RightDown_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_H_LeftDown_0_8F { get => _MTF_HV_H_LeftDown_0_8F; set { _MTF_HV_H_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_LeftDown_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF_HV")]
        public RecipeBase MTF_HV_V_LeftDown_0_8F { get => _MTF_HV_V_LeftDown_0_8F; set { _MTF_HV_V_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_LeftDown_0_8F = new RecipeBase(0.5, 0);
    }
}