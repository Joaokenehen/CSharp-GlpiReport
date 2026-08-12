using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using RelatorioGLPIApp.Models;
using RelatorioGLPIApp.Services;

namespace RelatorioGLPIApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _paginaAtual = null!;

    public MainWindowViewModel()
    {
        ShowLoginView();
    }

    private void ShowLoginView()
    {
        var loginViewModel = new LoginViewModel();
        loginViewModel.AoLogarComSucesso = (connectionInfo) =>
        {
            ShowDashboardView(connectionInfo);
        };
        PaginaAtual = loginViewModel;
    }

    private void ShowDashboardView(GlpiConnectionInfo connectionInfo)
    {
        var dashboardViewModel = new DashboardViewModel(connectionInfo)
        {
            OnLogoutRequested = ShowLoginView
        };

        dashboardViewModel.OnNavigateToGeneralReportsRequested += () =>
        {
            ShowGeneralReportsView(connectionInfo, dashboardViewModel);
        };

        PaginaAtual = dashboardViewModel;
    }

    private void ShowGeneralReportsView(GlpiConnectionInfo connectionInfo, DashboardViewModel dashboardViewModel)
    {
        var generalReportsViewModel = new GeneralReportsViewModel(connectionInfo, new LogService(), dashboardViewModel);
        generalReportsViewModel.OnBackToDashboardRequested += () => PaginaAtual = dashboardViewModel;
        PaginaAtual = generalReportsViewModel;
    }
}