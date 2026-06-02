// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using System.Windows;

namespace ColorVision.Themes.Controls;

/// <summary>
/// Use <see cref="ToggleSwitch"/> to present users with two mutually exclusive options, such as on/off.
/// </summary>
public class ToggleSwitch : System.Windows.Controls.Primitives.ToggleButton
{
    /// <summary>Identifies the <see cref="OffContent"/> dependency property.</summary>
    public static readonly DependencyProperty OffContentProperty = DependencyProperty.Register(
        nameof(OffContent),
        typeof(object),
        typeof(ToggleSwitch),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="OnContent"/> dependency property.</summary>
    public static readonly DependencyProperty OnContentProperty = DependencyProperty.Register(
        nameof(OnContent),
        typeof(object),
        typeof(ToggleSwitch),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="LabelPosition"/> dependency property.</summary>
    public static readonly DependencyProperty LabelPositionProperty = DependencyProperty.Register(
        nameof(LabelPosition),
        typeof(ElementPlacement),
        typeof(ToggleSwitch),
        new FrameworkPropertyMetadata(
            ElementPlacement.Right,
            FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsMeasure
        )
    );

    /// <summary>
    /// Gets or sets the content that should be displayed when the <see cref="ToggleSwitch"/> is off.
    /// </summary>
    [Bindable(true)]
    public object? OffContent
    {
        get => GetValue(OffContentProperty);
        set => SetValue(OffContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the content that should be displayed when the <see cref="ToggleSwitch"/> is on.
    /// </summary>
    [Bindable(true)]
    public object? OnContent
    {
        get => GetValue(OnContentProperty);
        set => SetValue(OnContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the position of the label content relative to the toggle switch.
    /// </summary>
    [Bindable(true)]
    [Category("Layout")]
    public ElementPlacement LabelPosition
    {
        get => (ElementPlacement)GetValue(LabelPositionProperty);
        set => SetValue(LabelPositionProperty, value);
    }
}
