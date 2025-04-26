// --- BiblioAPI/Controllers/PrestamoController.cs ---
using BiblioAPI.Models;
using BiblioAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Necesario para ILogger
using System; // Necesario para Exception, ArgumentNullException
using System.Collections.Generic; // Necesario para List<>
using System.Threading.Tasks; // Necesario para Task

namespace BiblioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Ruta base: api/prestamo
    public class PrestamoController : ControllerBase
    {
        private readonly PrestamoService _prestamoService;
        private readonly ILogger<PrestamoController> _logger; // Inyectar ILogger

        // Constructor con inyección de PrestamoService y ILogger
        public PrestamoController(PrestamoService prestamoService, ILogger<PrestamoController> logger)
        {
            _prestamoService = prestamoService ?? throw new ArgumentNullException(nameof(prestamoService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/prestamo
        [HttpGet]
        public async Task<ActionResult<List<PrestamoModel>>> ObtenerPrestamos()
        {
            _logger.LogInformation("Solicitud GET recibida para obtener todos los préstamos.");
            try
            {
                var prestamos = await _prestamoService.ObtenerPrestamosAsync();
                _logger.LogInformation("Se obtuvieron {Count} préstamos.", prestamos.Count);
                return Ok(prestamos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al obtener todos los préstamos.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al obtener los préstamos.");
            }
        }

        // POST: api/prestamo
        [HttpPost]
        public async Task<ActionResult<PrestamoModel>> RegistrarPrestamo([FromBody] PrestamoModel prestamo)
        {
            _logger.LogInformation("Solicitud POST recibida para registrar préstamo para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo?.IdUsuario, prestamo?.IdLibro);
            if (prestamo == null || !ModelState.IsValid)
            {
                _logger.LogWarning("Modelo para registrar préstamo inválido.");
                return BadRequest(ModelState);
            }

            try
            {
                await _prestamoService.RegistrarPrestamoAsync(prestamo);
                _logger.LogInformation("Préstamo registrado exitosamente para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo.IdUsuario, prestamo.IdLibro);
                // Devolver 200 OK con el objeto enviado (sin ID preciso)
                // Idealmente sería 201 Created si el SP devolviera el ID.
                return Ok(prestamo);
                // return CreatedAtAction(nameof(ObtenerPrestamos), new { id = prestamo.Id }, prestamo); // Idealmente
            }
            catch (InvalidOperationException ex) // Capturar errores específicos del servicio (ej. no hay stock, usuario/libro no existe)
            {
                _logger.LogWarning(ex, "Error de operación al registrar préstamo para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo.IdUsuario, prestamo.IdLibro);
                // Devolver 400 Bad Request o 409 Conflict según el caso
                // El mensaje de la excepción (ex.Message) ya viene del SP o del servicio
                return BadRequest(new ProblemDetails { Title = "Error al registrar préstamo", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (Exception ex) // Otros errores inesperados
            {
                _logger.LogError(ex, "Error inesperado al registrar préstamo para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo.IdUsuario, prestamo.IdLibro);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al registrar el préstamo.");
            }
        }

        // PUT: api/prestamo/5
        [HttpPut("{id:int}")] // Restricción de tipo
        public async Task<IActionResult> ActualizarPrestamo(int id, [FromBody] PrestamoModel prestamo)
        {
            _logger.LogInformation("Solicitud PUT recibida para actualizar préstamo ID: {PrestamoId}", id);
            // Validar IDs y modelo
            if (prestamo == null || id <= 0 || id != prestamo.Id || !ModelState.IsValid)
            {
                _logger.LogWarning("Actualización fallida: Datos inválidos o IDs no coinciden para ID: {PrestamoId}", id);
                return BadRequest("Datos inválidos o IDs no coinciden.");
            }

            try
            {
                var actualizado = await _prestamoService.ActualizarPrestamoAsync(id, prestamo);

                if (!actualizado)
                {
                    // Verificar si no se encontró o no se pudo actualizar (ej. ya devuelto)
                    // El servicio podría devolver false si rowsAffected = 0
                    // Necesitaríamos un GetPrestamoById para verificar existencia si quisiéramos ser más precisos
                    _logger.LogWarning("Actualización fallida: Préstamo ID: {PrestamoId} no encontrado o no se pudo actualizar.", id);
                    return NotFound(new ProblemDetails { Title = "Préstamo no encontrado o no se pudo actualizar", Status = StatusCodes.Status404NotFound, Detail = $"No se encontró o no se pudo actualizar el préstamo con ID {id}." });
                }

                _logger.LogInformation("Préstamo ID: {PrestamoId} actualizado exitosamente.", id);
                return NoContent(); // 204 Éxito
            }
            catch (InvalidOperationException ex) // Capturar errores específicos del servicio
            {
                _logger.LogWarning(ex, "Error de operación al actualizar préstamo ID: {PrestamoId}", id);
                return BadRequest(new ProblemDetails { Title = "Error al actualizar préstamo", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar préstamo ID: {PrestamoId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al actualizar el préstamo.");
            }
        }

        // DELETE: api/prestamo/5
        [HttpDelete("{id:int}")] // Restricción de tipo
        public async Task<IActionResult> EliminarPrestamo(int id)
        {
            _logger.LogInformation("Solicitud DELETE recibida para préstamo ID: {PrestamoId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("Eliminación fallida: ID inválido: {PrestamoId}", id);
                return BadRequest("ID de préstamo inválido.");
            }

            try
            {
                var eliminado = await _prestamoService.EliminarPrestamoAsync(id);

                if (!eliminado)
                {
                    // El servicio devuelve false si no se encontró
                    // Necesitaríamos un GetPrestamoById para confirmar
                    _logger.LogWarning("Eliminación fallida: Préstamo ID: {PrestamoId} no encontrado.", id);
                    return NotFound(new ProblemDetails { Title = "Préstamo no encontrado", Status = StatusCodes.Status404NotFound, Detail = $"No se encontró un préstamo con ID {id} para eliminar." });
                }

                _logger.LogInformation("Préstamo ID: {PrestamoId} eliminado exitosamente.", id);
                return NoContent(); // 204 Éxito
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al eliminar préstamo ID: {PrestamoId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al eliminar el préstamo.");
            }
        }
    }
}
