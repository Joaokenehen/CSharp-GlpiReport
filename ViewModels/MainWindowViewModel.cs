using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _paginaAtual;

    public MainWindowViewModel()
    {
        var telaDeLogin = new LoginViewModel();

        telaDeLogin.AoLogarComSucesso = (connectionInfo) =>
        {
            PaginaAtual = new DashboardViewModel(connectionInfo);
        };

        PaginaAtual = telaDeLogin;
    }
}