using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            _log.Info("API", "Buscando chamados recentes no GLPI...");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("App-Token", appToken);
            client.DefaultRequestHeaders.Add("Session-Token", sessionToken);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            // NOVO: 0. Busca todos os usuários para mapear ID -> Nome de usuário (login)
            var userMap = await GetUserMapAsync(client, urlBase, jsonOptions);

            // 1. Busca os chamados principais
            // ALTERADO: Ordena por data de modificação para capturar chamados antigos que foram solucionados/fechados hoje.
            string endpointChamados = $"{urlBase.TrimEnd('/')}/Ticket?expand_dropdowns=true&sort=date_mod&order=DESC&range=0-200";
            HttpResponseMessage respChamados = await client.GetAsync(endpointChamados);

            if (!respChamados.IsSuccessStatusCode)
            {
                _log.Erro("API", $"Erro ao buscar chamados: {respChamados.StatusCode}");
                return new List<Chamado>();
            }

            string jsonChamados = await respChamados.Content.ReadAsStringAsync();
            var chamados = JsonSerializer.Deserialize<List<Chamado>>(jsonChamados, jsonOptions) ?? new List<Chamado>();

            // 2. Busca a tabela de Atores (Ticket_User) para pegar o ID do Técnico Atribuído (Type == 2)
            _log.Info("API", "Buscando os técnicos atribuídos aos chamados...");
            // CORREÇÃO: Busca as associações mais recentes. A ordenação DESC é crucial para encontrar os técnicos dos chamados recentes.
            string endpointAtores = $"{urlBase.TrimEnd('/')}/Ticket_User?sort=id&order=DESC&range=0-1000";
            HttpResponseMessage respAtores = await client.GetAsync(endpointAtores);

            if (respAtores.IsSuccessStatusCode)
            {
                string jsonAtores = await respAtores.Content.ReadAsStringAsync();
                var atores = ParseTicketUsers(jsonAtores);

                foreach (var chamado in chamados)
                {
                    // Procura por TODOS os atores do tipo "atribuído" (type 2)
                    var tecnicosAtribuidos = atores.Where(a =>
                        a.TicketsId.HasValue && a.TicketsId.Value == chamado.Id && a.Type == 2);

                    var nomesDosTecnicos = new List<string>();
                    foreach (var tecnico in tecnicosAtribuidos)
                    {
                        // Se encontrarmos o técnico e seu ID, usamos o mapa para obter o nome de login
                        if (!string.IsNullOrEmpty(tecnico.UsersId) && userMap.TryGetValue(tecnico.UsersId, out var nomeLogin))
                        {
                            nomesDosTecnicos.Add(nomeLogin);
                        }
                    }

                    if (nomesDosTecnicos.Any())
                    {
                        chamado.TecnicoAtribuido = string.Join(", ", nomesDosTecnicos.Distinct());
                    }
                }
            }

            // 3. Busca as Soluções e armazena em um dicionário para uso posterior
            _log.Info("API", "Buscando as soluções dos chamados...");
            var solucoesPorTicketId = new Dictionary<int, string>();
            // Aumenta o range para garantir que mais soluções sejam buscadas e remove expand_dropdowns
            string endpointSolucoes = $"{urlBase.TrimEnd('/')}/ITILSolution?sort=id&order=DESC&range=0-500";
            HttpResponseMessage respSolucoes = await client.GetAsync(endpointSolucoes);

            if (respSolucoes.IsSuccessStatusCode)
            {
                string jsonSolucoes = await respSolucoes.Content.ReadAsStringAsync();
                var solucoes = JsonSerializer.Deserialize<List<Solution>>(jsonSolucoes, jsonOptions) ?? new List<Solution>();

                foreach (var solucao in solucoes)
                {   // Tenta converter ItemsId para int. Se falhar, ignora.
                    string? itemsIdString = solucao.ItemsId?.ToString();
                    if (solucao.ItemType == "Ticket" && !string.IsNullOrEmpty(itemsIdString) && int.TryParse(itemsIdString, out int ticketId))
                    {
                        // Pega a solução mais recente (a primeira que encontrar, por causa do sort=DESC)
                        if (!solucoesPorTicketId.ContainsKey(ticketId))
                        {
                            solucoesPorTicketId[ticketId] = solucao.Content;
                        }
                    }
                }
            }

            // 4. Busca os Acompanhamentos (Followups) de técnicos e armazena o mais recente
            _log.Info("API", "Buscando as últimas interações de técnicos (Followups)...");
            var followupsPorTicketId = new Dictionary<int, string>();
            // Aumenta o range para garantir que mais followups sejam buscados e remove expand_dropdowns
            string endpointFollowups = $"{urlBase.TrimEnd('/')}/ITILFollowup?sort=id&order=DESC&range=0-500";
            HttpResponseMessage respFollowups = await client.GetAsync(endpointFollowups);

            if (respFollowups.IsSuccessStatusCode)
            {
                string jsonFollowups = await respFollowups.Content.ReadAsStringAsync();
                var followups = JsonSerializer.Deserialize<List<Followup>>(jsonFollowups, jsonOptions) ?? new List<Followup>();

                foreach (var followup in followups)
                {   // Tenta converter ItemsId para int. Se falhar, ignora.
                    string? itemsIdString = followup.ItemsId?.ToString();
                    if (followup.ItemType == "Ticket" && !string.IsNullOrEmpty(itemsIdString) && int.TryParse(itemsIdString, out int ticketId))
                    {
                        // Pega o followup mais recente (o primeiro que encontrar) que seja privado (interação do técnico)
                        if (followup.IsPrivate == 1 && !followupsPorTicketId.ContainsKey(ticketId))
                        {
                            followupsPorTicketId[ticketId] = followup.Content;
                        }
                    }
                }
            }

            foreach (var chamado in chamados)
            {
                if ((chamado.Status == 5 || chamado.Status == 6) && solucoesPorTicketId.TryGetValue(chamado.Id, out var solucao))
                {
                    chamado.Descricao = solucao;
                }
                else if ((chamado.Status >= 2 && chamado.Status <= 4) && followupsPorTicketId.TryGetValue(chamado.Id, out var followup))
                {
                    chamado.Descricao = followup;
                }
            }

            _log.Sucesso("API", "Chamados, técnicos e conteúdos sincronizados com sucesso!");
            return chamados;
        }
        catch (Exception ex)
        {
            _log.Erro("API", $"Exceção Crítica: {ex.Message}");
            return new List<Chamado>();
        }
    }

    private async Task<Dictionary<string, string>> GetUserMapAsync(HttpClient client, string urlBase, JsonSerializerOptions options)
    {
        _log.Info("API", "Mapeando IDs de usuários para nomes de login...");
        var userMap = new Dictionary<string, string>();
        string endpointUsuarios = $"{urlBase.TrimEnd('/')}/User?range=0-2000";

        try
        {
            var response = await client.GetAsync(endpointUsuarios);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<GlpiUser>>(json, options);

                if (users != null)
                {
                    foreach (var user in users)
                    {
                        userMap[user.Id.ToString()] = user.Name;
                    }
                    _log.Info("API", $"{userMap.Count} usuários mapeados com sucesso.");
                }
            }
            else
            {
                _log.Erro("API", $"Falha ao buscar lista de usuários: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _log.Erro("API", $"Exceção ao mapear usuários: {ex.Message}");
        }

        return userMap;
    }

    private static List<TicketUser> ParseTicketUsers(string json)
    {
        var resultado = new List<TicketUser>();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return resultado;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var ticketUser = new TicketUser();

                if (item.TryGetProperty("type", out var typeProp))
                {
                    if (typeProp.ValueKind == JsonValueKind.Number && typeProp.TryGetInt32(out int type))
                    {
                        ticketUser.Type = type;
                    }
                    else if (typeProp.ValueKind == JsonValueKind.String && int.TryParse(typeProp.GetString(), out int typeString))
                    {
                        ticketUser.Type = typeString;
                    }
                }

                if (item.TryGetProperty("users_id", out var usersIdProp))
                {
                    ticketUser.UsersId = usersIdProp.ValueKind == JsonValueKind.String
                        ? usersIdProp.GetString()
                        : usersIdProp.ToString();
                }

                if (item.TryGetProperty("tickets_id", out var ticketsIdProp))
                {
                    ticketUser.TicketsId = ParseNullableInt(ticketsIdProp);
                }

                resultado.Add(ticketUser);
            }
        }
        catch (JsonException)
        {
            return new List<TicketUser>();
        }

        return resultado;
    }

    private static int? ParseNullableInt(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out int value) => value,
            JsonValueKind.String when int.TryParse(element.GetString(), out int value) => value,
            JsonValueKind.Null => null,
            _ => null
        };
    }

    public async Task<List<Chamado>> ObterChamadosParaRelatorioGeralAsync(string urlBase, string appToken, string sessionToken, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        _log.Info("API_GERAL", "Iniciando busca completa de todos os chamados (fetchAll=true). O filtro será aplicado localmente.");
        // Revertendo para a lógica de buscar todos os chamados e deixar o ViewModel filtrar.
        // Isso é mais lento, mas mais confiável se o filtro da API não estiver funcionando como esperado.
        var todosOsChamados = await ObterChamadosAsync(urlBase, appToken, sessionToken);
        _log.Sucesso("API_GERAL", $"{todosOsChamados.Count} chamados baixados do GLPI.");
        return todosOsChamados;
    }
}