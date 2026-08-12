using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services;

public interface IChamadoService
{
    Task<List<Chamado>> ObterChamadosAsync(string urlBase, string appToken, string sessionToken);
    Task<List<Chamado>> ObterChamadosParaRelatorioGeralAsync(string urlBase, string appToken, string sessionToken, DateTimeOffset startDate, DateTimeOffset endDate);
}