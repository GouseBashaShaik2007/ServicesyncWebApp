namespace ServicesyncWebApp.Models
{
    public class EmailRequest
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Html { get; set; } = string.Empty;
        public string? Text { get; set; }
    }
}
