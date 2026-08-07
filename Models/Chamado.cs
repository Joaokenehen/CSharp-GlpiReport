using System.Text.Json.Serialization;

namespace RelatorioGLPIApp.Models;

public class Chamado
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("date")]
    public string DataCriacao { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Descricao { get; set; } = string.Empty;

    [JsonPropertyName("entities_id")]
    public string? Entidade { get; set; }

    [JsonPropertyName("users_id_recipient")]
    public string? NomeUsuario { get; set; }

}