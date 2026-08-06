using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace RelatorioGLPIApp.Services;

public class GLPIAuthService : IGlpiAuthService
{

    private readonly ILogService _log;

    public GLPIAuthService(ILogService logService)
    {
        _log = logService;
    }
    public async Task<bool> AutenticarAsync(string url, string userToken, string appToken)
    {
        try
        {
            _log.Info("Auth", "Iniciando tentativa de conexão...");
            _log.Info("Auth", $"URL Base: {url}");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("App-Token", appToken);
            client.DefaultRequestHeaders.Add("Authorization", $"user_token {userToken}");

            string endpoint = url.TrimEnd('/') + "/initSession";

            HttpResponseMessage response = await client.GetAsync(endpoint);
            _log.Info("Auth", $"Status HTTP retornado: {(int)response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {

                // Extraindo o token de sessão do JSON
                string jsonResponse = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                string sessionToken = doc.RootElement.GetProperty("session_token").GetString() ?? "";

                _log.Sucesso("Auth", "SUCESSO! Sessão iniciada no GLPI.");
                return true;
            }
            else
            {
                string erroDetalhado = await response.Content.ReadAsStringAsync();
                _log.Erro("Auth", $"Falha na API: {erroDetalhado}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log.Erro("Auth", $"Exceção Crítica: {ex.Message}");
            return false;
        }
    }
}
