using ColorVision.Engine.Templates.Jsons.MTF2;
using ProjectARVRPro.Recipe;

namespace ProjectARVRPro.Process.MTF.MTF07
{
    internal static class MTF07DynamicResultBuilder
    {
        public static void PopulateItem(ObjectiveTestItem item, MTFItem mtf, RecipeBase recipe, string showConfig, string unit, MtfRecipeCorrection correction)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(mtf);
            ArgumentNullException.ThrowIfNull(recipe);

            double value = correction.Apply(mtf.mtfValue ?? 0, recipe, mtf.name);
            item.Unit = unit;
            item.Value = value;
            item.TestValue = value.ToString(showConfig);
            item.LowLimit = recipe.Min;
            item.UpLimit = recipe.Max;
        }
    }
}
