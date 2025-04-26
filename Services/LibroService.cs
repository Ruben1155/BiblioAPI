// --- BiblioAPI/Services/LibroService.cs ---
using BiblioAPI.Models;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration; // Para IConfiguration
using System.Collections.Generic; // Para List<>
using System.Threading.Tasks; // Para Task y async/await
using Microsoft.Extensions.Logging; // Para Logging (opcional pero recomendado)
using System; // Para ArgumentNullException, Convert, DBNull, Exception

namespace BiblioAPI.Services
{
    // Responsable de la interacción con la base de datos para la entidad Libro.
    public class LibroService
    {
        private readonly string _connectionString;
        private readonly ILogger<LibroService> _logger; // Opcional: para logging

        // Constructor para inyectar IConfiguration y ILogger (opcional)
        public LibroService(IConfiguration configuration, ILogger<LibroService> logger)
        {
            _connectionString = configuration.GetConnectionString("MiConexion")
                ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'MiConexion' not found.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Asignar logger
        }

        // Método para obtener todos los libros usando Stored Procedure "ObtenerLibros"
        public async Task<List<LibroModel>> ObtenerLibrosAsync(string? tituloFilter = null, string? autorFilter = null)
        {
            // Registrar los filtros que se están aplicando (si los hay)
            _logger.LogInformation("Obteniendo libros con filtros: Titulo='{TituloFilter}', Autor='{AutorFilter}'",
                string.IsNullOrEmpty(tituloFilter) ? "N/A" : tituloFilter,
                string.IsNullOrEmpty(autorFilter) ? "N/A" : autorFilter);

            var libros = new List<LibroModel>();
            using (SqlConnection con = new SqlConnection(_connectionString)) // Asume _connectionString y _logger definidos
            {
                // Llamar al Stored Procedure "ObtenerLibros" (que ahora acepta filtros)
                using (SqlCommand cmd = new SqlCommand("ObtenerLibros", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir los parámetros para los filtros. Usar DBNull.Value si el filtro es nulo o vacío.
                    cmd.Parameters.AddWithValue("@TituloFilter",
                        string.IsNullOrWhiteSpace(tituloFilter) ? (object)DBNull.Value : tituloFilter);

                    cmd.Parameters.AddWithValue("@AutorFilter",
                        string.IsNullOrWhiteSpace(autorFilter) ? (object)DBNull.Value : autorFilter);

                    try
                    {
                        await con.OpenAsync();
                        _logger.LogDebug("Ejecutando SP ObtenerLibros con filtros...");
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                libros.Add(new LibroModel
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Titulo = reader["Titulo"].ToString() ?? string.Empty,
                                    Autor = reader["Autor"].ToString() ?? string.Empty,
                                    Editorial = reader["Editorial"].ToString() ?? string.Empty,
                                    ISBN = reader["ISBN"].ToString() ?? string.Empty,
                                    Anio = Convert.ToInt32(reader["Anio"]),
                                    Categoria = reader["Categoria"].ToString() ?? string.Empty,
                                    Existencias = Convert.ToInt32(reader["Existencias"])
                                });
                            }
                        }
                        _logger.LogInformation("Se obtuvieron {Count} libros aplicando filtros.", libros.Count);
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP ObtenerLibros con filtros.");
                        throw; // Re-lanzar
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en ObtenerLibrosAsync con filtros.");
                        throw; // Re-lanzar
                    }
                } // Libera SqlCommand
            } // Libera SqlConnection
            return libros;
        }

        // Método para obtener libro por ID usando Stored Procedure dedicado "ObtenerLibroPorId" (OPTIMIZADO)
        public async Task<LibroModel?> ObtenerLibroPorIdAsync(int id)
        {
            _logger.LogInformation("Intentando obtener libro por ID usando SP: {LibroId}", id);
            LibroModel? libro = null; // Inicializar como null

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("ObtenerLibroPorId", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);

                    try
                    {
                        await con.OpenAsync();
                        _logger.LogDebug("Ejecutando SP ObtenerLibroPorId para ID: {LibroId}", id);
                        using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow)) // Optimización: esperamos máximo una fila
                        {
                            if (await reader.ReadAsync())
                            {
                                libro = new LibroModel
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Titulo = reader["Titulo"].ToString() ?? string.Empty,
                                    Autor = reader["Autor"].ToString() ?? string.Empty,
                                    Editorial = reader["Editorial"].ToString() ?? string.Empty,
                                    ISBN = reader["ISBN"].ToString() ?? string.Empty,
                                    Anio = Convert.ToInt32(reader["Anio"]),
                                    Categoria = reader["Categoria"].ToString() ?? string.Empty,
                                    Existencias = Convert.ToInt32(reader["Existencias"])
                                };
                                _logger.LogInformation("Libro encontrado usando SP para ID: {LibroId}", id);
                            }
                            else
                            {
                                _logger.LogWarning("Libro no encontrado usando SP para ID: {LibroId}", id);
                            }
                        }
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP ObtenerLibroPorId para ID: {LibroId}", id);
                        throw; // Re-lanzar para que el controlador maneje el error
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en ObtenerLibroPorIdAsync para ID: {LibroId}", id);
                        throw; // Re-lanzar
                    }
                }
            }
            return libro; // Devuelve el libro encontrado o null
        }

        // Método para crear un libro usando Stored Procedure "InsertarLibro"
        public async Task CrearLibroAsync(LibroModel libro)
        {
            _logger.LogInformation("Intentando crear libro con título: {Titulo}", libro.Titulo);
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("InsertarLibro", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Titulo", libro.Titulo);
                    cmd.Parameters.AddWithValue("@Autor", libro.Autor);
                    cmd.Parameters.AddWithValue("@Editorial", libro.Editorial);
                    cmd.Parameters.AddWithValue("@ISBN", libro.ISBN);
                    cmd.Parameters.AddWithValue("@Anio", libro.Anio);
                    cmd.Parameters.AddWithValue("@Categoria", libro.Categoria);
                    cmd.Parameters.AddWithValue("@Existencias", libro.Existencias);

                    try
                    {
                        await con.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("Libro creado (SP InsertarLibro ejecutado) para título: {Titulo}", libro.Titulo);
                        // SP no devuelve ID
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // UNIQUE constraint (ISBN)
                    {
                        _logger.LogWarning(ex, "Error al crear libro: ISBN '{ISBN}' ya existe.", libro.ISBN);
                        // Considera lanzar una excepción personalizada o devolver un resultado específico
                        throw new InvalidOperationException($"El ISBN '{libro.ISBN}' ya existe.", ex);
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP InsertarLibro para título: {Titulo}", libro.Titulo);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en CrearLibroAsync para título: {Titulo}", libro.Titulo);
                        throw;
                    }
                }
            }
        }

        // Método para actualizar un libro usando Stored Procedure "ActualizarLibro"
        public async Task<bool> ActualizarLibroAsync(int id, LibroModel libro)
        {
            _logger.LogInformation("Intentando actualizar libro ID: {LibroId}", id);
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("ActualizarLibro", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Titulo", libro.Titulo);
                    cmd.Parameters.AddWithValue("@Autor", libro.Autor);
                    cmd.Parameters.AddWithValue("@Editorial", libro.Editorial);
                    cmd.Parameters.AddWithValue("@ISBN", libro.ISBN);
                    cmd.Parameters.AddWithValue("@Anio", libro.Anio);
                    cmd.Parameters.AddWithValue("@Categoria", libro.Categoria);
                    cmd.Parameters.AddWithValue("@Existencias", libro.Existencias);

                    try
                    {
                        await con.OpenAsync();
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("SP ActualizarLibro ejecutado para ID: {LibroId}. Filas afectadas: {RowsAffected}", id, rowsAffected);
                        return rowsAffected > 0; // true si se actualizó al menos una fila
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // UNIQUE constraint (ISBN)
                    {
                        _logger.LogWarning(ex, "Error al actualizar libro ID: {LibroId}. El ISBN '{ISBN}' ya existe para otro libro.", id, libro.ISBN);
                        throw new InvalidOperationException($"El ISBN '{libro.ISBN}' ya existe para otro libro.", ex);
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP ActualizarLibro para ID: {LibroId}", id);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en ActualizarLibroAsync para ID: {LibroId}", id);
                        throw;
                    }
                }
            }
        }

        // Método para eliminar un libro usando Stored Procedure "EliminarLibro"
        public async Task<bool> EliminarLibroAsync(int id)
        {
            _logger.LogInformation("Intentando eliminar libro ID: {LibroId}", id);
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("EliminarLibro", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);

                    try
                    {
                        await con.OpenAsync();
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("SP EliminarLibro ejecutado para ID: {LibroId}. Filas afectadas: {RowsAffected}", id, rowsAffected);
                        return rowsAffected > 0; // true si se eliminó
                    }
                    catch (SqlException ex) when (ex.Number == 50003) // Error personalizado del SP (préstamos pendientes)
                    {
                        _logger.LogWarning("No se pudo eliminar libro ID: {LibroId} debido a restricción (préstamos pendientes?). SQL Error: {SqlErrorNumber}", id, ex.Number);
                        return false; // Indicar que no se pudo eliminar
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP EliminarLibro para ID: {LibroId}", id);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en EliminarLibroAsync para ID: {LibroId}", id);
                        throw;
                    }
                }
            }
        }
    }
}
