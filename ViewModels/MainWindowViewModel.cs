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
    private TechnicianReportsViewModel? _technicianReportsViewModel;
    private readonly ILogService _log;
    // Não precisa de cache para a tela de detalhe, ela será sempre recriada.

    public MainWindowViewModel()
    {
        // Adiciona um logger para depurar a navegação
        _log = new LogService();
        _log.Info("App", "MainWindowViewModel inicializado.");
        ShowLoginView();
    }

    private void ShowLoginView()
    {
        // Limpa os ViewModels em cache ao fazer logout ou iniciar, para garantir um estado limpo.
        _dashboardViewModel = null;
        _generalReportsViewModel = null;
        _technicianReportsViewModel = null;

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

            _dashboardViewModel.OnNavigateToTechnicianReportsRequested += () =>
            {
                ShowTechnicianReportsView(connectionInfo, _dashboardViewModel);
            };

            _dashboardViewModel.OnLoadGeneralReportAndNavigateRequested += (reportId) =>
            {
                ShowGeneralReportsView(connectionInfo, _dashboardViewModel);
                _generalReportsViewModel?.LoadStateCommand.Execute(reportId);
            };

            _dashboardViewModel.OnLoadTechnicianReportAndNavigateRequested += (reportId) =>
            {
                ShowTechnicianReportsView(connectionInfo, _dashboardViewModel);
                _technicianReportsViewModel?.LoadStateCommand.Execute(reportId);
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
            _generalReportsViewModel.OnBackToDashboardRequested += () =>
            {
                PaginaAtual = dashboardViewModel;
            };
            _generalReportsViewModel.OnNavigateToTechnicianReportsRequested += () =>
            {
                ShowTechnicianReportsView(connectionInfo, dashboardViewModel);
            };
            _generalReportsViewModel.OnShowTechnicianDetailRequested += (techName, tickets, isDaily) =>
            {
                _log.Info("Navigation", $"Evento OnShowTechnicianDetailRequested recebido para o técnico: {techName}.");
                ShowTechnicianDetailView(techName, tickets, isDaily, _generalReportsViewModel, _generalReportsViewModel);
            };
        }
        PaginaAtual = _generalReportsViewModel;
    }

    private void ShowTechnicianReportsView(GlpiConnectionInfo connectionInfo, DashboardViewModel dashboardViewModel)
    {
        if (_technicianReportsViewModel == null)
        {
            _technicianReportsViewModel = new TechnicianReportsViewModel(connectionInfo, new LogService(), dashboardViewModel);
            _technicianReportsViewModel.OnBackToDashboardRequested += () =>
            {
                PaginaAtual = dashboardViewModel;
            };
            _technicianReportsViewModel.OnNavigateToGeneralReportsRequested += () =>
            {
                ShowGeneralReportsView(connectionInfo, dashboardViewModel);
            };
            _technicianReportsViewModel.OnShowTechnicianDetailRequested += (techName, tickets, isDaily) =>
            {
                _log.Info("Navigation", $"Evento OnShowTechnicianDetailRequested recebido para o técnico: {techName}.");
                ShowTechnicianDetailView(techName, tickets, isDaily, _technicianReportsViewModel, _technicianReportsViewModel);
            };
        }
        PaginaAtual = _technicianReportsViewModel;
    }

    private void ShowTechnicianDetailView(string techName, List<Chamado> tickets, bool isDaily, IOnDutyChecker onDutyChecker, ViewModelBase parentViewModel)
    {
        _log.Info("Navigation", "Criando e exibindo a TechnicianDetailView.");
        var detailViewModel = new TechnicianDetailViewModel(techName, tickets, isDaily, onDutyChecker);
        detailViewModel.OnBackToGeneralReportsRequested += () =>
        {
            _log.Info("Navigation", $"Retornando da TechnicianDetailView para a tela anterior.");
            PaginaAtual = parentViewModel;
        };
        PaginaAtual = detailViewModel;
    }
}