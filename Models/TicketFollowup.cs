namespace RelatorioGLPIApp.Models
{
    public class TicketFollowup
    {
        public string Content { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public bool IsPrivate { get; set; }
    }
}