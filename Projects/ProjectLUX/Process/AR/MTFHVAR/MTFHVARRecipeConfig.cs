#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectLUX;
using ProjectLUX.Process.MTFHVAR;
using ProjectLUX.Recipe;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectLUX.Process.MTFHVAR
{
    public class MTFHVARRecipeConfig : ViewModelBase, IRecipeConfig
    {
        // 0F Center (existing aggregate items)
        [Category("MTFHVAR")]
        public RecipeBase MTF_HV_H_Center_0F { get => _MTF_HV_H_Center_0F; set { _MTF_HV_H_Center_0F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_H_Center_0F = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF_HV_V_Center_0F { get => _MTF_HV_V_Center_0F; set { _MTF_HV_V_Center_0F = value; OnPropertyChanged(); } }
        private RecipeBase _MTF_HV_V_Center_0F = new RecipeBase(0.5, 0);

        // 0F Center (per-channel: H1, H2, V1, V2 + horizontal, Vertical)
        [Category("MTFHVAR")]
        public RecipeBase MTF0F_Center_H1 { get => _MTF0F_Center_H1; set { _MTF0F_Center_H1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0F_Center_H1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0F_Center_H2 { get => _MTF0F_Center_H2; set { _MTF0F_Center_H2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0F_Center_H2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0F_Center_V1 { get => _MTF0F_Center_V1; set { _MTF0F_Center_V1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0F_Center_V1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0F_Center_V2 { get => _MTF0F_Center_V2; set { _MTF0F_Center_V2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0F_Center_V2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0F_Center_horizontal { get => _MTF0F_Center_horizontal; set { _MTF0F_Center_horizontal = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0F_Center_horizontal = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0F_Center_Vertical { get => _MTF0F_Center_Vertical; set { _MTF0F_Center_Vertical = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0F_Center_Vertical = new RecipeBase(0.5, 0);

        // 0.4F LeftUp
        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftUp_H1 { get => _MTF0_4F_LeftUp_H1; set { _MTF0_4F_LeftUp_H1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftUp_H1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftUp_H2 { get => _MTF0_4F_LeftUp_H2; set { _MTF0_4F_LeftUp_H2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftUp_H2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftUp_V1 { get => _MTF0_4F_LeftUp_V1; set { _MTF0_4F_LeftUp_V1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftUp_V1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftUp_V2 { get => _MTF0_4F_LeftUp_V2; set { _MTF0_4F_LeftUp_V2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftUp_V2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftUp_horizontal { get => _MTF0_4F_LeftUp_horizontal; set { _MTF0_4F_LeftUp_horizontal = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftUp_horizontal = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftUp_Vertical { get => _MTF0_4F_LeftUp_Vertical; set { _MTF0_4F_LeftUp_Vertical = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftUp_Vertical = new RecipeBase(0.5, 0);

        // 0.4F RightUp
        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightUp_H1 { get => _MTF0_4F_RightUp_H1; set { _MTF0_4F_RightUp_H1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightUp_H1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightUp_H2 { get => _MTF0_4F_RightUp_H2; set { _MTF0_4F_RightUp_H2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightUp_H2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightUp_V1 { get => _MTF0_4F_RightUp_V1; set { _MTF0_4F_RightUp_V1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightUp_V1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightUp_V2 { get => _MTF0_4F_RightUp_V2; set { _MTF0_4F_RightUp_V2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightUp_V2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightUp_horizontal { get => _MTF0_4F_RightUp_horizontal; set { _MTF0_4F_RightUp_horizontal = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightUp_horizontal = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightUp_Vertical { get => _MTF0_4F_RightUp_Vertical; set { _MTF0_4F_RightUp_Vertical = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightUp_Vertical = new RecipeBase(0.5, 0);

        // 0.4F RightDown
        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightDown_H1 { get => _MTF0_4F_RightDown_H1; set { _MTF0_4F_RightDown_H1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightDown_H1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightDown_H2 { get => _MTF0_4F_RightDown_H2; set { _MTF0_4F_RightDown_H2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightDown_H2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightDown_V1 { get => _MTF0_4F_RightDown_V1; set { _MTF0_4F_RightDown_V1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightDown_V1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightDown_V2 { get => _MTF0_4F_RightDown_V2; set { _MTF0_4F_RightDown_V2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightDown_V2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightDown_horizontal { get => _MTF0_4F_RightDown_horizontal; set { _MTF0_4F_RightDown_horizontal = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightDown_horizontal = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_RightDown_Vertical { get => _MTF0_4F_RightDown_Vertical; set { _MTF0_4F_RightDown_Vertical = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_RightDown_Vertical = new RecipeBase(0.5, 0);

        // 0.4F LeftDown
        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftDown_H1 { get => _MTF0_4F_LeftDown_H1; set { _MTF0_4F_LeftDown_H1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftDown_H1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftDown_H2 { get => _MTF0_4F_LeftDown_H2; set { _MTF0_4F_LeftDown_H2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftDown_H2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftDown_V1 { get => _MTF0_4F_LeftDown_V1; set { _MTF0_4F_LeftDown_V1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftDown_V1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftDown_V2 { get => _MTF0_4F_LeftDown_V2; set { _MTF0_4F_LeftDown_V2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftDown_V2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftDown_horizontal { get => _MTF0_4F_LeftDown_horizontal; set { _MTF0_4F_LeftDown_horizontal = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftDown_horizontal = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_4F_LeftDown_Vertical { get => _MTF0_4F_LeftDown_Vertical; set { _MTF0_4F_LeftDown_Vertical = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_4F_LeftDown_Vertical = new RecipeBase(0.5, 0);

        // 0.8F LeftUp
        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftUp_H1 { get => _MTF0_8F_LeftUp_H1; set { _MTF0_8F_LeftUp_H1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftUp_H1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftUp_H2 { get => _MTF0_8F_LeftUp_H2; set { _MTF0_8F_LeftUp_H2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftUp_H2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftUp_V1 { get => _MTF0_8F_LeftUp_V1; set { _MTF0_8F_LeftUp_V1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftUp_V1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftUp_V2 { get => _MTF0_8F_LeftUp_V2; set { _MTF0_8F_LeftUp_V2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftUp_V2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftUp_horizontal { get => _MTF0_8F_LeftUp_horizontal; set { _MTF0_8F_LeftUp_horizontal = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftUp_horizontal = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftUp_Vertical { get => _MTF0_8F_LeftUp_Vertical; set { _MTF0_8F_LeftUp_Vertical = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftUp_Vertical = new RecipeBase(0.5, 0);

        // 0.8F RightUp
        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightUp_H1 { get => _MTF0_8F_RightUp_H1; set { _MTF0_8F_RightUp_H1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightUp_H1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightUp_H2 { get => _MTF0_8F_RightUp_H2; set { _MTF0_8F_RightUp_H2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightUp_H2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightUp_V1 { get => _MTF0_8F_RightUp_V1; set { _MTF0_8F_RightUp_V1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightUp_V1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightUp_V2 { get => _MTF0_8F_RightUp_V2; set { _MTF0_8F_RightUp_V2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightUp_V2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightUp_horizontal { get => _MTF0_8F_RightUp_horizontal; set { _MTF0_8F_RightUp_horizontal = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightUp_horizontal = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightUp_Vertical { get => _MTF0_8F_RightUp_Vertical; set { _MTF0_8F_RightUp_Vertical = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightUp_Vertical = new RecipeBase(0.5, 0);

        // 0.8F RightDown
        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightDown_H1 { get => _MTF0_8F_RightDown_H1; set { _MTF0_8F_RightDown_H1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightDown_H1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightDown_H2 { get => _MTF0_8F_RightDown_H2; set { _MTF0_8F_RightDown_H2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightDown_H2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightDown_V1 { get => _MTF0_8F_RightDown_V1; set { _MTF0_8F_RightDown_V1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightDown_V1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightDown_V2 { get => _MTF0_8F_RightDown_V2; set { _MTF0_8F_RightDown_V2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightDown_V2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightDown_horizontal { get => _MTF0_8F_RightDown_horizontal; set { _MTF0_8F_RightDown_horizontal = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightDown_horizontal = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_RightDown_Vertical { get => _MTF0_8F_RightDown_Vertical; set { _MTF0_8F_RightDown_Vertical = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_RightDown_Vertical = new RecipeBase(0.5, 0);

        // 0.8F LeftDown
        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftDown_H1 { get => _MTF0_8F_LeftDown_H1; set { _MTF0_8F_LeftDown_H1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftDown_H1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftDown_H2 { get => _MTF0_8F_LeftDown_H2; set { _MTF0_8F_LeftDown_H2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftDown_H2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftDown_V1 { get => _MTF0_8F_LeftDown_V1; set { _MTF0_8F_LeftDown_V1 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftDown_V1 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftDown_V2 { get => _MTF0_8F_LeftDown_V2; set { _MTF0_8F_LeftDown_V2 = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftDown_V2 = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftDown_horizontal { get => _MTF0_8F_LeftDown_horizontal; set { _MTF0_8F_LeftDown_horizontal = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftDown_horizontal = new RecipeBase(0.5, 0);

        [Category("MTFHVAR")]
        public RecipeBase MTF0_8F_LeftDown_Vertical { get => _MTF0_8F_LeftDown_Vertical; set { _MTF0_8F_LeftDown_Vertical = value; OnPropertyChanged(); } }
        private RecipeBase _MTF0_8F_LeftDown_Vertical = new RecipeBase(0.5, 0);
    }
}