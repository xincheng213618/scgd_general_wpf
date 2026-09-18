namespace ProjectARVRPro.Recipe
{
    /// <summary>
    /// 十字 RGB 分离结果中记录的判定参数快照。Fix/B 分别为线性修正的 K/B。
    /// </summary>
    public class RecipeBase
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Fix { get; set; } = 1;
        public double B { get; set; }
    }
}
