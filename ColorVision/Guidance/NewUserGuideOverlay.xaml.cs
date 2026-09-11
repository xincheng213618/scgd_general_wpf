using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Guidance;

public partial class NewUserGuideOverlay : UserControl
{
    private NewUserGuideStep[] _steps = [];
    private int _currentStepIndex = -1;

    internal event EventHandler? GuideDismissed;

    internal bool IsGuideOpen => Visibility == Visibility.Visible;
    internal int CurrentStepIndex => _currentStepIndex;

    public NewUserGuideOverlay()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RefreshPlacement();
        ApplyLocalizedText();
    }

    internal void ShowWelcome(IEnumerable<NewUserGuideStep> steps)
    {
        _steps = steps.ToArray();
        if (_steps.Length == 0)
            return;

        _currentStepIndex = -1;
        Visibility = Visibility.Visible;
        IsHitTestVisible = true;
        FullShade.Visibility = Visibility.Visible;
        TourCanvas.Visibility = Visibility.Collapsed;
        CompletionCard.Visibility = Visibility.Collapsed;
        WelcomeCard.Visibility = Visibility.Visible;
        FocusElement(StartTourButton);
    }

    internal void StartTour()
    {
        if (_steps.Length == 0)
            return;

        WelcomeCard.Visibility = Visibility.Collapsed;
        CompletionCard.Visibility = Visibility.Collapsed;
        FullShade.Visibility = Visibility.Collapsed;
        TourCanvas.Visibility = Visibility.Visible;
        ShowStep(0);
    }

    internal void Next()
    {
        if (_currentStepIndex < 0)
        {
            StartTour();
            return;
        }
        if (_currentStepIndex + 1 >= _steps.Length)
        {
            ShowCompletion();
            return;
        }
        ShowStep(_currentStepIndex + 1);
    }

    internal void Previous()
    {
        if (_currentStepIndex <= 0)
            return;
        ShowStep(_currentStepIndex - 1);
    }

    internal void Dismiss()
    {
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
        _currentStepIndex = -1;
        _steps = [];
        GuideDismissed?.Invoke(this, EventArgs.Empty);
    }

    private void ShowStep(int index)
    {
        _currentStepIndex = index;
        NewUserGuideStep step = _steps[index];
        StepProgressText.Text = NewUserGuideText.Format("StepProgressFormat", index + 1, _steps.Length);
        StepTitleText.Text = step.Title;
        StepDescriptionText.Text = step.Description;
        PreviousButton.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = index == _steps.Length - 1
            ? NewUserGuideText.Get("FinishAction")
            : NewUserGuideText.Get("NextAction");
        RefreshPlacement();
        FocusElement(NextButton);
    }

    private void ShowCompletion()
    {
        _currentStepIndex = _steps.Length;
        FullShade.Visibility = Visibility.Visible;
        TourCanvas.Visibility = Visibility.Collapsed;
        WelcomeCard.Visibility = Visibility.Collapsed;
        CompletionCard.Visibility = Visibility.Visible;
        FocusElement(CompleteButton);
    }

    private void RefreshPlacement()
    {
        if (TourCanvas.Visibility != Visibility.Visible || _currentStepIndex < 0 || _currentStepIndex >= _steps.Length)
            return;

        FrameworkElement? target = _steps[_currentStepIndex].TargetResolver();
        Rect? targetRect = TryGetTargetRect(target);
        double cardWidth = Math.Min(332, Math.Max(280, ActualWidth - NewUserGuideLayout.EdgeMargin * 2));
        TourCard.Width = cardWidth;
        TourCard.Height = double.NaN;
        TourCard.Measure(new Size(cardWidth, Math.Max(0, ActualHeight - NewUserGuideLayout.EdgeMargin * 2)));
        Size desiredCardSize = new(cardWidth, TourCard.DesiredSize.Height);
        NewUserGuideLayoutResult layout = NewUserGuideLayout.Calculate(RenderSize, targetRect, desiredCardSize);

        SetCanvasRect(TopMask, layout.TopMask);
        SetCanvasRect(LeftMask, layout.LeftMask);
        SetCanvasRect(RightMask, layout.RightMask);
        SetCanvasRect(BottomMask, layout.BottomMask);
        SetCanvasRect(SpotlightBorder, layout.Spotlight);
        SpotlightBorder.Visibility = layout.Spotlight.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        SetCanvasRect(TourCard, layout.Card);
    }

    private Rect? TryGetTargetRect(FrameworkElement? target)
    {
        if (target is not { IsVisible: true, ActualWidth: > 0, ActualHeight: > 0 })
            return null;

        try
        {
            GeneralTransform transform = target.TransformToVisual(this);
            return transform.TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void SetCanvasRect(FrameworkElement element, Rect rect)
    {
        Canvas.SetLeft(element, rect.IsEmpty ? 0 : rect.X);
        Canvas.SetTop(element, rect.IsEmpty ? 0 : rect.Y);
        element.Width = rect.IsEmpty ? 0 : rect.Width;
        element.Height = rect.IsEmpty ? 0 : rect.Height;
    }

    private void ApplyLocalizedText()
    {
        WelcomeTitleText.Text = NewUserGuideText.Get("WelcomeTitle");
        WelcomeDescriptionText.Text = NewUserGuideText.Get("WelcomeDescription");
        DismissWelcomeButton.Content = NewUserGuideText.Get("DismissWelcomeAction");
        StartTourButton.Content = NewUserGuideText.Get("StartTourAction");
        SkipButton.Content = NewUserGuideText.Get("SkipAction");
        PreviousButton.Content = NewUserGuideText.Get("PreviousAction");
        CompletionTitleText.Text = NewUserGuideText.Get("CompletionTitle");
        CompletionDescriptionText.Text = NewUserGuideText.Get("CompletionDescription");
        CompleteButton.Content = NewUserGuideText.Get("CompleteAction");
    }

    private static void FocusElement(UIElement element)
    {
        _ = element.Dispatcher.BeginInvoke(new Action(() =>
        {
            _ = element.Focus();
            Keyboard.Focus(element);
        }));
    }

    private void GuideOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismiss();
            e.Handled = true;
        }
    }

    private void StartTourButton_Click(object sender, RoutedEventArgs e) => StartTour();
    private void DismissWelcomeButton_Click(object sender, RoutedEventArgs e) => Dismiss();
    private void PreviousButton_Click(object sender, RoutedEventArgs e) => Previous();
    private void NextButton_Click(object sender, RoutedEventArgs e) => Next();
    private void SkipButton_Click(object sender, RoutedEventArgs e) => Dismiss();
    private void CompleteButton_Click(object sender, RoutedEventArgs e) => Dismiss();
}
