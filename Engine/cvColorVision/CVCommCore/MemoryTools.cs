using System;

namespace CVCommCore;

public static class MemoryTools
{
	public static void CleanupMemory()
	{
		if (IsMemoryPressureHigh(2L))
		{
			GC.Collect(2, GCCollectionMode.Forced);
			GC.WaitForPendingFinalizers();
			GC.Collect(2, GCCollectionMode.Forced);
		}
	}

	private static bool IsMemoryPressureHigh(long maxMem = 2L)
	{
		long totalMemory = GC.GetTotalMemory(forceFullCollection: false);
		long num = maxMem * 1024 * 1024 * 1024;
		return (double)totalMemory > (double)num * 0.8;
	}
}
