using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
            _log.Info("API_DIARIO", "Buscando últimos 200 chamados no GLPI (Carregamento Rápido)...");

            using var client = CriarHttpClient(appToken, sessionToken);
            var jsonOptions = CriarJsonOptions();

            var userMap = await GetUserMapAsync(client, urlBase, jsonOptions);

            // 1. Busca APENAS os últimos 200 chamados (Sem paginação, muito rápido)
            string endpointChamados = $"{urlBase.TrimEnd('/')}/Ticket?expand_dropdowns=true&sort=date_mod&order=DESC&range=0-200";
            HttpResponseMessage respChamados = await client.GetAsync(endpointChamados);
            if (!respChamados.IsSuccessStatusCode) return new List<Chamado>();

            var chamados = JsonSerializer.Deserialize<List<Chamado>>(await respChamados.Content.ReadAsStringAsync(), jsonOptions) ?? new List<Chamado>();

            // 2. Busca APENAS os últimos 1000 atores 
            string endpointAtores = $"{urlBase.TrimEnd('/')}/Ticket_User?sort=id&order=DESC&range=0-1000";
            var respAtores = await client.GetAsync(endpointAtores);
            var atores = respAtores.IsSuccessStatusCode ? ParseTicketUsers(await respAtores.Content.ReadAsStringAsync()) : new List<TicketUser>();

            // 3. Busca APENAS as últimas 500 soluções
            var solucoesPorTicketId = new Dictionary<int, string>();
            string endpointSolucoes = $"{urlBase.TrimEnd('/')}/ITILSolution?sort=id&order=DESC&range=0-500";
            var respSolucoes = await client.GetAsync(endpointSolucoes);
            if (respSolucoes.IsSuccessStatusCode)
            {
                var solucoes = JsonSerializer.Deserialize<List<Solution>>(await respSolucoes.Content.ReadAsStringAsync(), jsonOptions) ?? new List<Solution>();
                foreach (var s in solucoes)
                {
                    if (s.ItemType == "Ticket" && int.TryParse(s.ItemsId?.ToString(), out int tId) && !solucoesPorTicketId.ContainsKey(tId))
                        solucoesPorTicketId[tId] = s.Content;
                }
            }

            // 4. Busca APENAS os últimos 500 followups
            var followupsPorTicketId = new Dictionary<int, string>();
            string endpointFollowups = $"{urlBase.TrimEnd('/')}/ITILFollowup?sort=id&order=DESC&range=0-500";
            var respFollowups = await client.GetAsync(endpointFollowups);
            if (respFollowups.IsSuccessStatusCode)
            {
                var followups = JsonSerializer.Deserialize<List<Followup>>(await respFollowups.Content.ReadAsStringAsync(), jsonOptions) ?? new List<Followup>();
                foreach (var f in followups)
                {
                    if (f.ItemType == "Ticket" && f.IsPrivate == 1 && int.TryParse(f.ItemsId?.ToString(), out int tId) && !followupsPorTicketId.ContainsKey(tId))
                        followupsPorTicketId[tId] = f.Content;
                }
            }

            // 5. Cruza os dados rapidamente na memória
            foreach (var chamado in chamados)
            {
                var tecnicos = atores.Where(a => a.TicketsId == chamado.Id && a.Type == 2)
                                     .Select(t => !string.IsNullOrEmpty(t.UsersId) && userMap.ContainsKey(t.UsersId) ? userMap[t.UsersId] : null)
                                     .Where(n => n != null).Distinct();

                if (tecnicos.Any()) chamado.TecnicoAtribuido = string.Join(", ", tecnicos);

                if ((chamado.Status == 5 || chamado.Status == 6) && solucoesPorTicketId.TryGetValue(chamado.Id, out var solucao))
                    chamado.Descricao = solucao;
                else if ((chamado.Status >= 2 && chamado.Status <= 4) && followupsPorTicketId.TryGetValue(chamado.Id, out var followup))
                    chamado.Descricao = followup;
            }

            _log.Sucesso("API_DIARIO", "Carregamento inicial rápido concluído!");
            return chamados;
        }
        catch (Exception ex)
        {
            _log.Erro("API_DIARIO", $"Erro no carregamento rápido: {ex.Message}");
            return new List<Chamado>();
        }
    }

    private async Task<Dictionary<string, string>> GetUserMapAsync(HttpClient client, string urlBase, JsonSerializerOptions options)
    {
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
                    foreach (var user in users) userMap[user.Id.ToString()] = user.Name;
                }
            }
        }
        catch { /* Log omitido para limpeza, mantém a execução */ }

        return userMap;
    }

    private static List<TicketUser> ParseTicketUsers(string json)
    {
        var resultado = new List<TicketUser>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return resultado;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var ticketUser = new TicketUser();

                if (item.TryGetProperty("type", out var typeProp))
                {
                    if (typeProp.ValueKind == JsonValueKind.Number && typeProp.TryGetInt32(out int type))
                        ticketUser.Type = type;
                    else if (typeProp.ValueKind == JsonValueKind.String && int.TryParse(typeProp.GetString(), out int typeString))
                        ticketUser.Type = typeString;
                }

                if (item.TryGetProperty("users_id", out var usersIdProp))
                    ticketUser.UsersId = usersIdProp.ValueKind == JsonValueKind.String ? usersIdProp.GetString() : usersIdProp.ToString();

                if (item.TryGetProperty("tickets_id", out var ticketsIdProp))
                    ticketUser.TicketsId = ParseNullableInt(ticketsIdProp);

                resultado.Add(ticketUser);
            }
        }
        catch (JsonException) { }

        return resultado;
    }

    private static int? ParseNullableInt(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number when element.TryGetInt32(out int value) => value,
        JsonValueKind.String when int.TryParse(element.GetString(), out int value) => value,
        _ => null
    };

    public async Task<List<Chamado>> ObterChamadosParaRelatorioGeralAsync(string urlBase, string appToken, string sessionToken, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        _log.Info("API_GERAL", "Buscando chamados (Geral) com filtro de data e paginação...");

        using var client = CriarHttpClient(appToken, sessionToken);
        var jsonOptions = CriarJsonOptions();

        string ticketUrl = $"{urlBase.TrimEnd('/')}/Ticket?expand_dropdowns=true&sort=id&order=ASC";

        // Aplica o filtro de data de criação (field=2)
        if (startDate != default && endDate != default)
        {
            var start = startDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
            var end = endDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
            ticketUrl += $"&criteria[0][field]=2&criteria[0][searchtype]=morethan&criteria[0][value]={start}" +
                         $"&criteria[1][field]=2&criteria[1][searchtype]=lessthan&criteria[1][value]={end}" +
                         $"&search_op=AND";
        }

        return await BuscarEEnriquecerChamadosAsync(client, urlBase, ticketUrl, jsonOptions, "API_GERAL");
    }

    private async Task<List<Chamado>> BuscarEEnriquecerChamadosAsync(HttpClient client, string urlBase, string ticketUrl, JsonSerializerOptions options, string logContext)
    {
        try
        {
            // 1. Busca usuários e os chamados (paginado)
            var userMap = await GetUserMapAsync(client, urlBase, options);
            var chamados = await FetchAllPaginatedAsync<Chamado>(client, ticketUrl, options);

            if (!chamados.Any())
            {
                _log.Info(logContext, "Nenhum chamado encontrado para este filtro.");
                return chamados;
            }

            _log.Info(logContext, $"Processando e enriquecendo {chamados.Count} chamados...");

            // 2. Busca Atores, Soluções e Followups (paginado)
            var atores = await FetchAllTicketUsersPaginatedAsync(client, $"{urlBase.TrimEnd('/')}/Ticket_User?sort=id&order=DESC", options);
            var solucoes = await FetchAllPaginatedAsync<Solution>(client, $"{urlBase.TrimEnd('/')}/ITILSolution?sort=id&order=DESC", options);
            var followups = await FetchAllPaginatedAsync<Followup>(client, $"{urlBase.TrimEnd('/')}/ITILFollowup?sort=id&order=DESC", options);

            // 3. Organiza Soluções e Followups em Dicionários rápidos
            var solucoesPorTicketId = solucoes
                .Where(s => s.ItemType == "Ticket" && int.TryParse(s.ItemsId?.ToString(), out _))
                .GroupBy(s => int.Parse(s.ItemsId!.ToString()!))
                .ToDictionary(g => g.Key, g => g.First().Content);

            var followupsPorTicketId = followups
                .Where(f => f.ItemType == "Ticket" && f.IsPrivate == 1 && int.TryParse(f.ItemsId?.ToString(), out _))
                .GroupBy(f => int.Parse(f.ItemsId!.ToString()!))
                .ToDictionary(g => g.Key, g => g.First().Content);

            // 4. Cruza as informações
            foreach (var chamado in chamados)
            {
                // Atribui técnico
                var tecnicosAtribuidos = atores.Where(a => a.TicketsId.HasValue && a.TicketsId.Value == chamado.Id && a.Type == 2);
                var nomesDosTecnicos = tecnicosAtribuidos
                    .Where(t => !string.IsNullOrEmpty(t.UsersId) && userMap.ContainsKey(t.UsersId))
                    .Select(t => userMap[t.UsersId!])
                    .Distinct();

                if (nomesDosTecnicos.Any())
                {
                    chamado.TecnicoAtribuido = string.Join(", ", nomesDosTecnicos);
                }

                // Atribui descrição (Solução ou Followup)
                if ((chamado.Status == 5 || chamado.Status == 6) && solucoesPorTicketId.TryGetValue(chamado.Id, out var solucao))
                {
                    chamado.Descricao = solucao;
                }
                else if ((chamado.Status >= 2 && chamado.Status <= 4) && followupsPorTicketId.TryGetValue(chamado.Id, out var followup))
                {
                    chamado.Descricao = followup;
                }
            }

            _log.Sucesso(logContext, "Sincronização e enriquecimento concluídos com sucesso!");
            return chamados;
        }
        catch (Exception ex)
        {
            _log.Erro(logContext, $"Exceção Crítica: {ex.Message}");
            return new List<Chamado>();
        }
    }

    private HttpClient CriarHttpClient(string appToken, string sessionToken)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("App-Token", appToken);
        client.DefaultRequestHeaders.Add("Session-Token", sessionToken);
        return client;
    }

    private JsonSerializerOptions CriarJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private async Task<List<T>> FetchAllPaginatedAsync<T>(HttpClient client, string baseUrl, JsonSerializerOptions options)
    {
        var allItems = new List<T>();
        int rangeStart = 0;
        const int rangeSize = 500;
        bool hasMore = true;

        while (hasMore)
        {
            string paginatedUrl = $"{baseUrl}{(baseUrl.Contains("?") ? "&" : "?")}range={rangeStart}-{rangeStart + rangeSize - 1}";
            var response = await client.GetAsync(paginatedUrl);
            if (!response.IsSuccessStatusCode) break;

            var json = await response.Content.ReadAsStringAsync();
            var pageItems = JsonSerializer.Deserialize<List<T>>(json, options) ?? new List<T>();

            if (pageItems.Any())
            {
                allItems.AddRange(pageItems);
                rangeStart += pageItems.Count;
                if (pageItems.Count < rangeSize) hasMore = false;
            }
            else
            {
                hasMore = false;
            }
        }
        return allItems;
    }



    private async Task<List<TicketUser>> FetchAllTicketUsersPaginatedAsync(HttpClient client, string baseUrl, JsonSerializerOptions options)
    {
        var jsonList = await FetchAllPaginatedAsync<JsonElement>(client, baseUrl, options);
        var jsonArrayString = JsonSerializer.Serialize(jsonList);
        return ParseTicketUsers(jsonArrayString);
    }

    // Modelo para desserializar a resposta da API para um Followup
    private class GlpiFollowup
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("users_id")]
        public string? UsersId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("is_private")]
        public int IsPrivate { get; set; } // 1 for private (technician), 0 for public (user)
    }

    // Modelo para desserializar a resposta da API para uma Solution
    private class GlpiSolution
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("users_id")]
        public string? UsersId { get; set; }
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
        [JsonPropertyName("date_creation")]
        public string? Date { get; set; }
    }

    public async Task<List<TicketFollowup>> GetTicketFollowupsAsync(string urlBase, string appToken, string sessionToken, int ticketId)
    {
        _log.Info("API", $"Buscando follow-ups para o chamado ID: {ticketId}");
        var followups = new List<TicketFollowup>();

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("App-Token", appToken);
            client.DefaultRequestHeaders.Add("Session-Token", sessionToken);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            // O mapa de usuários é necessário para obter os nomes dos autores
            var userMap = await GetUserMapAsync(client, urlBase, jsonOptions);

            // Busca o chamado original para saber quem é o requisitante (para follow-ups públicos)
            string ticketEndpoint = $"{urlBase.TrimEnd('/')}/Ticket/{ticketId}?expand_dropdowns=true";
            var ticketResp = await client.GetAsync(ticketEndpoint);
            string requesterName = "Requisitante";
            if (ticketResp.IsSuccessStatusCode)
            {
                var ticketJson = await ticketResp.Content.ReadAsStringAsync();
                // A resposta para um único ticket pode não ser uma lista.
                // Vamos usar um JsonDocument para extrair o 'users_id_recipient'
                using var doc = JsonDocument.Parse(ticketJson);
                if (doc.RootElement.TryGetProperty("users_id_recipient", out var userIdProp) && userIdProp.ValueKind == JsonValueKind.Number)
                {
                    string requesterId = userIdProp.GetInt32().ToString();
                    if (userMap.TryGetValue(requesterId, out var name))
                    {
                        requesterName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.Replace('.', ' '));
                    }
                }
            }

            // 1. Busca os Acompanhamentos (Follow-ups) usando o endpoint aninhado
            string followupsEndpoint = $"{urlBase.TrimEnd('/')}/Ticket/{ticketId}/ITILFollowup?expand_dropdowns=true&sort=date&order=ASC";
            HttpResponseMessage followupsResponse = await client.GetAsync(followupsEndpoint);

            if (followupsResponse.IsSuccessStatusCode)
            {
                string followupsJson = await followupsResponse.Content.ReadAsStringAsync();
                var glpiFollowups = JsonSerializer.Deserialize<List<GlpiFollowup>>(followupsJson, jsonOptions) ?? new List<GlpiFollowup>();
                foreach (var glpiFollowup in glpiFollowups)
                {
                    string author = "Desconhecido";
                    if (glpiFollowup.IsPrivate == 1) // Follow-up privado (técnico)
                    {
                        if (!string.IsNullOrEmpty(glpiFollowup.UsersId) && userMap.TryGetValue(glpiFollowup.UsersId, out var techName))
                        {
                            author = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(techName.Replace('.', ' '));
                        }
                    }
                    else // Follow-up público (requisitante)
                    {
                        author = requesterName;
                    }

                    string formattedDate = "Data indisponível";
                    if (DateTime.TryParse(glpiFollowup.Date, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var date))
                    {
                        formattedDate = date.ToString("g", System.Globalization.CultureInfo.CurrentCulture);
                    }

                    followups.Add(new TicketFollowup
                    {
                        Content = Regex.Replace(
                            System.Net.WebUtility.HtmlDecode(glpiFollowup.Content ?? "").Replace("&nbsp;", " "), // Trata &nbsp;
                            "<.*?>", string.Empty).Trim(),
                        Author = author,
                        Date = formattedDate,
                        IsPrivate = glpiFollowup.IsPrivate == 1
                    });
                }
            }
            else
            {
                _log.Erro("API", $"Erro ao buscar follow-ups para o chamado {ticketId}: {followupsResponse.StatusCode} - {await followupsResponse.Content.ReadAsStringAsync()}");
            }

            // 2. Busca as Soluções
            string solutionsEndpoint = $"{urlBase.TrimEnd('/')}/Ticket/{ticketId}/ITILSolution?expand_dropdowns=true&sort=date_creation&order=ASC";
            HttpResponseMessage solutionsResponse = await client.GetAsync(solutionsEndpoint);
            if (solutionsResponse.IsSuccessStatusCode)
            {
                string solutionsJson = await solutionsResponse.Content.ReadAsStringAsync();
                var glpiSolutions = JsonSerializer.Deserialize<List<GlpiSolution>>(solutionsJson, jsonOptions) ?? new List<GlpiSolution>();
                foreach (var glpiSolution in glpiSolutions)
                {
                    string author = "Desconhecido";
                    if (!string.IsNullOrEmpty(glpiSolution.UsersId) && userMap.TryGetValue(glpiSolution.UsersId, out var techName))
                    {
                        author = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(techName.Replace('.', ' ')); // Nome do técnico específico
                    }
                    else
                    {
                        author = "Técnico"; // Se não encontrar o nome, assume "Técnico" para soluções
                    }

                    string formattedDate = "Data indisponível";
                    if (DateTime.TryParse(glpiSolution.Date, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var date))
                    {
                        formattedDate = date.ToString("g", System.Globalization.CultureInfo.CurrentCulture);
                    }

                    string cleanedContent = System.Net.WebUtility.HtmlDecode(glpiSolution.Content ?? "").Replace("&nbsp;", " "); // Trata &nbsp;
                    cleanedContent = Regex.Replace(cleanedContent, "<.*?>", string.Empty).Trim();

                    followups.Add(new TicketFollowup
                    {
                        Content = "[SOLUÇÃO] " + cleanedContent,
                        Author = author,
                        Date = formattedDate,
                        IsPrivate = true // Soluções são sempre de técnicos
                    });
                }
            }
            else
            {
                _log.Erro("API", $"Erro ao buscar soluções para o chamado {ticketId}: {solutionsResponse.StatusCode} - {await solutionsResponse.Content.ReadAsStringAsync()}");
            }

            // 3. Ordena a lista combinada por data
            var sortedFollowups = followups.OrderBy(f =>
            {
                DateTime.TryParse(f.Date, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var dt);
                return dt;
            }).ToList();

            _log.Sucesso("API", $"{sortedFollowups.Count} itens de conversa (follow-ups + soluções) encontrados para o chamado {ticketId}.");
            return sortedFollowups;
        }
        catch (Exception ex)
        {
            _log.Erro("API", $"Exceção ao buscar follow-ups: {ex.Message}");
        }

        return followups;
    }
}