using CommunityToolkit.Mvvm.ComponentModel;

namespace RelatorioGLPIApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Bem-vindo ao Sistema de Relatórios GLPI!";
}