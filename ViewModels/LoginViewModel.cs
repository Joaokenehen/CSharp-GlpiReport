using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelatorioGLPIApp.Services;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    public Action<List<Chamado>>? AoLogarComSucesso { get; set; }
    private readonly IGlpiAuthService _authService;
    private readonly IChamadoService _chamadoService;

    [ObservableProperty]
    private string _urlGlpi = "https://suporte.expnordeste.com.br/apirest.php";

    [ObservableProperty]
    private string _userToken = "";

    [ObservableProperty]
    private string _appToken = "";

    [ObservableProperty]
    private string _mensagem = "";

    public LoginViewModel()
    {
        ILogService logger = new LogService();

        _authService = new GLPIAuthService(logger);
        _chamadoService = new ChamadoService(logger);
    }

    [RelayCommand]
    private async Task Entrar()
    {
        Mensagem = "Conectando ao servidor...";

        bool sucesso = await _authService.AutenticarAsync(UrlGlpi, UserToken, AppToken);

        if (sucesso)
        {
            Mensagem = "Autenticado com sucesso! SESSÃO INICIADA.";
            string sessionToken = _authService.SessionToken ?? "";
            var listaDeChamados = await _chamadoService.ObterChamadosAsync(UrlGlpi, AppToken, sessionToken);
            Mensagem = $"Pronto! {listaDeChamados.Count} chamados carregados na memória.";

            AoLogarComSucesso?.Invoke(listaDeChamados);
        }
        else
        {
            Mensagem = "Erro: Verifique seus tokens ou a URL.";
        }
    }
}