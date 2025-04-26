// --- BiblioAPI/Services/PrestamoService.cs ---
using BiblioAPI.Models;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration; // Para IConfiguration
using System.Collections.Generic; // Para List<>
using System.Threading.Tasks; // Para Task y async/await
using Microsoft.Extensions.Logging; // Para ILogger
using System; // Para ArgumentNullException, Convert, DBNull, Exception, InvalidOperationException

namespace BiblioAPI.Services
{
    // Responsable de la interacción con la base de datos para la entidad Préstamo.
    public class PrestamoService
    {
        private readonly string _connectionString;
        private readonly ILogger<PrestamoService> _logger; // Inyectar ILogger

        // Constructor para inyectar IConfiguration y ILogger
        public PrestamoService(IConfiguration configuration, ILogger<PrestamoService> logger)
        {
            _connectionString = configuration.GetConnectionString("MiConexion")
                ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'MiConexion' not found.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Asignar logger
        }

        // Método para obtener todos los préstamos usando Stored Procedure "ObtenerPrestamos"
        public async Task<List<PrestamoModel>> ObtenerPrestamosAsync()
        {
            _logger.LogInformation("Obteniendo todos los préstamos usando SP ObtenerPrestamos...");
            var prestamos = new List<PrestamoModel>();
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                // Usar el SP que devuelve todos los préstamos (posiblemente con JOINs)
                using (SqlCommand cmd = new SqlCommand("ObtenerPrestamos", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    try
                    {
                        await con.OpenAsync();
                        _logger.LogDebug("Ejecutando SP ObtenerPrestamos...");
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                prestamos.Add(new PrestamoModel
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    IdUsuario = Convert.ToInt32(reader["IdUsuario"]),
                                    IdLibro = Convert.ToInt32(reader["IdLibro"]),
                                    FechaPrestamo = Convert.ToDateTime(reader["FechaPrestamo"]),
                                    FechaDevolucionEsperada = Convert.ToDateTime(reader["FechaDevolucionEsperada"]),
                                    // Manejar posible null en FechaDevolucionReal
                                    FechaDevolucionReal = reader["FechaDevolucionReal"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["FechaDevolucionReal"]),
                                    Estado = reader["Estado"].ToString() ?? string.Empty
                                    // Si el SP ObtenerPrestamos hace JOINs, leer campos adicionales:
                                    // NombreUsuario = reader["NombreUsuario"] == DBNull.Value ? null : reader["NombreUsuario"].ToString(),
                                    // TituloLibro = reader["TituloLibro"] == DBNull.Value ? null : reader["TituloLibro"].ToString()
                                });
                            }
                        }
                        _logger.LogInformation("Se obtuvieron {Count} préstamos.", prestamos.Count);
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP ObtenerPrestamos.");
                        throw; // Re-lanzar para que el controlador maneje
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en ObtenerPrestamosAsync.");
                        throw; // Re-lanzar
                    }
                } // Libera SqlCommand
            } // Libera SqlConnection
            return prestamos;
        }

        // Método para registrar un préstamo usando Stored Procedure "RegistrarPrestamo"
        public async Task RegistrarPrestamoAsync(PrestamoModel prestamo)
        {
            _logger.LogInformation("Intentando registrar préstamo para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo.IdUsuario, prestamo.IdLibro);
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("RegistrarPrestamo", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdUsuario", prestamo.IdUsuario);
                    cmd.Parameters.AddWithValue("@IdLibro", prestamo.IdLibro);
                    cmd.Parameters.AddWithValue("@FechaDevolucionEsperada", prestamo.FechaDevolucionEsperada);
                    // El estado puede tener un valor por defecto en el SP o pasarse aquí
                    if (!string.IsNullOrEmpty(prestamo.Estado))
                    {
                        cmd.Parameters.AddWithValue("@Estado", prestamo.Estado);
                        _logger.LogDebug("Estado proporcionado para nuevo préstamo: {Estado}", prestamo.Estado);
                    }
                    else
                    {
                        _logger.LogDebug("No se proporcionó estado para nuevo préstamo, se usará el default del SP.");
                    }

                    try
                    {
                        await con.OpenAsync();
                        _logger.LogDebug("Ejecutando SP RegistrarPrestamo...");
                        await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("Préstamo registrado (SP RegistrarPrestamo ejecutado) para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo.IdUsuario, prestamo.IdLibro);
                        // SP no devuelve ID
                    }
                    catch (SqlException ex) when (ex.Number >= 50000) // Errores personalizados del SP (ej. no stock, no existe user/libro)
                    {
                        _logger.LogWarning(ex, "Error de negocio al registrar préstamo (Usuario ID: {UsuarioId}, Libro ID: {LibroId}). Mensaje SQL: {SqlMessage}", prestamo.IdUsuario, prestamo.IdLibro, ex.Message);
                        // Re-lanzar como InvalidOperationException para que el controlador devuelva BadRequest/Conflict
                        throw new InvalidOperationException($"Error al registrar préstamo: {ex.Message}", ex);
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP RegistrarPrestamo para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo.IdUsuario, prestamo.IdLibro);
                        throw; // Re-lanzar otros errores SQL
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en RegistrarPrestamoAsync para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo.IdUsuario, prestamo.IdLibro);
                        throw; // Re-lanzar
                    }
                }
            }
        }

        // Método para actualizar un préstamo (ej. registrar devolución) usando Stored Procedure "ActualizarPrestamo"
        public async Task<bool> ActualizarPrestamoAsync(int id, PrestamoModel prestamo)
        {
            _logger.LogInformation("Intentando actualizar préstamo ID: {PrestamoId} con estado: {Estado}", id, prestamo.Estado);
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("ActualizarPrestamo", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    // Pasar FechaDevolucionReal solo si tiene valor
                    cmd.Parameters.AddWithValue("@FechaDevolucionReal", prestamo.FechaDevolucionReal.HasValue ? (object)prestamo.FechaDevolucionReal.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Estado", prestamo.Estado);

                    try
                    {
                        await con.OpenAsync();
                        _logger.LogDebug("Ejecutando SP ActualizarPrestamo para ID: {PrestamoId}...", id);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("SP ActualizarPrestamo ejecutado para ID: {PrestamoId}. Filas afectadas: {RowsAffected}", id, rowsAffected);
                        return rowsAffected > 0; // true si se actualizó al menos una fila
                    }
                    catch (SqlException ex) when (ex.Number >= 50000) // Errores personalizados del SP (ej. estado inválido, préstamo no encontrado)
                    {
                        _logger.LogWarning(ex, "Error de negocio al actualizar préstamo ID: {PrestamoId}. Mensaje SQL: {SqlMessage}", id, ex.Message);
                        // Podríamos lanzar InvalidOperationException o simplemente devolver false
                        // throw new InvalidOperationException($"Error al actualizar préstamo: {ex.Message}", ex);
                        return false; // Indicar que no se pudo actualizar debido a regla de negocio o no encontrado
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP ActualizarPrestamo para ID: {PrestamoId}", id);
                        throw; // Re-lanzar otros errores SQL
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en ActualizarPrestamoAsync para ID: {PrestamoId}", id);
                        throw; // Re-lanzar
                    }
                }
            }
        }

        // Método para eliminar un préstamo usando Stored Procedure "EliminarPrestamo"
        public async Task<bool> EliminarPrestamoAsync(int id)
        {
            _logger.LogInformation("Intentando eliminar préstamo ID: {PrestamoId}", id);
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("EliminarPrestamo", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);

                    try
                    {
                        await con.OpenAsync();
                        _logger.LogDebug("Ejecutando SP EliminarPrestamo para ID: {PrestamoId}...", id);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("SP EliminarPrestamo ejecutado para ID: {PrestamoId}. Filas afectadas: {RowsAffected}", id, rowsAffected);
                        return rowsAffected > 0; // true si se eliminó
                    }
                    catch (SqlException ex) when (ex.Number >= 50000) // Errores personalizados del SP (si los hubiera)
                    {
                        _logger.LogWarning(ex, "Error de negocio al eliminar préstamo ID: {PrestamoId}. Mensaje SQL: {SqlMessage}", id, ex.Message);
                        return false; // Indicar que no se pudo eliminar
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP EliminarPrestamo para ID: {PrestamoId}", id);
                        throw; // Re-lanzar otros errores SQL
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en EliminarPrestamoAsync para ID: {PrestamoId}", id);
                        throw; // Re-lanzar
                    }
                }
            }
        }
    }
}
