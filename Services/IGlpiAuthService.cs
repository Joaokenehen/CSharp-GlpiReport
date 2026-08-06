using System.Threading.Tasks;

namespace RelatorioGLPIApp.Services;

public interface IGlpiAuthService
{
    Task<bool> AutenticarAsync(string url, string userToken, string appToken);
}