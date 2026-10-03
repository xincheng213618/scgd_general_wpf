using System;
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
		Canvas = ColorTranslator.FromHtml(isDark ? "#202124" : "#F3F5F8");
		Surface = ColorTranslator.FromHtml(isDark ? "#303238" : "#FFFFFF");
		Text = ColorTranslator.FromHtml(isDark ? "#ECEEF2" : "#202936");
		SecondaryText = ColorTranslator.FromHtml(isDark ? "#B4BBC6" : "#626E7F");
		Border = ColorTranslator.FromHtml(isDark ? "#535862" : "#C4CBD5");
		Accent = ColorTranslator.FromHtml(isDark ? "#78ACFF" : "#2563B8");
		Success = ColorTranslator.FromHtml(isDark ? "#62C99A" : "#21805A");
		Error = ColorTranslator.FromHtml(isDark ? "#FF8B8B" : "#C43D4B");
		Warning = ColorTranslator.FromHtml(isDark ? "#E4BC69" : "#976614");
	}

	public bool IsDark { get; }
	public Color Canvas { get; }
	public Color Surface { get; }
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

	public Color TitleSurface(Color accent) => Blend(Surface, ResolveAccent(accent), IsDark ? 0.19f : 0.09f);

	public Color ResolveText(Color color) => color.ToArgb() == Color.White.ToArgb()
		? Text : color.ToArgb() == Color.Gray.ToArgb() ? SecondaryText : color;

	internal static Color Blend(Color background, Color foreground, float amount) => Color.FromArgb(
		(int)Math.Round(background.R + (foreground.R - background.R) * amount),
		(int)Math.Round(background.G + (foreground.G - background.G) * amount),
		(int)Math.Round(background.B + (foreground.B - background.B) * amount));
}

public enum STNodeExecutionState
{
	Idle,
	Running,
	Succeeded,
	Failed,
	Canceled
}
