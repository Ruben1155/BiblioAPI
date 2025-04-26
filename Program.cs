// --- BiblioAPI/Program.cs ---

// Usings necesarios para los servicios, hashing y configuración
using BiblioAPI.Services;
using BiblioAPI.Models; // Necesario para IPasswordHasher<UsuarioModel>
using Microsoft.AspNetCore.Identity; // Necesario para IPasswordHasher

var builder = WebApplication.CreateBuilder(args);

// 1. --- Configuración de Servicios ---

// Habilitar controladores de API
builder.Services.AddControllers();

// Configuración para Swagger/OpenAPI (documentación y prueba de API)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Opcional: Configuración adicional de Swagger (ej. información de la API)
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Version = "v1",
        Title = "BiblioAPI",
        Description = "API para el Sistema de Gestión de Biblioteca (SiGeBi)"
        // Puedes añadir más información como Contacto, Licencia, etc.
    });
});

// *** Registro del Hasher de Contraseñas ***
// Se registra como Singleton, lo cual es eficiente para este servicio sin estado.
// Se especifica <UsuarioModel> para asociarlo con tu modelo de usuario.
builder.Services.AddSingleton<IPasswordHasher<UsuarioModel>, PasswordHasher<UsuarioModel>>();

// *** Registro de tus Servicios de Lógica de Negocio/Acceso a Datos ***
// Scoped: Se crea una nueva instancia de estos servicios por cada petición HTTP.
// Esto es bueno para servicios que usan recursos como DbContext o HttpClient.
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<LibroService>();
builder.Services.AddScoped<PrestamoService>();
// Nota: IConfiguration ya está disponible por defecto, no necesitas registrarlo manualmente.
// builder.Services.AddSingleton<IConfiguration>(builder.Configuration); //<- Esta línea es redundante

// *** Configuración de CORS (Cross-Origin Resource Sharing) ***
// Esencial para permitir que tu aplicación Frontend (BiblioApp, ejecutándose
// en un origen diferente - localhost con otro puerto) pueda llamar a esta API.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBiblioApp", // Nombre descriptivo de la política
        policyBuilder =>
        {
            // Lee la URL de la app frontend desde la configuración (si la tienes)
            // string? frontendAppUrl = builder.Configuration["AllowedOrigins:BiblioApp"];

            // O especifica los orígenes permitidos directamente.
            // ¡IMPORTANTE! En producción, sé lo más específico posible.
            // Para desarrollo, puedes permitir el origen de tu BiblioApp (ej. https://localhost:7060)
            policyBuilder
                //.WithOrigins(frontendAppUrl ?? "https://localhost:7060") // Permitir origen específico (ajusta el puerto)
                .AllowAnyOrigin() // MÁS PERMISIVO: Permite cualquier origen (Útil para desarrollo, pero menos seguro para producción)
                .AllowAnyHeader() // Permitir cualquier cabecera (Authorization, Content-Type, etc.)
                .AllowAnyMethod(); // Permitir métodos HTTP (GET, POST, PUT, DELETE, etc.)
        });
});


// 2. --- Construcción de la Aplicación ---
var app = builder.Build();


// 3. --- Configuración del Pipeline de Peticiones HTTP ---
// El orden del middleware es importante.

// Configuración específica para el entorno de Desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Muestra páginas de error detalladas
    app.UseSwagger(); // Habilita el middleware de Swagger (genera el JSON)
    app.UseSwaggerUI(options => // Habilita la interfaz de usuario de Swagger
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "BiblioAPI v1");
        // Opcional: Ruta donde estará disponible la UI de Swagger
        // options.RoutePrefix = string.Empty; // Para acceder desde la raíz (/)
    });
}
else
{
    // En producción, usar un manejador de excepciones más genérico
    // app.UseExceptionHandler("/Error"); // Necesitarías crear un endpoint /Error
    // Considera usar middleware de ProblemDetails para errores estándar:
    app.UseStatusCodePages(); // Devuelve respuestas simples para códigos de error sin cuerpo
}

// Redirigir peticiones HTTP a HTTPS
app.UseHttpsRedirection();

// Aplicar la política CORS definida anteriormente
app.UseCors("AllowBiblioApp");

// Habilitar enrutamiento para que las peticiones lleguen a los controladores
app.UseRouting();

// Middleware de Autenticación (si lo implementas más adelante)
// app.UseAuthentication();

// Middleware de Autorización (verifica permisos)
// Asegúrate de tenerlo si usas atributos [Authorize] en los controladores/acciones.
app.UseAuthorization();

// Mapear las rutas definidas en los atributos [Route] de los controladores
app.MapControllers();

// 4. --- Ejecutar la Aplicación ---
app.Run();
