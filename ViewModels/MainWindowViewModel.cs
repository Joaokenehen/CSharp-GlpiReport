using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using RelatorioGLPIApp.Models;
using RelatorioGLPIApp.Services;

namespace RelatorioGLPIApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _paginaAtual = null!;

    // Cache para os ViewModels principais para manter o estado durante a navegação.
    private DashboardViewModel? _dashboardViewModel;
    private GeneralReportsViewModel? _generalReportsViewModel;

    public MainWindowViewModel()
    {
        ShowLoginView();
    }

    private void ShowLoginView()
    {
        // Limpa os ViewModels em cache ao fazer logout ou iniciar, para garantir um estado limpo.
        _dashboardViewModel = null;
        _generalReportsViewModel = null;

        var loginViewModel = new LoginViewModel();
        loginViewModel.AoLogarComSucesso = (connectionInfo) =>
        {
            ShowDashboardView(connectionInfo);
        };
        PaginaAtual = loginViewModel;
    }

    private void ShowDashboardView(GlpiConnectionInfo connectionInfo)
    {
        // Cria o DashboardViewModel apenas uma vez e o reutiliza.
        if (_dashboardViewModel == null)
        {
            _dashboardViewModel = new DashboardViewModel(connectionInfo)
            {
                OnLogoutRequested = ShowLoginView
            };

            _dashboardViewModel.OnNavigateToGeneralReportsRequested += () =>
            {
                ShowGeneralReportsView(connectionInfo, _dashboardViewModel);
            };
        }

        PaginaAtual = _dashboardViewModel;
    }

    private void ShowGeneralReportsView(GlpiConnectionInfo connectionInfo, DashboardViewModel dashboardViewModel)
    {
        // Cria o GeneralReportsViewModel apenas uma vez e o reutiliza.
        if (_generalReportsViewModel == null)
        {
            _generalReportsViewModel = new GeneralReportsViewModel(connectionInfo, new LogService(), dashboardViewModel);
            _generalReportsViewModel.OnBackToDashboardRequested += () => PaginaAtual = dashboardViewModel;
        }
        PaginaAtual = _generalReportsViewModel;
    }
}