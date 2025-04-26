namespace BiblioAPI.Models
{
    public class LibroModel
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty; // Inicializar para evitar nulls
        public string Autor { get; set; } = string.Empty;
        public string Editorial { get; set; } = string.Empty;
        public string ISBN { get; set; } = string.Empty;
        public int Anio { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public int Existencias { get; set; }
    }
}