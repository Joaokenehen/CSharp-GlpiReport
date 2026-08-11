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
    public Action<GlpiConnectionInfo>? AoLogarComSucesso { get; set; }
    private readonly IGlpiAuthService _authService;
    private readonly IChamadoService _chamadoService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _urlGlpi = "https://suporte.expnordeste.com.br/apirest.php"; // Colocar URl do seu GLPI

    [ObservableProperty]
    private string _userToken = "";

    [ObservableProperty]
    private string _appToken = "";

    [ObservableProperty]
    private string _mensagem = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isUserTokenVisible;

    [ObservableProperty]
    private char? _userTokenPasswordChar = '*';

    [ObservableProperty]
    private bool _isAppTokenVisible;

    [ObservableProperty]
    private char? _appTokenPasswordChar = '*';

    [ObservableProperty]
    private bool _isRememberMe;

    public LoginViewModel()
    {
        ILogService logger = new LogService();

        _authService = new GLPIAuthService(logger);
        _chamadoService = new ChamadoService(logger);
        _settingsService = new SettingsService();

        LoadSavedCredentials();
    }

    private void LoadSavedCredentials()
    {
        var credentials = _settingsService.LoadCredentials();
        if (credentials != null)
        {
            UrlGlpi = credentials.UrlGlpi;
            UserToken = credentials.UserToken;
            AppToken = credentials.AppToken;
            IsRememberMe = true;
        }
    }

    [RelayCommand]
    private async Task Entrar()
    {
        IsLoading = true;
        Mensagem = "Conectando ao servidor...";

        bool sucesso = await _authService.AutenticarAsync(UrlGlpi, UserToken, AppToken);

        if (sucesso)
        {
            if (IsRememberMe)
            {
                var credentials = new LoginCredentials { UrlGlpi = this.UrlGlpi, UserToken = this.UserToken, AppToken = this.AppToken };
                _settingsService.SaveCredentials(credentials);
            }
            else
            {
                _settingsService.ClearCredentials();
            }

            Mensagem = "Autenticado com sucesso! Iniciando sessão.";
            string sessionToken = _authService.SessionToken ?? "";
            var listaDeChamados = await _chamadoService.ObterChamadosAsync(UrlGlpi, AppToken, sessionToken);
            Mensagem = $"Pronto! {listaDeChamados.Count} chamados carregados na memória.";

            var connectionInfo = new GlpiConnectionInfo(UrlGlpi, AppToken, sessionToken, _chamadoService, listaDeChamados);
            AoLogarComSucesso?.Invoke(connectionInfo);
        }
        else
        {
            Mensagem = "Erro: Verifique seus tokens ou a URL.";
        }
        IsLoading = false;
    }

    [RelayCommand]
    private void ToggleUserTokenVisibility()
    {
        IsUserTokenVisible = !IsUserTokenVisible;
        UserTokenPasswordChar = IsUserTokenVisible ? null : '*';
    }

    [RelayCommand]
    private void ToggleAppTokenVisibility()
    {
        IsAppTokenVisible = !IsAppTokenVisible;
        AppTokenPasswordChar = IsAppTokenVisible ? null : '*';
    }
}