sampler2D Input : register(s0);
sampler2D Lut : register(s1);

#ifndef USE_PSEUDO_COLOR
#define USE_PSEUDO_COLOR 0
#endif

#ifndef FILTER_THRESHOLD_MODE
#define FILTER_THRESHOLD_MODE 0
#endif

float ChannelMode : register(c0);
float RedGain : register(c1);
float GreenGain : register(c2);
float BlueGain : register(c3);
float RedOffset : register(c4);
float GreenOffset : register(c5);
float BlueOffset : register(c6);
float Brightness : register(c7);
float Contrast : register(c8);
float Gamma : register(c9);
float Saturation : register(c10);
float Invert : register(c11);
float ThresholdMode : register(c12);
float Threshold : register(c13);
float ThresholdLow : register(c14);
float ThresholdHigh : register(c15);
float RangeLow : register(c16);
float RangeHigh : register(c17);
float HighlightOpacity : register(c18);
float PseudoColorMode : register(c19);
float PseudoMin : register(c20);
float PseudoMax : register(c21);

float LumaValue(float3 rgb)
{
    return dot(rgb, float3(0.299, 0.587, 0.114));
}

float3 ChannelSelect(float3 rgb)
{
    float luma = LumaValue(rgb);

    if (ChannelMode < 0.5)
    {
        return rgb;
    }
    if (ChannelMode < 1.5)
    {
        return rgb.rrr;
    }
    if (ChannelMode < 2.5)
    {
        return rgb.ggg;
    }
    if (ChannelMode < 3.5)
    {
        return rgb.bbb;
    }

    return luma.xxx;
}

float3 ApplyBasicFilter(float3 sourceRgb)
{
    float3 rgb = ChannelSelect(sourceRgb);

    rgb = rgb * float3(RedGain, GreenGain, BlueGain) + float3(RedOffset, GreenOffset, BlueOffset);
    rgb = saturate(rgb);

    rgb = (rgb - 0.5) * Contrast + 0.5 + Brightness;
    rgb = saturate(rgb);

    float luma = LumaValue(rgb);
    rgb = luma.xxx + (rgb - luma.xxx) * Saturation;
    rgb = saturate(rgb);

    rgb = pow(max(rgb, 0.0001), 1.0 / max(Gamma, 0.0001));
    rgb = lerp(rgb, 1.0 - rgb, step(0.5, Invert));

    return rgb;
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 source = tex2D(Input, uv);
    float3 rgb = ApplyBasicFilter(source.rgb);
    float analysisLuma = LumaValue(rgb);
    float alpha = source.a;

#if USE_PSEUDO_COLOR
    float pseudoValue = saturate((analysisLuma - PseudoMin) / max(PseudoMax - PseudoMin, 0.0001));
    rgb = tex2D(Lut, float2(0.5, 1.0 - pseudoValue)).rgb;
#endif

#if FILTER_THRESHOLD_MODE == 1
    float thresholdValue = step(Threshold, analysisLuma);
    rgb = thresholdValue.xxx;
#elif FILTER_THRESHOLD_MODE == 2
    alpha *= step(Threshold, analysisLuma);
#elif FILTER_THRESHOLD_MODE == 3
    float overMask = step(ThresholdHigh, analysisLuma);
    rgb = lerp(rgb, float3(1.0, 0.05, 0.0), overMask * HighlightOpacity);
#elif FILTER_THRESHOLD_MODE == 4
    float underMask = 1.0 - step(ThresholdLow, analysisLuma);
    rgb = lerp(rgb, float3(0.0, 0.35, 1.0), underMask * HighlightOpacity);
#elif FILTER_THRESHOLD_MODE == 5
    float rangeMask = step(RangeLow, analysisLuma) * (1.0 - step(RangeHigh, analysisLuma));
    rgb = lerp(rgb, float3(0.0, 1.0, 0.15), rangeMask * HighlightOpacity);
#elif FILTER_THRESHOLD_MODE == 6
    float underMask = 1.0 - step(ThresholdLow, analysisLuma);
    float overMask = step(ThresholdHigh, analysisLuma);
    rgb = lerp(rgb, float3(0.0, 0.35, 1.0), underMask * HighlightOpacity);
    rgb = lerp(rgb, float3(1.0, 0.05, 0.0), overMask * HighlightOpacity);
#endif

    return float4(saturate(rgb), alpha);
}
