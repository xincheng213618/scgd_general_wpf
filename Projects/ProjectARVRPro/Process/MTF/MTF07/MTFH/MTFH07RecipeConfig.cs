using ColorVision.Common.MVVM;
using ProjectARVRPro.Process.MTF.MTF07;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

#pragma warning disable CA1707 // 测试点属性名需与既有MTF命名和导出字段保持一致

namespace ProjectARVRPro.Process.MTF.MTF07.MTFH
{
    public sealed class MTFH07RecipeConfig : ViewModelBase, IRecipeConfig, IMTF07DynamicRecipeConfig
    {
        [Category("MTFH07（横条纹）")]
        public RecipeBase MTF_H_Center_0F { get => _MTF_H_Center_0F; set { _MTF_H_Center_0F = value ?? new(); OnPropertyChanged(); } }
        private RecipeBase _MTF_H_Center_0F = new(0.5, 0);

        [Category("MTFH07（横条纹）")]
        public RecipeBase MTF_H_LeftUp_0_7F { get => _MTF_H_LeftUp_0_7F; set { _MTF_H_LeftUp_0_7F = value ?? new(); OnPropertyChanged(); } }
        private RecipeBase _MTF_H_LeftUp_0_7F = new(0.5, 0);

        [Category("MTFH07（横条纹）")]
        public RecipeBase MTF_H_RightUp_0_7F { get => _MTF_H_RightUp_0_7F; set { _MTF_H_RightUp_0_7F = value ?? new(); OnPropertyChanged(); } }
        private RecipeBase _MTF_H_RightUp_0_7F = new(0.5, 0);

        [Category("MTFH07（横条纹）")]
        public RecipeBase MTF_H_LeftDown_0_7F { get => _MTF_H_LeftDown_0_7F; set { _MTF_H_LeftDown_0_7F = value ?? new(); OnPropertyChanged(); } }
        private RecipeBase _MTF_H_LeftDown_0_7F = new(0.5, 0);

        [Category("MTFH07（横条纹）")]
        public RecipeBase MTF_H_RightDown_0_7F { get => _MTF_H_RightDown_0_7F; set { _MTF_H_RightDown_0_7F = value ?? new(); OnPropertyChanged(); } }
        private RecipeBase _MTF_H_RightDown_0_7F = new(0.5, 0);

        public bool TryGetRecipe(string itemName, out RecipeBase recipe)
        {
            recipe = itemName switch
            {
                nameof(MTFH07TestResult.MTF_H_Center_0F) => MTF_H_Center_0F,
                nameof(MTFH07TestResult.MTF_H_LeftUp_0_7F) => MTF_H_LeftUp_0_7F,
                nameof(MTFH07TestResult.MTF_H_RightUp_0_7F) => MTF_H_RightUp_0_7F,
                nameof(MTFH07TestResult.MTF_H_LeftDown_0_7F) => MTF_H_LeftDown_0_7F,
                nameof(MTFH07TestResult.MTF_H_RightDown_0_7F) => MTF_H_RightDown_0_7F,
                _ => null!
            };
            return recipe != null;
        }
    }
}
