namespace ProjectKB
{
    public static class BacklightAutotuneService
    {
        public const string SourceNone = "None";
        public const string SourceRecipe = "Recipe";

        private const double DisabledQValue = -1;
        private const double DefaultSteepness = 5;

        public static void Apply(KBItemMaster result, KBRecipeConfig recipe)
        {
            result.BacklightAutotuneEnabled = recipe.EnableBacklightAutotune;
            result.BacklightAutotuneApplied = false;
            result.BacklightAutotuneSource = SourceNone;
            result.BacklightAutotuneSteepness = NormalizeSteepness(recipe.BacklightAutotuneSteepness);

            result.AvgLvRaw = result.AvgLv;
            result.MinLvRaw = result.MinLv;
            result.LvUniformityRaw = result.LvUniformity;

            if (!recipe.EnableBacklightAutotune)
            {
                ResetUnusedSpec(result);
                SetAdjustedToRaw(result);
                return;
            }

            result.BacklightAutotuneSource = SourceRecipe;
            result.AvgLvQ1 = recipe.BacklightAutotuneAvgLvQ1;
            result.AvgLvQ3 = recipe.BacklightAutotuneAvgLvQ3;
            result.MinLvQ1 = recipe.BacklightAutotuneMinLvQ1;
            result.MinLvQ3 = recipe.BacklightAutotuneMinLvQ3;
            result.UniformityQ1 = recipe.BacklightAutotuneUniformityQ1;
            result.UniformityQ3 = recipe.BacklightAutotuneUniformityQ3;

            AutotuneMetricResult avgLv = CorrectMetric(result.AvgLvRaw, result.AvgLvQ1, result.AvgLvQ3, result.BacklightAutotuneSteepness);
            AutotuneMetricResult minLv = CorrectMetric(result.MinLvRaw, result.MinLvQ1, result.MinLvQ3, result.BacklightAutotuneSteepness);
            AutotuneMetricResult uniformity = CorrectMetric(result.LvUniformityRaw * 100, result.UniformityQ1, result.UniformityQ3, result.BacklightAutotuneSteepness);

            result.AvgLvAdjusted = avgLv.AdjustedValue;
            result.MinLvAdjusted = minLv.AdjustedValue;
            result.LvUniformityAdjusted = uniformity.AdjustedValue / 100;

            if (avgLv.Applied)
            {
                result.AvgLv = result.AvgLvAdjusted;
            }

            if (minLv.Applied)
            {
                result.MinLv = result.MinLvAdjusted;
            }

            if (uniformity.Applied)
            {
                result.LvUniformity = result.LvUniformityAdjusted;
            }

            result.BacklightAutotuneApplied = avgLv.Applied || minLv.Applied || uniformity.Applied;
        }

        public static double CorrectIfNeeded(double value, double q1, double q3, double steepness)
        {
            if (!HasValidBounds(q1, q3))
            {
                return value;
            }

            double iqr = q3 - q1;
            double normalized = (value - (q1 + q3) / 2) / (iqr / NormalizeSteepness(steepness));
            return q1 + (1 / (1 + Math.Exp(-normalized))) * iqr;
        }

        public static double GetOriginalAvgLv(KBItemMaster result)
        {
            return result.BacklightAutotuneEnabled ? result.AvgLvRaw : result.AvgLv;
        }

        public static double GetOriginalMinLv(KBItemMaster result)
        {
            return result.BacklightAutotuneEnabled ? result.MinLvRaw : result.MinLv;
        }

        public static double GetOriginalLvUniformity(KBItemMaster result)
        {
            return result.BacklightAutotuneEnabled ? result.LvUniformityRaw : result.LvUniformity;
        }

        private static AutotuneMetricResult CorrectMetric(double value, double q1, double q3, double steepness)
        {
            bool applied = HasValidBounds(q1, q3);
            double adjusted = applied ? CorrectIfNeeded(value, q1, q3, steepness) : value;
            return new AutotuneMetricResult(value, adjusted, applied);
        }

        private static bool HasValidBounds(double q1, double q3)
        {
            return q1 != DisabledQValue && q3 != DisabledQValue && q3 > q1;
        }

        private static double NormalizeSteepness(double steepness)
        {
            return steepness > 0 ? steepness : DefaultSteepness;
        }

        private static void ResetUnusedSpec(KBItemMaster result)
        {
            result.AvgLvQ1 = DisabledQValue;
            result.AvgLvQ3 = DisabledQValue;
            result.MinLvQ1 = DisabledQValue;
            result.MinLvQ3 = DisabledQValue;
            result.UniformityQ1 = DisabledQValue;
            result.UniformityQ3 = DisabledQValue;
        }

        private static void SetAdjustedToRaw(KBItemMaster result)
        {
            result.AvgLvAdjusted = result.AvgLvRaw;
            result.MinLvAdjusted = result.MinLvRaw;
            result.LvUniformityAdjusted = result.LvUniformityRaw;
        }

        private readonly record struct AutotuneMetricResult(double RawValue, double AdjustedValue, bool Applied);
    }
}
