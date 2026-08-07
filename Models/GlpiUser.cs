using System.Text.Json.Serialization;

namespace RelatorioGLPIApp.Models;

public class GlpiUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
