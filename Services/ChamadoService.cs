using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services;

public class ChamadoService : IChamadoService
{
    private readonly ILogService _log;

    public ChamadoService(ILogService logService)
    {
        _log = logService;
    }

    public async Task<List<Chamado>> ObterChamadosAsync(string urlBase, string appToken, string sessionToken)
    {
        try
        {
            _log.Info("Chamados", "Buscando relatórios/chamados no GLPI...");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("App-Token", appToken);
            client.DefaultRequestHeaders.Add("Session-Token", sessionToken);

            string endpoint = urlBase.TrimEnd('/') + "/Ticket?expand_dropdowns=true&sort=id&order=DESC&range=0-100";

            HttpResponseMessage response = await client.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();

                var chamados = JsonSerializer.Deserialize<List<Chamado>>(jsonResponse) ?? new List<Chamado>();

                _log.Sucesso("Chamados", $"{chamados.Count} chamados carregados com sucesso!");
                return chamados;
            }
            else
            {
                string erroDetalhado = await response.Content.ReadAsStringAsync();
                _log.Erro("Chamados", $"Erro ao buscar: {(int)response.StatusCode} - {erroDetalhado}");
                return new List<Chamado>();
            }
        }
        catch (Exception ex)
        {
            _log.Erro("Chamados", $"Exceção Crítica: {ex.Message}");
            return new List<Chamado>();
        }
    }
}