namespace ServicesyncWebApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name  { get; set; } = "";
        public string Email { get; set; } = "";   // unique
        public string Phone { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? OtpCode { get; set; }
        public DateTime? OtpExpiry { get; set; }
        public bool IsVerified { get; set; } = false;
    }
}
