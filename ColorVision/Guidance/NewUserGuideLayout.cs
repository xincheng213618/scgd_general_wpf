using System;
using System.Windows;

namespace ColorVision.Guidance;

internal readonly record struct NewUserGuideLayoutResult(
    Rect Spotlight,
    Rect TopMask,
    Rect LeftMask,
    Rect RightMask,
    Rect BottomMask,
    Rect Card);

internal static class NewUserGuideLayout
{
    internal const double SpotlightPadding = 8;
    internal const double CardGap = 18;
    internal const double EdgeMargin = 16;

    internal static NewUserGuideLayoutResult Calculate(
        Size overlaySize,
        Rect? target,
        Size cardSize)
    {
        Rect bounds = new(0, 0, Math.Max(0, overlaySize.Width), Math.Max(0, overlaySize.Height));
        Size constrainedCard = new(
            Math.Min(cardSize.Width, Math.Max(0, bounds.Width - EdgeMargin * 2)),
            Math.Min(cardSize.Height, Math.Max(0, bounds.Height - EdgeMargin * 2)));

        if (!target.HasValue || target.Value.IsEmpty || bounds.Width < 1 || bounds.Height < 1)
        {
            return new NewUserGuideLayoutResult(
                Rect.Empty,
                bounds,
                Rect.Empty,
                Rect.Empty,
                Rect.Empty,
                CenterCard(bounds, constrainedCard));
        }

        Rect targetRect = target.Value;
        targetRect.Inflate(SpotlightPadding, SpotlightPadding);
        targetRect.Intersect(bounds);
        if (targetRect.IsEmpty || targetRect.Width < 1 || targetRect.Height < 1)
        {
            return new NewUserGuideLayoutResult(
                Rect.Empty,
                bounds,
                Rect.Empty,
                Rect.Empty,
                Rect.Empty,
                CenterCard(bounds, constrainedCard));
        }

        Rect top = new(0, 0, bounds.Width, targetRect.Top);
        Rect bottom = new(0, targetRect.Bottom, bounds.Width, Math.Max(0, bounds.Height - targetRect.Bottom));
        Rect left = new(0, targetRect.Top, targetRect.Left, targetRect.Height);
        Rect right = new(targetRect.Right, targetRect.Top, Math.Max(0, bounds.Width - targetRect.Right), targetRect.Height);
        Rect card = PlaceCard(bounds, targetRect, constrainedCard);
        return new NewUserGuideLayoutResult(targetRect, top, left, right, bottom, card);
    }

    private static Rect PlaceCard(Rect bounds, Rect spotlight, Size card)
    {
        if (bounds.Right - spotlight.Right >= card.Width + CardGap + EdgeMargin)
            return ClampCard(bounds, new Rect(spotlight.Right + CardGap, CenterY(spotlight, card), card.Width, card.Height));
        if (spotlight.Left >= card.Width + CardGap + EdgeMargin)
            return ClampCard(bounds, new Rect(spotlight.Left - CardGap - card.Width, CenterY(spotlight, card), card.Width, card.Height));
        if (bounds.Bottom - spotlight.Bottom >= card.Height + CardGap + EdgeMargin)
            return ClampCard(bounds, new Rect(CenterX(spotlight, card), spotlight.Bottom + CardGap, card.Width, card.Height));
        if (spotlight.Top >= card.Height + CardGap + EdgeMargin)
            return ClampCard(bounds, new Rect(CenterX(spotlight, card), spotlight.Top - CardGap - card.Height, card.Width, card.Height));
        return CenterCard(bounds, card);
    }

    private static Rect CenterCard(Rect bounds, Size card) =>
        ClampCard(bounds, new Rect(
            bounds.Left + (bounds.Width - card.Width) / 2,
            bounds.Top + (bounds.Height - card.Height) / 2,
            card.Width,
            card.Height));

    private static Rect ClampCard(Rect bounds, Rect card)
    {
        double maxX = Math.Max(EdgeMargin, bounds.Right - EdgeMargin - card.Width);
        double maxY = Math.Max(EdgeMargin, bounds.Bottom - EdgeMargin - card.Height);
        return new Rect(
            Math.Clamp(card.X, EdgeMargin, maxX),
            Math.Clamp(card.Y, EdgeMargin, maxY),
            card.Width,
            card.Height);
    }

    private static double CenterX(Rect target, Size card) => target.Left + (target.Width - card.Width) / 2;
    private static double CenterY(Rect target, Size card) => target.Top + (target.Height - card.Height) / 2;
}
