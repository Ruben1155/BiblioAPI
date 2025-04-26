// --- BiblioAPI/Controllers/LibroController.cs ---
using BiblioAPI.Models;
using BiblioAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Necesario para ILogger
using System; // Necesario para Exception, ArgumentNullException
using System.Collections.Generic; // Necesario para IEnumerable
using System.Threading.Tasks; // Necesario para Task

namespace BiblioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Ruta base: api/libro
    public class LibroController : ControllerBase
    {
        private readonly LibroService _libroService;
        private readonly ILogger<LibroController> _logger;

        // Constructor con inyección de LibroService y ILogger
        public LibroController(LibroService libroService, ILogger<LibroController> logger)
        {
            _libroService = libroService ?? throw new ArgumentNullException(nameof(libroService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/libro
        // AHORA acepta filtros opcionales desde la query string
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LibroModel>>> GetLibros(
            [FromQuery] string? tituloFilter = null, // Recibe el filtro de título desde la URL (?tituloFilter=...)
            [FromQuery] string? autorFilter = null)  // Recibe el filtro de autor desde la URL (?autorFilter=...)
        {
            _logger.LogInformation("Solicitud GET recibida para obtener libros con filtros: Titulo='{TituloFilter}', Autor='{AutorFilter}'",
                 string.IsNullOrEmpty(tituloFilter) ? "N/A" : tituloFilter,
                 string.IsNullOrEmpty(autorFilter) ? "N/A" : autorFilter);
            try
            {
                // Pasar los filtros recibidos al método del servicio
                var libros = await _libroService.ObtenerLibrosAsync(tituloFilter, autorFilter);
                _logger.LogInformation("Se obtuvieron {Count} libros aplicando filtros.", libros.Count);
                return Ok(libros);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al obtener libros con filtros.");
                // Devolver un error 500 genérico
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Error interno del servidor", Detail = "Ocurrió un error inesperado al procesar la solicitud de libros.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        // GET: api/libro/5
        [HttpGet("{id:int}")] // Restricción de tipo
        public async Task<ActionResult<LibroModel>> GetLibroPorId(int id)
        {
            _logger.LogInformation("Solicitud GET recibida para obtener libro por ID: {LibroId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("Solicitud GET para libro con ID inválido: {LibroId}", id);
                return BadRequest("ID de libro inválido.");
            }

            try
            {
                var libro = await _libroService.ObtenerLibroPorIdAsync(id);
                if (libro == null)
                {
                    _logger.LogWarning("Libro con ID: {LibroId} no encontrado.", id);
                    return NotFound(new ProblemDetails { Title = "Libro no encontrado", Status = StatusCodes.Status404NotFound, Detail = $"No se encontró un libro con ID {id}." });
                }
                _logger.LogInformation("Libro con ID: {LibroId} encontrado.", id);
                return Ok(libro);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al obtener libro por ID: {LibroId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Error interno del servidor", Detail = "Ocurrió un error inesperado al obtener el libro.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        // POST: api/libro
        [HttpPost]
        public async Task<ActionResult<LibroModel>> CrearLibro([FromBody] LibroModel libro)
        {
            _logger.LogInformation("Solicitud POST recibida para crear libro con título: {Titulo}", libro?.Titulo);
            if (libro == null || !ModelState.IsValid)
            {
                _logger.LogWarning("Modelo para crear libro inválido.");
                return BadRequest(ModelState);
            }

            try
            {
                await _libroService.CrearLibroAsync(libro);
                _logger.LogInformation("Libro creado exitosamente con título: {Titulo}", libro.Titulo);
                // Devolver 200 OK con el objeto creado (sin ID preciso)
                return Ok(libro);
            }
            catch (InvalidOperationException ex) // Capturar error específico de ISBN duplicado del servicio
            {
                _logger.LogWarning(ex, "Conflicto al crear libro con título: {Titulo}", libro.Titulo);
                return Conflict(new ProblemDetails { Title = "Conflicto al crear libro", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al crear libro con título: {Titulo}", libro.Titulo);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Error interno del servidor", Detail = "Ocurrió un error inesperado al crear el libro.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        // PUT: api/libro/5
        [HttpPut("{id:int}")] // Restricción de tipo
        public async Task<IActionResult> ActualizarLibro(int id, [FromBody] LibroModel libro)
        {
            _logger.LogInformation("Solicitud PUT recibida para actualizar libro ID: {LibroId}", id);
            if (libro == null || id <= 0 || id != libro.Id || !ModelState.IsValid)
            {
                _logger.LogWarning("Actualización fallida: Datos inválidos o IDs no coinciden para ID: {LibroId}", id);
                return BadRequest("Datos inválidos o IDs no coinciden.");
            }

            try
            {
                var resultado = await _libroService.ActualizarLibroAsync(id, libro);

                if (!resultado)
                {
                    var existe = await _libroService.ObtenerLibroPorIdAsync(id);
                    if (existe == null)
                    {
                        _logger.LogWarning("Actualización fallida: Libro ID: {LibroId} no encontrado.", id);
                        return NotFound(new ProblemDetails { Title = "Libro no encontrado", Status = StatusCodes.Status404NotFound, Detail = $"No se encontró un libro con ID {id} para actualizar." });
                    }
                    else
                    {
                        _logger.LogWarning("Actualización fallida: Libro ID: {LibroId} no se actualizó (posiblemente datos iguales o error).", id);
                        return StatusCode(StatusCodes.Status304NotModified);
                    }
                }
                _logger.LogInformation("Libro ID: {LibroId} actualizado exitosamente.", id);
                return NoContent(); // 204 Éxito
            }
            catch (InvalidOperationException ex) // Capturar error específico de ISBN duplicado del servicio
            {
                _logger.LogWarning(ex, "Conflicto al actualizar libro ID: {LibroId}", id);
                return Conflict(new ProblemDetails { Title = "Conflicto al actualizar libro", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar libro ID: {LibroId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Error interno del servidor", Detail = "Ocurrió un error inesperado al actualizar el libro.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        // DELETE: api/libro/5
        [HttpDelete("{id:int}")] // Restricción de tipo
        public async Task<ActionResult> EliminarLibro(int id)
        {
            _logger.LogInformation("Solicitud DELETE recibida para libro ID: {LibroId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("Eliminación fallida: ID inválido: {LibroId}", id);
                return BadRequest("ID de libro inválido.");
            }

            try
            {
                var resultado = await _libroService.EliminarLibroAsync(id);
                if (!resultado)
                {
                    var existe = await _libroService.ObtenerLibroPorIdAsync(id);
                    if (existe == null)
                    {
                        _logger.LogWarning("Eliminación fallida: Libro ID: {LibroId} no encontrado.", id);
                        return NotFound(new ProblemDetails { Title = "Libro no encontrado", Status = StatusCodes.Status404NotFound, Detail = $"No se encontró un libro con ID {id} para eliminar." });
                    }
                    else
                    {
                        _logger.LogWarning("Eliminación fallida: Libro ID: {LibroId} no se pudo eliminar (posiblemente préstamos pendientes).", id);
                        return Conflict(new ProblemDetails { Title = "Conflicto al eliminar", Detail = "No se pudo eliminar el libro. Puede tener préstamos asociados.", Status = StatusCodes.Status409Conflict });
                    }
                }
                _logger.LogInformation("Libro ID: {LibroId} eliminado exitosamente.", id);
                return NoContent(); // 204 Éxito
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al eliminar libro ID: {LibroId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Error interno del servidor", Detail = "Ocurrió un error inesperado al eliminar el libro.", Status = StatusCodes.Status500InternalServerError });
            }
        }
    }
}