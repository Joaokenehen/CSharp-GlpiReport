using System.Text.Json.Serialization;

namespace RelatorioGLPIApp.Models;

public class Solution
{
    [JsonPropertyName("itemtype")]
    public string ItemType { get; set; } = string.Empty;

    [JsonPropertyName("items_id")]
    public object? ItemsId { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
