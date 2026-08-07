using System.Collections.Generic;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services;

public interface IChamadoService
{
    Task<List<Chamado>> ObterChamadosAsync(string urlBase, string appToken, string sessionToken);
}