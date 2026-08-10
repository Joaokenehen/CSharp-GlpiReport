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
        ShowLoginView();
    }

    private void ShowLoginView()
    {
        var loginViewModel = new LoginViewModel();
        loginViewModel.AoLogarComSucesso = (connectionInfo) =>
        {
            PaginaAtual = new DashboardViewModel(connectionInfo)
            {
                OnLogoutRequested = ShowLoginView // Agora, ao sair, chamamos este método novamente
            };
        };
        PaginaAtual = loginViewModel;
    }
}