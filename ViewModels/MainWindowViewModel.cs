using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // A variável que diz ao Avalonia qual tela desenhar agora
    [ObservableProperty]
    private ViewModelBase _paginaAtual;

    public MainWindowViewModel()
    {
        var telaDeLogin = new LoginViewModel();

        telaDeLogin.AoLogarComSucesso = (chamadosBaixados) =>
        {
            PaginaAtual = new DashboardViewModel(chamadosBaixados);
        };

        PaginaAtual = telaDeLogin;
    }
}