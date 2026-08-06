using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RelatorioGLPIApp.ViewModels;
using RelatorioGLPIApp.Views;

namespace RelatorioGLPIApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                // Mude de MainViewModel() para LoginViewModel() aqui:
                DataContext = new LoginViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

}