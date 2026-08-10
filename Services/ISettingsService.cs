using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services
{
    public interface ISettingsService
    {
        void SaveCredentials(LoginCredentials credentials);
        LoginCredentials? LoadCredentials();
        void ClearCredentials();
    }
}
