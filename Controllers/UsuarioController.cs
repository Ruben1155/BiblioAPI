using BiblioAPI.Models;
using BiblioAPI.Services;
using Microsoft.AspNetCore.Mvc; // Necesario para [ApiController], [Route], ControllerBase, etc.
using System.Collections.Generic; // Para List<>
using System.Threading.Tasks; // Para Task y async/await
using Microsoft.AspNetCore.Identity; // Necesario para IPasswordHasher y PasswordVerificationResult
using Microsoft.Extensions.Logging; // Necesario para ILogger
using System; // Necesario para Exception, ArgumentNullException
using System.ComponentModel.DataAnnotations; // Necesario para [Required], [EmailAddress]
using System.Data.SqlClient; // Necesario para SqlException

namespace BiblioAPI.Controllers
{
    // --- Modelo para la solicitud de login ---
    public class LoginRequest
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La clave es obligatoria.")]
        public string Clave { get; set; } = string.Empty;
    }


    // --- BiblioAPI/Controllers/UsuarioController.cs ---
    [ApiController]
    [Route("api/[controller]")] // Ruta base: api/usuario
    public class UsuarioController : ControllerBase
    {
        private readonly UsuarioService _usuarioService;
        private readonly IPasswordHasher<UsuarioModel> _passwordHasher; // Inyectar el hasher integrado
        private readonly ILogger<UsuarioController> _logger;        // Inyectar el logger

        // Inyectar los servicios necesarios en el constructor
        public UsuarioController(UsuarioService usuarioService, IPasswordHasher<UsuarioModel> passwordHasher, ILogger<UsuarioController> logger)
        {
            _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/usuario
        // Obtiene todos los usuarios (sin claves/hashes)
        [HttpGet]
        public async Task<ActionResult<List<UsuarioModel>>> ObtenerUsuarios()
        {
            _logger.LogInformation("Solicitud GET recibida para obtener todos los usuarios.");
            var usuarios = await _usuarioService.ObtenerUsuariosAsync();
            // El servicio ya se encarga de no devolver hashes.
            return Ok(usuarios);
        }

        // GET: api/usuario/5
        // Obtiene un usuario por ID (sin clave/hash)
        [HttpGet("{id:int}")] // Añadir restricción de tipo :int
        public async Task<ActionResult<UsuarioModel>> ObtenerUsuarioPorId(int id)
        {
            _logger.LogInformation("Solicitud GET recibida para obtener usuario por ID: {UsuarioId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("Solicitud GET para usuario con ID inválido: {UsuarioId}", id);
                return BadRequest("ID de usuario inválido.");
            }

            var usuario = await _usuarioService.ObtenerUsuarioPorIdAsync(id);
            if (usuario == null)
            {
                _logger.LogWarning("Usuario con ID: {UsuarioId} no encontrado.", id);
                return NotFound(); // 404 si no se encuentra
            }
            // El servicio ya se encarga de no devolver hashes.
            return Ok(usuario);
        }

        // POST: api/usuario/validar
        // Valida las credenciales del usuario de forma SEGURA usando Hashing
        [HttpPost("validar")]
        // [AllowAnonymous] // Descomentar si se implementa autenticación global en la API
        public async Task<ActionResult<UsuarioModel>> ValidarUsuario([FromBody] LoginRequest loginRequest)
        {
            _logger.LogInformation("Solicitud POST recibida para validar usuario con correo: {Correo}", loginRequest.Correo);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Modelo de LoginRequest inválido para correo: {Correo}", loginRequest.Correo);
                return BadRequest(ModelState);
            }

            // 1. Obtener el usuario y su HASH almacenado usando el método del servicio modificado
            var usuarioAlmacenado = await _usuarioService.ObtenerUsuarioParaValidacionAsync(loginRequest.Correo);

            if (usuarioAlmacenado == null)
            {
                _logger.LogWarning("Validación fallida: Usuario no encontrado para correo: {Correo}", loginRequest.Correo);
                // Devolver Unauthorized para no dar pistas si el usuario existe o no
                return Unauthorized("Credenciales inválidas."); // 401
            }

            _logger.LogInformation("Usuario encontrado para validación: {Correo}. Procediendo a verificar hash.", loginRequest.Correo);

            // 2. Verificar la contraseña proporcionada contra el HASH almacenado
            var verificationResult = _passwordHasher.VerifyHashedPassword(
                usuarioAlmacenado,      // El objeto usuario (puede ser null si no se usa para factores de hashing)
                usuarioAlmacenado.Clave, // El HASH recuperado de la BD
                loginRequest.Clave       // La contraseña en texto plano enviada por el cliente
            );

            // 3. Evaluar el resultado
            if (verificationResult == PasswordVerificationResult.Success || verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                _logger.LogInformation("Validación exitosa para correo: {Correo}. Resultado: {VerificationResult}", loginRequest.Correo, verificationResult);

                // Opcional: Si es SuccessRehashNeeded, se podría actualizar el hash en la BD
                if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    _logger.LogInformation("Se necesita re-hashear la contraseña para el usuario: {Correo}", loginRequest.Correo);
                    // Lógica para actualizar el hash (requiere modificar ActualizarUsuarioAsync o un método dedicado)
                }

                // Devolver el usuario (SIN el hash)
                usuarioAlmacenado.Clave = string.Empty;

                // Aquí se podría generar un token JWT o una cookie si se implementa autenticación completa
                return Ok(usuarioAlmacenado);
            }
            else
            {
                _logger.LogWarning("Validación fallida para correo: {Correo}. Contraseña incorrecta. Resultado: {VerificationResult}", loginRequest.Correo, verificationResult);
                // Contraseña incorrecta
                return Unauthorized("Credenciales inválidas."); // 401
            }
        }


        // POST: api/usuario
        // Crea un nuevo usuario.
        // Si se proporciona una clave en el body (registro público), se hashea esa.
        // Si no se proporciona clave (creación por admin), se genera y hashea una por defecto.
        [HttpPost]
        public async Task<ActionResult<UsuarioModel>> CrearUsuario([FromBody] UsuarioModel usuarioInput) // Renombrado para claridad
        {
            _logger.LogInformation("Solicitud POST recibida para crear usuario con correo: {Correo}", usuarioInput?.Correo);

            // Crear el modelo base para guardar, copiando datos seguros
            var usuarioParaGuardar = new UsuarioModel
            {
                Nombre = usuarioInput?.Nombre ?? string.Empty,
                Apellido = usuarioInput?.Apellido ?? string.Empty,
                Correo = usuarioInput?.Correo ?? string.Empty,
                Telefono = usuarioInput?.Telefono,
                TipoUsuario = usuarioInput?.TipoUsuario ?? string.Empty
                // La clave se manejará a continuación
            };

            // Validar campos básicos requeridos (Nombre, Apellido, Correo, TipoUsuario)
            // Podrías usar DataAnnotations en un DTO específico para la entrada si prefieres
            if (string.IsNullOrWhiteSpace(usuarioParaGuardar.Nombre) ||
                string.IsNullOrWhiteSpace(usuarioParaGuardar.Apellido) ||
                string.IsNullOrWhiteSpace(usuarioParaGuardar.Correo) ||
                string.IsNullOrWhiteSpace(usuarioParaGuardar.TipoUsuario))
            {
                _logger.LogWarning("Modelo para crear usuario inválido (campos faltantes) para correo: {Correo}", usuarioParaGuardar.Correo);
                return BadRequest(new ProblemDetails { Title = "Datos incompletos", Detail = "Nombre, Apellido, Correo y Tipo de Usuario son obligatorios.", Status = StatusCodes.Status400BadRequest });
            }
            // Podrías añadir más validaciones aquí (ej. formato correo)

            string hashedPassword;
            // Verificar si se proporcionó una contraseña en la solicitud
            if (!string.IsNullOrWhiteSpace(usuarioInput?.Clave))
            {
                // Caso: Registro público - Hashear la contraseña proporcionada
                _logger.LogInformation("Se proporcionó contraseña para nuevo usuario: {Correo}. Hasheando...", usuarioParaGuardar.Correo);
                hashedPassword = _passwordHasher.HashPassword(usuarioParaGuardar, usuarioInput.Clave);
            }
            else
            {
                // Caso: Creación por Admin (sin contraseña) - Generar y hashear contraseña por defecto
                _logger.LogInformation("No se proporcionó contraseña para nuevo usuario: {Correo}. Generando contraseña por defecto y hasheando...", usuarioParaGuardar.Correo);
                const string contraseñaPorDefecto = "P@ssw0rd123!"; // ¡CAMBIAR ESTO EN PRODUCCIÓN!
                hashedPassword = _passwordHasher.HashPassword(usuarioParaGuardar, contraseñaPorDefecto);
            }

            // Asignar el hash resultante
            usuarioParaGuardar.Clave = hashedPassword;

            try
            {
                // Llamar al servicio para insertar en BD
                await _usuarioService.CrearUsuarioAsync(usuarioParaGuardar);
                _logger.LogInformation("Usuario creado exitosamente en BD para correo: {Correo}", usuarioParaGuardar.Correo);

                // Devolver el usuario creado (sin hash)
                usuarioParaGuardar.Clave = string.Empty;

                // Devolver 200 OK con el objeto (sin ID preciso, ya que el SP no lo devuelve)
                return Ok(usuarioParaGuardar);
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // UNIQUE constraint violation (Correo)
            {
                _logger.LogWarning(ex, "Error al crear usuario: Correo '{Correo}' ya existe.", usuarioParaGuardar.Correo);
                return Conflict(new ProblemDetails { Title = "Conflicto al crear usuario", Detail = $"El correo electrónico '{usuarioParaGuardar.Correo}' ya está registrado.", Status = StatusCodes.Status409Conflict });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al crear usuario {Correo}", usuarioParaGuardar.Correo);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al crear el usuario.");
            }
        }


        // PUT: api/usuario/5
        // Actualiza un usuario existente. Hashea la contraseña si se proporciona una nueva.
        [HttpPut("{id:int}")]
        // [Authorize] // Proteger si hay autenticación
        public async Task<IActionResult> ActualizarUsuario(int id, [FromBody] UsuarioModel usuario)
        {
            _logger.LogInformation("Solicitud PUT recibida para actualizar usuario ID: {UsuarioId}", id);
            // Validar IDs y modelo
            if (usuario == null || id <= 0 || id != usuario.Id)
            {
                _logger.LogWarning("Actualización fallida: Datos inválidos o IDs no coinciden para ID: {UsuarioId}", id);
                return BadRequest("Datos inválidos o IDs no coinciden.");
            }
            // Remover validación de Clave del ModelState si viene del formulario de edición (que no la tiene)
            // Opcional: Podrías tener un ViewModel diferente para la actualización que no incluya Clave
            ModelState.Remove(nameof(UsuarioModel.Clave));

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Actualización fallida: Modelo inválido para ID: {UsuarioId}", id);
                return BadRequest(ModelState);
            }

            // Preparar el modelo para el servicio
            var usuarioParaActualizar = new UsuarioModel
            {
                Id = id,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Correo = usuario.Correo,
                Telefono = usuario.Telefono,
                TipoUsuario = usuario.TipoUsuario,
                // La clave se manejará específicamente si se quiere actualizar (ej. desde un endpoint dedicado)
                Clave = null! // Asegurarse de no pasar la clave desde un formulario de edición normal
            };

            // Lógica para ACTUALIZAR CONTRASEÑA (si se implementara un flujo específico para ello):
            // if (!string.IsNullOrWhiteSpace(usuario.Clave)) // Si viene una nueva clave
            // {
            //     _logger.LogInformation("Se proporcionó nueva contraseña para usuario ID: {UsuarioId}. Hasheando...", id);
            //     usuarioParaActualizar.Clave = _passwordHasher.HashPassword(usuario, usuario.Clave);
            // }
            // else
            // {
            //      usuarioParaActualizar.Clave = null!; // No actualizar clave
            // }

            try
            {
                var actualizado = await _usuarioService.ActualizarUsuarioAsync(id, usuarioParaActualizar);

                if (!actualizado)
                {
                    // Verificar si el usuario realmente no existe
                    var existe = await _usuarioService.ObtenerUsuarioPorIdAsync(id);
                    if (existe == null)
                    {
                        _logger.LogWarning("Actualización fallida: Usuario ID: {UsuarioId} no encontrado.", id);
                        return NotFound(); // 404 si no existe
                    }
                    else
                    {
                        _logger.LogWarning("Actualización fallida: Usuario ID: {UsuarioId} encontrado pero no se actualizó (posiblemente datos iguales o error inesperado).", id);
                        return StatusCode(StatusCodes.Status304NotModified); // O devolver un error genérico
                    }
                }

                _logger.LogInformation("Usuario ID: {UsuarioId} actualizado exitosamente.", id);
                return NoContent(); // 204 Éxito sin contenido
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // Error de correo duplicado al actualizar
            {
                _logger.LogWarning(ex, "Error al actualizar usuario ID: {UsuarioId}. El correo '{Correo}' ya existe para otro usuario.", id, usuario.Correo);
                return Conflict(new ProblemDetails { Title = "Conflicto al actualizar usuario", Detail = $"El correo electrónico '{usuario.Correo}' ya está registrado para otro usuario.", Status = StatusCodes.Status409Conflict });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar usuario ID: {UsuarioId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al actualizar el usuario.");
            }
        }

        // DELETE: api/usuario/5
        // Elimina un usuario
        [HttpDelete("{id:int}")]
        // [Authorize(Roles = "Administrador")] // Ejemplo: Proteger para que solo admins puedan borrar
        public async Task<IActionResult> EliminarUsuario(int id)
        {
            _logger.LogInformation("Solicitud DELETE recibida para usuario ID: {UsuarioId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("Eliminación fallida: ID inválido: {UsuarioId}", id);
                return BadRequest("ID de usuario inválido.");
            }

            try
            {
                var eliminado = await _usuarioService.EliminarUsuarioAsync(id);

                if (!eliminado)
                {
                    // Verificar si fue porque no existía o por restricción (préstamos)
                    var existe = await _usuarioService.ObtenerUsuarioPorIdAsync(id);
                    if (existe == null)
                    {
                        _logger.LogWarning("Eliminación fallida: Usuario ID: {UsuarioId} no encontrado.", id);
                        return NotFound(); // 404 No encontrado
                    }
                    else
                    {
                        _logger.LogWarning("Eliminación fallida: Usuario ID: {UsuarioId} no se pudo eliminar (posiblemente préstamos pendientes u otro error).", id);
                        // Podría ser 409 Conflict si el servicio detectó la restricción
                        return Conflict(new ProblemDetails { Title = "Conflicto al eliminar", Detail = "No se pudo eliminar el usuario. Puede tener préstamos asociados.", Status = StatusCodes.Status409Conflict });
                    }
                }

                _logger.LogInformation("Usuario ID: {UsuarioId} eliminado exitosamente.", id);
                return NoContent(); // 204 Éxito sin contenido
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al eliminar usuario ID: {UsuarioId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al eliminar el usuario.");
            }
        }
    } // Fin clase UsuarioController
} // Fin namespace