using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RelatorioGLPIApp.Services;
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
            ILogService logService = new LogService();
            IUpdateService updateService = new UpdateService(logService);

            var mainWindowViewModel = new MainWindowViewModel(updateService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}