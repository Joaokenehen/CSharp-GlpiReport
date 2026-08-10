using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RelatorioGLPIApp.ViewModels;
using System;
using System.Threading.Tasks;

namespace RelatorioGLPIApp.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.OnItemAdded = async (item) =>
            {
                // Aguarda a UI renderizar o novo item
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

                var itemsControl = this.FindControl<ItemsControl>("RelatoriosItemsControl");
                var container = itemsControl?.ContainerFromItem(item);
                container?.BringIntoView();
            };
        }
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        this.FindAncestorOfType<Window>()?.BeginMoveDrag(e);
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