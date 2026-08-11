using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace RelatorioGLPIApp.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly ILogService _log;
        // IMPORTANTE: Altere para o seu usuário e nome de repositório no GitHub
        private const string GitHubRepo = "SEU_USUARIO/SEU_REPOSITORIO";
        private readonly HttpClient _httpClient;

        public UpdateService(ILogService log)
        {
            _log = log;
            _httpClient = new HttpClient();
            // A API do GitHub exige um cabeçalho User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelatorioGLPIApp", GetCurrentVersion().ToString()));
        }

        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                _log.Info("Update", "Verificando se há novas atualizações...");
                var url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
                var response = await _httpClient.GetStringAsync(url);

                using var jsonDoc = JsonDocument.Parse(response);
                var tagName = jsonDoc.RootElement.GetProperty("tag_name").GetString();
                var htmlUrl = jsonDoc.RootElement.GetProperty("html_url").GetString();

                if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(htmlUrl))
                {
                    _log.Info("Update", "Não foi possível obter informações da última versão no GitHub.");
                    return null;
                }

                // Remove o prefixo 'v' se existir, ex: v1.1.0 -> 1.1.0
                var latestVersionStr = tagName.StartsWith("v") ? tagName.Substring(1) : tagName;
                var latestVersion = new Version(latestVersionStr);
                var currentVersion = GetCurrentVersion();

                _log.Info("Update", $"Versão atual: {currentVersion}. Versão mais recente no GitHub: {latestVersion}.");

                if (latestVersion > currentVersion)
                {
                    _log.Sucesso("Update", $"Nova versão encontrada: {tagName}");
                    return new UpdateInfo(tagName, htmlUrl);
                }

                _log.Info("Update", "O aplicativo já está na versão mais recente.");
                return null;
            }
            catch (Exception ex)
            {
                _log.Erro("Update", $"Falha ao verificar atualizações: {ex.Message}");
                return null;
            }
        }

        private Version GetCurrentVersion() => Assembly.GetExecutingAssembly().GetName().Version ?? new Version("0.0.0");
    }
}