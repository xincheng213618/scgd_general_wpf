namespace CVImageChannelLib;

public class OpenCvMatTools
{
	public static int GetMatDepth(int bpp)
	{
		int result = 0;
		switch (bpp)
		{
		case 8:
			result = 0;
			break;
		case 16:
			result = 2;
			break;
		case 32:
			result = 5;
			break;
		case 64:
			result = 6;
			break;
		}
		return result;
	}
}
