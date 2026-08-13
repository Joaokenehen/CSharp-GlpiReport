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

    [JsonPropertyName("solvedate")]
    public string? DataSolucao { get; set; }

    [JsonPropertyName("closedate")]
    public string? DataFechamento { get; set; }

    [JsonPropertyName("date_mod")]
    public string? DataModificacao { get; set; }

    [JsonPropertyName("content")]
    public string Descricao { get; set; } = string.Empty;

    [JsonPropertyName("entities_id")]
    public string? Entidade { get; set; }

    [JsonPropertyName("users_id_recipient")]
    public string? NomeUsuario { get; set; }

    [JsonPropertyName("users_id_assign")]
    public string? TecnicoAtribuido { get; set; }

    [JsonPropertyName("date_assign")]
    public string? DataAtribuicao { get; set; }

    [JsonPropertyName("solve_delay_stat")]
    public int? TempoParaSolucao { get; set; } // Time to solve in seconds
}