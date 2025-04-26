using System; // Necesario para DateTime

namespace BiblioAPI.Models
{
    public class PrestamoModel
    {
        public int Id { get; set; }
        public int IdUsuario { get; set; }
        public int IdLibro { get; set; }
        public DateTime FechaPrestamo { get; set; } // El SP usa DEFAULT GETDATE()
        public DateTime FechaDevolucionEsperada { get; set; }
        public DateTime? FechaDevolucionReal { get; set; } // Permitir null
        public string Estado { get; set; } = string.Empty;
    }
}