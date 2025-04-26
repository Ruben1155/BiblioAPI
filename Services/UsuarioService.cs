using BiblioAPI.Models;
using System.Data;
using System.Data.SqlClient; // Asegúrate de tener el using
using Microsoft.Extensions.Configuration; // Para IConfiguration
using System.Collections.Generic; // Para List<>
using System.Threading.Tasks; // Para Task y async/await
using System; // Para ArgumentNullException, DBNull, Convert, etc.
using Microsoft.Extensions.Logging; // Para Logging (opcional pero recomendado)

namespace BiblioAPI.Services
{
    // --- BiblioAPI/Services/UsuarioService.cs ---
    // Responsable de la interacción con la base de datos para la entidad Usuario.
    public class UsuarioService
    {
        private readonly string _connectionString;
        private readonly ILogger<UsuarioService> _logger; // Opcional: para registrar información o errores

        // Constructor para inyectar la configuración y obtener la cadena de conexión
        // Se añade ILogger opcionalmente
        public UsuarioService(IConfiguration configuration, ILogger<UsuarioService> logger)
        {
            _connectionString = configuration.GetConnectionString("MiConexion")
                ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'MiConexion' not found.");
            _logger = logger;
        }

        // Método para obtener todos los usuarios
        // IMPORTANTE: Este método NO debe devolver la Clave (hash).
        public async Task<List<UsuarioModel>> ObtenerUsuariosAsync()
        {
            var usuarios = new List<UsuarioModel>();
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                // Usar el nombre exacto del Stored Procedure de tu BD
                using (SqlCommand cmd = new SqlCommand("ObtenerUsuarios", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    await con.OpenAsync();
                    _logger.LogInformation("Ejecutando SP ObtenerUsuarios...");
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            usuarios.Add(new UsuarioModel
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Nombre = reader["Nombre"].ToString() ?? string.Empty,
                                Apellido = reader["Apellido"].ToString() ?? string.Empty,
                                Correo = reader["Correo"].ToString() ?? string.Empty,
                                Telefono = reader["Telefono"] == DBNull.Value ? null : reader["Telefono"].ToString(), // Manejar DBNull correctamente
                                TipoUsuario = reader["TipoUsuario"].ToString() ?? string.Empty,
                                Clave = string.Empty // NUNCA devolver la clave/hash aquí
                            });
                        }
                    }
                }
            }
            _logger.LogInformation("Obtenidos {Count} usuarios.", usuarios.Count);
            return usuarios;
        }

        // Método para obtener usuario por ID usando Stored Procedure dedicado
        // IMPORTANTE: Este método NO debe devolver la Clave (hash). El SP ya la excluye.
        public async Task<UsuarioModel?> ObtenerUsuarioPorIdAsync(int id)
        {
            _logger.LogInformation("Intentando obtener usuario por ID usando SP: {UsuarioId}", id);
            UsuarioModel? usuario = null; // Inicializar como null

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                // Usar el nuevo Stored Procedure
                using (SqlCommand cmd = new SqlCommand("ObtenerUsuarioPorId", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    // Añadir el parámetro @Id que espera el Stored Procedure
                    cmd.Parameters.AddWithValue("@Id", id);

                    try
                    {
                        await con.OpenAsync();
                        _logger.LogDebug("Ejecutando SP ObtenerUsuarioPorId para ID: {UsuarioId}", id);
                        using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow)) // Optimización: esperamos máximo una fila
                        {
                            // Verificar si se encontró una fila
                            if (await reader.ReadAsync())
                            {
                                // Mapear los datos del reader al modelo
                                usuario = new UsuarioModel
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Nombre = reader["Nombre"].ToString() ?? string.Empty,
                                    Apellido = reader["Apellido"].ToString() ?? string.Empty,
                                    Correo = reader["Correo"].ToString() ?? string.Empty,
                                    Telefono = reader["Telefono"] == DBNull.Value ? null : reader["Telefono"].ToString(), // Manejar DBNull
                                    TipoUsuario = reader["TipoUsuario"].ToString() ?? string.Empty,
                                    Clave = string.Empty // Asegurarse de que la clave esté vacía en el modelo devuelto
                                };
                                _logger.LogInformation("Usuario encontrado usando SP para ID: {UsuarioId}", id);
                            }
                            else
                            {
                                // No se encontró ninguna fila para ese ID
                                _logger.LogWarning("Usuario no encontrado usando SP para ID: {UsuarioId}", id);
                            }
                        }
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al ejecutar SP ObtenerUsuarioPorId para ID: {UsuarioId}", id);
                        // Podrías lanzar la excepción o devolver null/manejar de otra forma
                        throw; // Re-lanzar para que el controlador maneje el error
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en ObtenerUsuarioPorIdAsync para ID: {UsuarioId}", id);
                        throw; // Re-lanzar
                    }
                } // El using libera el SqlCommand
            } // El using libera la SqlConnection

            return usuario; // Devuelve el usuario encontrado o null
        }

        // Método para crear un usuario
        // Recibe el UsuarioModel con la Clave ya hasheada desde el Controller.
        public async Task CrearUsuarioAsync(UsuarioModel usuario)
        {
            _logger.LogInformation("Intentando crear usuario con correo: {Correo}", usuario.Correo);
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                // El SP 'InsertarUsuario' ahora espera el hash en @ClaveHash
                using (SqlCommand cmd = new SqlCommand("InsertarUsuario", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", usuario.Apellido);
                    cmd.Parameters.AddWithValue("@Correo", usuario.Correo);
                    cmd.Parameters.AddWithValue("@Telefono", string.IsNullOrEmpty(usuario.Telefono) ? (object)DBNull.Value : usuario.Telefono);
                    cmd.Parameters.AddWithValue("@TipoUsuario", usuario.TipoUsuario);
                    // Pasar el HASH recibido del controlador al parámetro del SP
                    cmd.Parameters.AddWithValue("@ClaveHash", usuario.Clave); // Asegúrate que el SP espera @ClaveHash

                    await con.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Usuario creado (SP InsertarUsuario ejecutado) para correo: {Correo}", usuario.Correo);
                    // Nota: El SP no devuelve el ID generado.
                }
            }
        }

        // Método para actualizar un usuario
        // Recibe el UsuarioModel con la nueva Clave hasheada (si se actualiza) desde el Controller.
        public async Task<bool> ActualizarUsuarioAsync(int id, UsuarioModel usuario)
        {
            _logger.LogInformation("Intentando actualizar usuario ID: {UsuarioId}", id);
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                // El SP 'ActualizarUsuario' ahora espera el hash en @ClaveHash (opcional)
                using (SqlCommand cmd = new SqlCommand("ActualizarUsuario", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", usuario.Apellido);
                    cmd.Parameters.AddWithValue("@Correo", usuario.Correo);
                    cmd.Parameters.AddWithValue("@Telefono", string.IsNullOrEmpty(usuario.Telefono) ? (object)DBNull.Value : usuario.Telefono);
                    cmd.Parameters.AddWithValue("@TipoUsuario", usuario.TipoUsuario);

                    // Pasar el HASH al SP solo si se proporcionó uno nuevo en el modelo
                    // El SP modificado usa ISNULL o CASE para no actualizar si el parámetro es NULL.
                    if (!string.IsNullOrWhiteSpace(usuario.Clave))
                    {
                        _logger.LogInformation("Actualizando hash de contraseña para usuario ID: {UsuarioId}", id);
                        cmd.Parameters.AddWithValue("@ClaveHash", usuario.Clave); // Asegúrate que el SP espera @ClaveHash
                    }
                    else
                    {
                        _logger.LogInformation("No se proporcionó nueva contraseña. No se actualizará el hash para usuario ID: {UsuarioId}", id);
                        // No añadir el parámetro @ClaveHash o añadirlo con DBNull.Value si el SP lo requiere explícitamente
                        // cmd.Parameters.AddWithValue("@ClaveHash", DBNull.Value); // Si el SP espera el parámetro siempre
                    }


                    await con.OpenAsync();
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("SP ActualizarUsuario ejecutado para ID: {UsuarioId}. Filas afectadas: {RowsAffected}", id, rowsAffected);
                    return rowsAffected > 0;
                }
            }
        }

        // Método para eliminar el usuario
        public async Task<bool> EliminarUsuarioAsync(int id)
        {
            _logger.LogInformation("Intentando eliminar usuario ID: {UsuarioId}", id);
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("EliminarUsuario", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    await con.OpenAsync();
                    try
                    {
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("SP EliminarUsuario ejecutado para ID: {UsuarioId}. Filas afectadas: {RowsAffected}", id, rowsAffected);
                        return rowsAffected > 0;
                    }
                    catch (SqlException ex) when (ex.Number == 50005) // Código de error personalizado del SP (préstamos pendientes)
                    {
                        _logger.LogWarning("No se pudo eliminar usuario ID: {UsuarioId} debido a restricción (préstamos pendientes?). SQL Error: {SqlErrorNumber}", id, ex.Number);
                        return false; // Indicar que no se pudo eliminar
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error SQL al eliminar usuario ID: {UsuarioId}", id);
                        throw; // Re-lanzar otros errores SQL
                    }
                }
            }
        }

        // Método para OBTENER DATOS para la validación del usuario.
        // Ya NO valida directamente. Devuelve el usuario CON SU HASH para que el Controller valide.
        public async Task<UsuarioModel?> ObtenerUsuarioParaValidacionAsync(string correo)
        {
            _logger.LogInformation("Obteniendo datos de usuario para validación con correo: {Correo}", correo);
            UsuarioModel? usuario = null;
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                // Usar el SP 'ValidarUsuario' que ahora solo toma @Correo y devuelve el hash
                using (SqlCommand cmd = new SqlCommand("ValidarUsuario", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Correo", correo);
                    // Ya no se pasa @Clave al SP

                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            usuario = new UsuarioModel
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Nombre = reader["Nombre"].ToString() ?? string.Empty,
                                Apellido = reader["Apellido"].ToString() ?? string.Empty,
                                Correo = reader["Correo"].ToString() ?? string.Empty,
                                Telefono = reader["Telefono"] == DBNull.Value ? null : reader["Telefono"].ToString(),
                                TipoUsuario = reader["TipoUsuario"].ToString() ?? string.Empty,
                                // *** Recuperar el HASH almacenado ***
                                // Asegúrate que el SP devuelve la columna con el hash (ej. 'ClaveHash')
                                Clave = reader["ClaveHash"].ToString() ?? string.Empty
                            };
                            _logger.LogInformation("Usuario encontrado para validación con correo: {Correo}. Hash recuperado.", correo);
                        }
                        else
                        {
                            _logger.LogWarning("Usuario NO encontrado para validación con correo: {Correo}", correo);
                        }
                    }
                }
            }
            // Retorna el usuario (con hash) si se encuentra, o null si no.
            // La validación real (comparación de hash) la hará el Controller.
            return usuario;
        }
    }
}
