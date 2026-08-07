using System.Text.Json.Serialization;

namespace RelatorioGLPIApp.Models;

public class TicketUser
{
    [JsonPropertyName("tickets_id")]
    public int? TicketsId { get; set; }

    [JsonPropertyName("users_id")]
    public string? UsersId { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }
}