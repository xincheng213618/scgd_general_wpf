using System.Collections.Generic;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.XR;

public struct GhostResult(int rows, int cols, PointFloat[] ledCenters, List<PointInt[]> lEDPixels, float[] lEDBlobGray, float[] ghostAverageGray, int[] singleLedPixelNum, int[] singleGhostPixelNum, List<PointInt[]> ghostPixels)
{
	public int Rows = rows;

	public int Cols = cols;

	public PointFloat[] LedCenters = ledCenters;

	public List<PointInt[]> LEDPixels = lEDPixels;

	public float[] LEDBlobGray = lEDBlobGray;

	public float[] ghostAverageGray = ghostAverageGray;

	public int[] singleLedPixelNum = singleLedPixelNum;

	public int[] singleGhostPixelNum = singleGhostPixelNum;

	public List<PointInt[]> GhostPixels = ghostPixels;
}
