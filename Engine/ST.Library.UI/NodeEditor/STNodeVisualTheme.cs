using System.Drawing;

namespace ST.Library.UI.NodeEditor;

/// <summary>Presentation-only colors. Node identities, saved colors and port types stay unchanged.</summary>
public sealed class STNodeVisualTheme
{
	public static STNodeVisualTheme Light { get; } = new STNodeVisualTheme(false);
	public static STNodeVisualTheme Dark { get; } = new STNodeVisualTheme(true);

	private STNodeVisualTheme(bool isDark)
	{
		IsDark = isDark;
		Canvas = ColorTranslator.FromHtml(isDark ? "#222426" : "#E7EBF0");
		Surface = ColorTranslator.FromHtml(isDark ? "#383A3E" : "#FFFFFF");
		GridMajor = ColorTranslator.FromHtml(isDark ? "#303337" : "#D1D7DF");
		GridMinor = ColorTranslator.FromHtml(isDark ? "#292C2F" : "#DEE3E9");
		Text = ColorTranslator.FromHtml(isDark ? "#ECEEF2" : "#202936");
		SecondaryText = ColorTranslator.FromHtml(isDark ? "#B4BBC6" : "#626E7F");
		Border = ColorTranslator.FromHtml(isDark ? "#484C52" : "#C8D0DB");
		Accent = ColorTranslator.FromHtml(isDark ? "#559BE0" : "#2878BF");
		Success = ColorTranslator.FromHtml(isDark ? "#47A36D" : "#328355");
		Error = ColorTranslator.FromHtml(isDark ? "#D45C5C" : "#C44949");
		Warning = ColorTranslator.FromHtml(isDark ? "#B68B2D" : "#A47A21");
	}

	public bool IsDark { get; }
	public Color Canvas { get; }
	public Color Surface { get; }
	public Color GridMajor { get; }
	public Color GridMinor { get; }
	public Color Text { get; }
	public Color SecondaryText { get; }
	public Color Border { get; }
	public Color Accent { get; }
	public Color Success { get; }
	public Color Error { get; }
	public Color Warning { get; }

	public Color ResolveAccent(Color color)
	{
		int rgb = color.ToArgb() & 0xFFFFFF;
		if (rgb == (Color.DodgerBlue.ToArgb() & 0xFFFFFF) || rgb == 0x0000FF || rgb == 0x00BFFF || rgb == 0x00FFFF)
			return Accent;
		if (rgb == 0x006400 || rgb == 0x008000 || rgb == 0x00FF00 || rgb == 0x228B22)
			return Success;
		if (rgb == 0xFF0000 || rgb == 0xB22222 || rgb == 0xA52A2A)
			return Error;
		if (rgb == 0xDAA520 || rgb == 0xFFD700 || rgb == 0xFFFF00 || rgb == 0xFFF8DC || rgb == 0xFFA500)
			return Warning;
		if (rgb == 0xFF1493)
			return ColorTranslator.FromHtml(IsDark ? "#E99AC9" : "#A43C80");
		if (rgb == 0x008B8B)
			return ColorTranslator.FromHtml(IsDark ? "#68C8CC" : "#147E85");
		if (rgb == 0x808080 || rgb == 0x708090)
			return SecondaryText;
		return Color.FromArgb(255, color);
	}

	public Color TitleSurface(Color color)
	{
		int rgb = color.ToArgb() & 0xFFFFFF;
		if (rgb == 0x1E90FF || rgb == 0x0000FF)
			return ColorTranslator.FromHtml(IsDark ? "#286FB2" : "#2676BF");
		if (rgb == 0x006400 || rgb == 0x008000 || rgb == 0x00FF00 || rgb == 0x228B22)
			return ColorTranslator.FromHtml(IsDark ? "#328657" : "#287B4D");
		return ResolveAccent(color);
	}

	public Color TitleProgress(Color color)
	{
		int rgb = color.ToArgb() & 0xFFFFFF;
		return rgb == 0x00BFFF || rgb == 0x1E90FF
			? ColorTranslator.FromHtml(IsDark ? "#4389C8" : "#438CC9") : ResolveAccent(color);
	}

	public Color ResolveText(Color color) => color.ToArgb() == Color.White.ToArgb()
		? Text : color.ToArgb() == Color.Gray.ToArgb() ? SecondaryText : color;
}
