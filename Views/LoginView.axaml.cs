using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace RelatorioGLPIApp.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void CloseWindow(object? sender, RoutedEventArgs e) => this.FindAncestorOfType<Window>()?.Close();

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
    {
        var window = this.FindAncestorOfType<Window>();
        if (window != null)
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        var window = this.FindAncestorOfType<Window>();
        if (window != null) window.WindowState = WindowState.Minimized;
    }
}