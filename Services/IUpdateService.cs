using System.Threading.Tasks;

namespace RelatorioGLPIApp.Services
{
    public record UpdateInfo(string Version, string Url);

    public interface IUpdateService
    {
        Task<UpdateInfo?> CheckForUpdateAsync();
    }
}