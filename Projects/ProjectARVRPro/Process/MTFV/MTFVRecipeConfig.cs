using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTFV
{
    /// <summary>
    /// 旧版MTFV Recipe配置（上下限）
    /// </summary>
    public class MTFVRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("MTF_V(旧版)")]
        public RecipeBase MTF_V_Center_0F { get => _MTF_V_Center_0F; set { _MTF_V_Center_0F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_V_Center_0F = new RecipeBase(0.5, 0);

        [Category("MTF_V(旧版)")]
        public RecipeBase MTF_V_LeftUp_0_5F { get => _MTF_V_LeftUp_0_5F; set { _MTF_V_LeftUp_0_5F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_V_LeftUp_0_5F = new RecipeBase(0.5, 0);

        [Category("MTF_V(旧版)")]
        public RecipeBase MTF_V_RightUp_0_5F { get => _MTF_V_RightUp_0_5F; set { _MTF_V_RightUp_0_5F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_V_RightUp_0_5F = new RecipeBase(0.5, 0);

        [Category("MTF_V(旧版)")]
        public RecipeBase MTF_V_LeftDown_0_5F { get => _MTF_V_LeftDown_0_5F; set { _MTF_V_LeftDown_0_5F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_V_LeftDown_0_5F = new RecipeBase(0.5, 0);

        [Category("MTF_V(旧版)")]
        public RecipeBase MTF_V_RightDown_0_5F { get => _MTF_V_RightDown_0_5F; set { _MTF_V_RightDown_0_5F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_V_RightDown_0_5F = new RecipeBase(0.5, 0);

        [Category("MTF_V(旧版)")]
        public RecipeBase MTF_V_LeftUp_0_8F { get => _MTF_V_LeftUp_0_8F; set { _MTF_V_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_V_LeftUp_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF_V(旧版)")]
        public RecipeBase MTF_V_RightUp_0_8F { get => _MTF_V_RightUp_0_8F; set { _MTF_V_RightUp_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_V_RightUp_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF_V(旧版)")]
        public RecipeBase MTF_V_LeftDown_0_8F { get => _MTF_V_LeftDown_0_8F; set { _MTF_V_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_V_LeftDown_0_8F = new RecipeBase(0.5, 0);

        [Category("MTF_V(旧版)")]
        public RecipeBase MTF_V_RightDown_0_8F { get => _MTF_V_RightDown_0_8F; set { _MTF_V_RightDown_0_8F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_V_RightDown_0_8F = new RecipeBase(0.5, 0);
    }
}
