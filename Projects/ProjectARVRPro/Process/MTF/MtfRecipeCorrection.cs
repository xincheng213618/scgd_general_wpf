using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Recipe;

namespace ProjectARVRPro.Process.MTF;

internal readonly struct MtfRecipeCorrection
{
    private readonly double? maximum;

    public MtfRecipeCorrection(string? resultParameters)
    {
        if (string.IsNullOrWhiteSpace(resultParameters)) return;

        try
        {
            var snapshot = JObject.Parse(resultParameters);
            // Local MTF records its effective parameters. A legacy snapshot must explicitly identify ratio mode.
            JObject? parameters = snapshot["Algorithm"] is JValue { Value: "StripeMtf" }
                ? snapshot["Parameters"] as JObject : snapshot;
            if (ReferenceEquals(parameters, snapshot) && !JToken.DeepEquals(snapshot["CalcMethod"], new JValue(0))) return;
            if (parameters?["PercentageDisplay"] is JValue { Value: bool percentage })
                maximum = percentage ? 100 : 1;
        }
        catch (JsonException)
        {
            // Historical or vendor results may lack a usable parameter snapshot.
        }
    }

    public double Apply(double rawValue, RecipeBase recipe, string pointName)
    {
        double value = recipe.Apply(rawValue);
        if (!double.IsFinite(value) || (maximum is double max && (value < 0 || value > max)))
        {
            string range = maximum is double limit ? $"0～{limit}" : "有限数值";
            throw new InvalidOperationException($"MTF 修正越界 [{pointName}]，原值={rawValue}，修正值={value}，Fix={recipe.Fix}，B={recipe.B}，允许范围={range}。");
        }
        return value;
    }
}
