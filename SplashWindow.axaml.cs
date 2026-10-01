using Avalonia.Controls;
using Avalonia.Media;

namespace LlamaServerLauncher;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (ActualTransparencyLevel == WindowTransparencyLevel.None
                && this.TryFindResource("WindowBackgroundBrush", out var brush) && brush is IBrush background)
                Background = background;
        };
    }
}
