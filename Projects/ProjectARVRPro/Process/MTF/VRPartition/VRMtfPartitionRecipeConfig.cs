using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

#pragma warning disable CA1707 // Region names retain the retired LUX metric contract.

namespace ProjectARVRPro.Process.MTF.VRPartition;

public sealed class VRMtfPartitionRecipeConfig : ViewModelBase, IRecipeConfig
{
    [Category("A区"), DisplayName("最小值")]
    public RecipeBase Region_A_Min { get => _regionAMin; set { _regionAMin = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionAMin = new(0, 0);

    [Category("A区"), DisplayName("最大值")]
    public RecipeBase Region_A_Max { get => _regionAMax; set { _regionAMax = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionAMax = new(0, 0);

    [Category("A区"), DisplayName("平均值")]
    public RecipeBase Region_A_Average { get => _regionAAverage; set { _regionAAverage = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionAAverage = new(0, 0);

    [Category("B区"), DisplayName("最小值")]
    public RecipeBase Region_B_Min { get => _regionBMin; set { _regionBMin = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionBMin = new(0, 0);

    [Category("B区"), DisplayName("最大值")]
    public RecipeBase Region_B_Max { get => _regionBMax; set { _regionBMax = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionBMax = new(0, 0);

    [Category("B区"), DisplayName("平均值")]
    public RecipeBase Region_B_Average { get => _regionBAverage; set { _regionBAverage = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionBAverage = new(0, 0);

    [Category("C区"), DisplayName("最小值")]
    public RecipeBase Region_C_Min { get => _regionCMin; set { _regionCMin = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionCMin = new(0, 0);

    [Category("C区"), DisplayName("最大值")]
    public RecipeBase Region_C_Max { get => _regionCMax; set { _regionCMax = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionCMax = new(0, 0);

    [Category("C区"), DisplayName("平均值")]
    public RecipeBase Region_C_Average { get => _regionCAverage; set { _regionCAverage = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionCAverage = new(0, 0);

    [Category("D区"), DisplayName("最小值")]
    public RecipeBase Region_D_Min { get => _regionDMin; set { _regionDMin = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionDMin = new(0, 0);

    [Category("D区"), DisplayName("最大值")]
    public RecipeBase Region_D_Max { get => _regionDMax; set { _regionDMax = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionDMax = new(0, 0);

    [Category("D区"), DisplayName("平均值")]
    public RecipeBase Region_D_Average { get => _regionDAverage; set { _regionDAverage = value ?? new(); OnPropertyChanged(); } }
    private RecipeBase _regionDAverage = new(0, 0);

    public RecipeBase GetRecipe(string name) => name switch
    {
        nameof(Region_A_Min) => Region_A_Min,
        nameof(Region_A_Max) => Region_A_Max,
        nameof(Region_A_Average) => Region_A_Average,
        nameof(Region_B_Min) => Region_B_Min,
        nameof(Region_B_Max) => Region_B_Max,
        nameof(Region_B_Average) => Region_B_Average,
        nameof(Region_C_Min) => Region_C_Min,
        nameof(Region_C_Max) => Region_C_Max,
        nameof(Region_C_Average) => Region_C_Average,
        nameof(Region_D_Min) => Region_D_Min,
        nameof(Region_D_Max) => Region_D_Max,
        nameof(Region_D_Average) => Region_D_Average,
        _ => throw new ArgumentException($"未知VR分区指标: {name}", nameof(name))
    };
}
