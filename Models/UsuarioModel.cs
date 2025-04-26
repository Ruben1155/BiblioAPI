namespace BiblioAPI.Models
{
    public class UsuarioModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string? Telefono { get; set; } // Permitir null para teléfono
        public string TipoUsuario { get; set; } = string.Empty;
        public string Clave { get; set; } = string.Empty; // IMPORTANTE: Almacenar HASH en la BD, validar en C#
    }
}