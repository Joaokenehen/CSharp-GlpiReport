using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;

namespace RelatorioGLPIApp.Views;

public partial class TechnicianReportsView : UserControl
{
    public TechnicianReportsView()
    {
        InitializeComponent();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e) => this.FindAncestorOfType<Window>()?.BeginMoveDrag(e);

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