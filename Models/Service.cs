using System;

namespace ServicesyncWebApp.Models
{
    public class Service
    {
        public int ServiceID { get; set; }
        public int ProfessionalID { get; set; }
        public int CategoryID { get; set; }
        public string ServiceName { get; set; } = "";
        public string Title { get; set; } = "";
        public decimal Price { get; set; }
        public int? EstimatedHours { get; set; }
        public string Description { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
