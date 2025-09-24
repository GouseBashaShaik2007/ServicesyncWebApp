using System;

namespace ServicesyncWebApp.Models
{
    public class ProfessionalService
    {
        public int ProfessionalID { get; set; }
        public int CategoryID { get; set; }
        public decimal? Rate { get; set; }
        public string Description { get; set; } = "";
    }
}
