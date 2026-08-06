using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelatorioGLPIApp.Services;
using System.Threading.Tasks;


namespace RelatorioGLPIApp.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IGlpiAuthService _authService;

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
    }

    [RelayCommand]
    private async Task Entrar()
    {
        Mensagem = "Conectando ao servidor..."; // Dá um feedback visual rápido

        bool sucesso = await _authService.AutenticarAsync(UrlGlpi, UserToken, AppToken);

        if (sucesso)
            Mensagem = "Autenticado com sucesso! SESSÃO INICIADA.";
        else
            Mensagem = "Erro: Verifique seus tokens ou a URL.";
    }

}