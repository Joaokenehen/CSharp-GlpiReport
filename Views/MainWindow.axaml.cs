using Avalonia.Controls;
using RelatorioGLPIApp.ViewModels;

namespace RelatorioGLPIApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new LoginViewModel();
    }
}