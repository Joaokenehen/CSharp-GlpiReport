using System.Threading.Tasks;

namespace RelatorioGLPIApp.Services;

public interface IGlpiAuthService
{
    string? SessionToken { get; }
    Task<bool> AutenticarAsync(string url, string userToken, string appToken);
}