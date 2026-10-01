using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using LlamaServerLauncher.Models;
using LlamaServerLauncher.Resources;
using LlamaServerLauncher.ViewModels;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LlamaServerLauncher.Controls;

public partial class HistoryTextBox : UserControl
{
    public static readonly StyledProperty<string?> PropertyNameProperty =
        AvaloniaProperty.Register<HistoryTextBox, string?>(nameof(PropertyName));

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<HistoryTextBox, string?>(nameof(PlaceholderText));

    public static readonly StyledProperty<bool> ShowClearButtonProperty =
        AvaloniaProperty.Register<HistoryTextBox, bool>(nameof(ShowClearButton), defaultValue: true);

    public string? PropertyName
    {
        get => GetValue(PropertyNameProperty);
        set => SetValue(PropertyNameProperty, value);
    }

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public bool ShowClearButton
    {
        get => GetValue(ShowClearButtonProperty);
        set => SetValue(ShowClearButtonProperty, value);
    }

    private const double HistoryMenuBorder = 2;
    private const double HistoryMenuItemChrome = 22;
    private const double HistoryMenuWindowMargin = 16;

    private IDisposable? _textBinding;
    private ArgType? _filterType;
    private string _lastValidText = "";
    private bool _suppressValidation;

    public HistoryTextBox()
    {
        InitializeComponent();
        InnerTextBox.TextChanged += OnInnerTextChanged;
        UpdateClearButton();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ApplyTextBinding();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PropertyNameProperty)
            ApplyTextBinding();
        else if (change.Property == PlaceholderTextProperty)
            InnerTextBox.PlaceholderText = change.GetNewValue<string?>();
        else if (change.Property == IsEnabledProperty)
            InnerTextBox.IsEnabled = change.GetNewValue<bool>();
        else if (change.Property == ShowClearButtonProperty)
            UpdateClearButton();
    }

    private void UpdateClearButton()
    {
        ClearButton.IsVisible = ShowClearButton && !string.IsNullOrEmpty(InnerTextBox.Text);
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        InnerTextBox.Text = string.Empty;
        InnerTextBox.Focus();
    }

    private void ApplyTextBinding()
    {
        _textBinding?.Dispose();
        _textBinding = null;

        // Determine the input filter from the argument type of the bound property.
        _filterType = PropertyName != null ? ServerConfiguration.GetArgType(PropertyName) : null;
        _lastValidText = InnerTextBox.Text ?? "";

        if (PropertyName != null && DataContext != null)
        {
            _textBinding = InnerTextBox.Bind(TextBox.TextProperty,
                new Binding(PropertyName) { Mode = BindingMode.TwoWay });
        }
    }

    private void OnInnerTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateClearButton();
        if (_suppressValidation) return;
        // Only Int/Double arguments are filtered; everything else accepts free text.
        if (_filterType != ArgType.Int && _filterType != ArgType.Double) return;

        var text = InnerTextBox.Text ?? "";
        if (IsValidNumeric(text, _filterType.Value))
        {
            _lastValidText = text;
            return;
        }

        // Reject the change: restore the last valid text and keep the caret sensible.
        var caret = InnerTextBox.CaretIndex;
        var removed = text.Length - _lastValidText.Length;
        _suppressValidation = true;
        InnerTextBox.Text = _lastValidText;
        InnerTextBox.CaretIndex = Math.Clamp(caret - (removed > 0 ? removed : 0), 0, _lastValidText.Length);
        _suppressValidation = false;
    }

    private static bool IsValidNumeric(string text, ArgType type)
    {
        if (text.Length == 0) return true; // empty means "not set"
        // Allow partial input while typing: an optional leading minus, digits, and
        // (for Double) a single decimal point. Final validity is enforced by parsing.
        return type == ArgType.Int
            ? Regex.IsMatch(text, @"^-?\d*$")
            : Regex.IsMatch(text, @"^-?\d*\.?\d*$");
    }

    private void HistoryButton_Click(object? sender, RoutedEventArgs e)
    {
        // Try multiple strategies to find the ViewModel
        var vm = this.DataContext as MainViewModel;
        if (vm == null)
            vm = (TopLevel.GetTopLevel(this) as Window)?.DataContext as MainViewModel;
        if (vm == null)
        {
            // Walk up the visual tree
            var parent = this.GetVisualParent();
            while (parent != null)
            {
                if (parent is Window w && w.DataContext is MainViewModel mvm)
                { vm = mvm; break; }
                parent = parent.GetVisualParent();
            }
        }
        if (vm == null) return;

        var values = vm.GetRecentValues(PropertyName ?? "");
        if (values == null || values.Count == 0) return;

        var minWidth = Math.Max(0, Bounds.Width - HistoryMenuBorder);
        var maxWidth = minWidth;
        var topLevel = TopLevel.GetTopLevel(this);
        var origin = topLevel != null ? this.TranslatePoint(new Point(0, 0), topLevel) : null;
        if (topLevel != null && origin != null)
            maxWidth = Math.Max(minWidth, topLevel.Bounds.Width - origin.Value.X - HistoryMenuWindowMargin - HistoryMenuBorder);

        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        flyout.FlyoutPresenterClasses.Add("history");
        foreach (var v in values)
        {
            var val = v;
            var header = new TextBlock { Text = val, TextTrimming = TextTrimming.PathSegmentEllipsis };
            var item = new MenuItem { Header = header, MinWidth = minWidth, MaxWidth = maxWidth };
            header.Measure(Size.Infinity);
            if (header.DesiredSize.Width > maxWidth - HistoryMenuItemChrome)
                ToolTip.SetTip(item, val);
            item.Click += (_, _) => InnerTextBox.Text = val;
            flyout.Items.Add(item);
        }

        flyout.ShowAt(InnerTextBox);
    }
}
